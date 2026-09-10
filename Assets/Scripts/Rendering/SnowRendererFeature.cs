using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 积雪全局控制器：运行时开关与全局积雪量调节的统一入口。
/// SetEnabled(false) 时积雪 Pass 直接跳过（无任何绘制开销）；SetGlobalAmount 可做下雪/化雪渐变；
/// RemoveSnow/AddSnow 按世界坐标擦除或恢复局部积雪（如爆炸弹坑处不该有雪），地形重建时调 ResetMask
/// </summary>
public static class SnowController
{
    public static readonly int SnowEnabledId = Shader.PropertyToID("_SnowEnabled");
    public static readonly int GlobalSnowAmountId = Shader.PropertyToID("_GlobalSnowAmount");

    /// <summary>积雪遮罩纹理 ID（全局纹理阵列，1=满雪 0=无雪）</summary>
    public static readonly int SnowMaskId = Shader.PropertyToID("_SnowMask");
    /// <summary>遮罩映射 ID：xy=地形原点(x,z)，zw=1/地形尺寸(x/z)</summary>
    public static readonly int SnowMaskRectId = Shader.PropertyToID("_SnowMaskRect");
    /// <summary>遮罩每边切片数 ID（0 表示遮罩未创建，Shader 侧直接按满雪处理）</summary>
    public static readonly int SnowMaskTilesId = Shader.PropertyToID("_SnowMaskTiles");

    /// <summary>每边切片数（1=单张 / 2=4 张 / 3=9 张）。切片只影响单次弹坑上传量，不改变遮罩精度</summary>
    public static int MaskTiles = 2;
    /// <summary>每张切片的分辨率（遮罩总分辨率 = MaskTileResolution × MaskTiles）</summary>
    public static int MaskTileResolution = 512;

    // 遮罩数据：按切片存放，_tilePixels[i] 的第 0 个元素 = 该片左下角(v=0)，行方向对应世界 +Z
    // （与 Shader 中 (世界XZ-原点)/地形尺寸 的 UV 映射一致）
    private static Texture2DArray _snowMaskArray;
    private static byte[][] _tilePixels;
    private static bool[] _tileDirty;
    private static int _maskTiles;
    private static int _tileRes;
    private static int _tileCount;
    private static Vector2 _terrainOrigin;
    private static float _terrainSizeXZ;
    private static Terrain _maskTerrain;
    // SetPixelData 的原始数据行序是否与 UV 相反（一次性探测，见 DetectRawRowFlip）
    private static bool _rawRowFlipped;
    private static byte[] _flipBuffer;
    // 逐片上传用的临时纹理（复用）
    private static Texture2D _uploadTex;
    // 平台是否支持 2D → 2DArray 切片的跨类型拷贝（不支持时退化为整块上传）
    private static bool _useSliceCopy;

    static SnowController()
    {
        // 默认关闭积雪（Shader.GetGlobalFloat 对未设置的变量返回 0，需显式初始化），
        // 仅下雪天气由 WeatherSystem.ApplyWeather 调 SetEnabled(true) 开启，避免非雪天积雪 Pass 生效
        Shader.SetGlobalFloat(SnowEnabledId, 0f);
        Shader.SetGlobalFloat(GlobalSnowAmountId, 1f);

        // 遮罩未创建时 Shader 靠 _SnowMaskTiles=0 跳过采样（不依赖占位纹理绑定）
        Shader.SetGlobalVector(SnowMaskRectId, Vector4.zero);
        Shader.SetGlobalFloat(SnowMaskTilesId, 0f);
    }

    /// <summary>开关积雪（false 时跳过整个积雪 Pass）</summary>
    public static void SetEnabled(bool enabled)
    {
        Shader.SetGlobalFloat(SnowEnabledId, enabled ? 1f : 0f);
    }

    /// <summary>设置全局积雪量倍率（0~1），与各材质自身 _SnowAmount 相乘，可做渐变过渡</summary>
    public static void SetGlobalAmount(float amount)
    {
        Shader.SetGlobalFloat(GlobalSnowAmountId, Mathf.Clamp01(amount));
    }

    /// <summary>遮罩是否已创建（调试/查询用）</summary>
    public static bool HasMask => _snowMaskArray != null;

    /// <summary>
    /// 清除指定点的积雪（爆炸弹坑、地形塌陷等处不该有雪）：以 worldPos 的 XZ 投影为圆心，
    /// 把遮罩对应圆形区域擦到 0，Shader 采样后该处积雪强度直接为 0。
    /// 地形未就绪（非地形关卡）或半径非正时静默忽略。
    /// </summary>
    /// <param name="worldPos">世界坐标（只取 XZ 定位，Y 不参与）</param>
    /// <param name="radius">擦除半径（世界单位，与地形破坏半径同一套尺度）</param>
    /// <param name="softness">边缘柔和度 0~1，越大边缘过渡越宽（0 为硬边）</param>
    public static void RemoveSnow(Vector3 worldPos, float radius, float softness = 0.35f)
        => PaintMask(worldPos, radius, 0f, softness);

    /// <summary>恢复指定点的积雪（与 RemoveSnow 相反，可用于回填/消坑）</summary>
    public static void AddSnow(Vector3 worldPos, float radius, float softness = 0.35f)
        => PaintMask(worldPos, radius, 1f, softness);

    /// <summary>
    /// 释放遮罩并把全局遮罩复位为"全雪"（地形重建/换图时调用，避免上一场战斗的弹坑痕迹残留），
    /// 下次擦雪时按新地形按需重建
    /// </summary>
    public static void ResetMask()
    {
        DestroyRuntimeObject(_snowMaskArray);
        _snowMaskArray = null;
        DestroyRuntimeObject(_uploadTex);
        _uploadTex = null;

        _tilePixels = null;
        _tileDirty = null;
        _tileCount = 0;
        _flipBuffer = null;
        _maskTerrain = null;
        Shader.SetGlobalVector(SnowMaskRectId, Vector4.zero);
        Shader.SetGlobalFloat(SnowMaskTilesId, 0f);
    }

    // 运行时对象销毁（编辑期用 DestroyImmediate，否则 Destroy 会被拒绝）
    private static void DestroyRuntimeObject(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(obj);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    /// <summary>
    /// 把遮罩上以 worldPos 为中心、radius 为半径的圆形区域向 target 混合（0=无雪，1=满雪）。
    /// 在 CPU 侧累加（可正确处理多个弹坑重叠），并按切片只统计/标脏被碰到的切片
    /// </summary>
    private static void PaintMask(Vector3 worldPos, float radius, float target, float softness)
    {
        if (radius <= 0f || !EnsureMask())
        {
            return;
        }

        // 世界坐标 → 整块遮罩的像素坐标（与 Shader 中 (世界XZ-原点)/地形尺寸 的映射保持一致）
        int totalRes = _tileRes * _maskTiles;
        float invScale = totalRes / _terrainSizeXZ;
        float centerX = (worldPos.x - _terrainOrigin.x) * invScale;
        float centerY = (worldPos.z - _terrainOrigin.y) * invScale;
        float pixelRadius = radius * invScale;
        if (pixelRadius <= 0f)
        {
            return;
        }

        int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - pixelRadius));
        int maxX = Mathf.Min(totalRes - 1, Mathf.CeilToInt(centerX + pixelRadius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - pixelRadius));
        int maxY = Mathf.Min(totalRes - 1, Mathf.CeilToInt(centerY + pixelRadius));
        if (maxX < minX || maxY < minY)
        {
            // 完全落在地形范围外，无雪可擦
            return;
        }

        // 内圈(0~1-softness)完全生效，外圈(1-softness~1)平滑衰减
        float falloffWidth = Mathf.Max(0.001f, Mathf.Clamp01(softness));
        float targetValue = Mathf.Clamp01(target) * 255f;

        // 只遍历与弹坑相交的切片：小弹坑通常只脏 1 片，单次上传量 = 1/(切片数)
        int minTileX = minX / _tileRes;
        int maxTileX = maxX / _tileRes;
        int minTileY = minY / _tileRes;
        int maxTileY = maxY / _tileRes;

        for (int tileY = minTileY; tileY <= maxTileY; tileY++)
        {
            int tileOriginY = tileY * _tileRes;
            int localMinY = Mathf.Max(minY - tileOriginY, 0);
            int localMaxY = Mathf.Min(maxY - tileOriginY, _tileRes - 1);

            for (int tileX = minTileX; tileX <= maxTileX; tileX++)
            {
                int tileOriginX = tileX * _tileRes;
                int localMinX = Mathf.Max(minX - tileOriginX, 0);
                int localMaxX = Mathf.Min(maxX - tileOriginX, _tileRes - 1);

                int tileIndex = tileY * _maskTiles + tileX;
                byte[] pixels = _tilePixels[tileIndex];
                bool changed = false;

                for (int y = localMinY; y <= localMaxY; y++)
                {
                    // 距离用「整块遮罩」的像素空间算，保证跨切片时形状连续
                    float dy = (tileOriginY + y + 0.5f - centerY) / pixelRadius;
                    int rowOffset = y * _tileRes;
                    for (int x = localMinX; x <= localMaxX; x++)
                    {
                        float dx = (tileOriginX + x + 0.5f - centerX) / pixelRadius;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        if (distance >= 1f)
                        {
                            continue;
                        }

                        float weight = Mathf.Clamp01((1f - distance) / falloffWidth);
                        weight = weight * weight * (3f - 2f * weight);

                        int index = rowOffset + x;
                        int oldValue = pixels[index];
                        int newValue = Mathf.RoundToInt(Mathf.Lerp(oldValue, targetValue, weight));
                        if (newValue != oldValue)
                        {
                            pixels[index] = (byte)newValue;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    _tileDirty[tileIndex] = true;
                }
            }
        }
    }

    /// <summary>
    /// 把累积的遮罩修改上传到 GPU。由积雪 Pass 每帧渲染前调用（与积雪开关无关）：
    /// 同一帧内多次擦雪合并，且只重传被改动的切片（512×512 R8 单片仅 256KB）
    /// </summary>
    public static void FlushMask()
    {
        if (_snowMaskArray == null)
        {
            return;
        }

        for (int i = 0; i < _tileCount; i++)
        {
            if (!_tileDirty[i])
            {
                continue;
            }

            _tileDirty[i] = false;
            UploadTile(i);
        }
    }

    /// <summary>取某切片用于上传的字节缓冲（必要时翻转行，保证第 0 行落在 v=0）</summary>
    private static byte[] GetUploadBuffer(int tileIndex)
    {
        byte[] pixels = _tilePixels[tileIndex];
        if (!_rawRowFlipped)
        {
            return pixels;
        }

        int res = _tileRes;
        for (int y = 0; y < res; y++)
        {
            System.Array.Copy(pixels, y * res, _flipBuffer, (res - 1 - y) * res, res);
        }
        return _flipBuffer;
    }

    /// <summary>上传单个切片：先写进临时纹理，再整片拷进阵列对应切片（GPU 侧只更新这一片，约 256KB）</summary>
    private static void UploadTile(int tileIndex)
    {
        byte[] buffer = GetUploadBuffer(tileIndex);

        if (!_useSliceCopy)
        {
            // 平台不支持跨类型拷贝（如部分 GLES）：退化为整块上传，保证功能可用
            _snowMaskArray.SetPixelData(buffer, tileIndex, 0);
            _snowMaskArray.Apply(false, false);
            return;
        }

        _uploadTex.SetPixelData(buffer, 0);
        _uploadTex.Apply(false, false);
        Graphics.CopyTexture(_uploadTex, 0, 0, _snowMaskArray, tileIndex, 0);
    }

    /// <summary>
    /// 探测 SetPixelData 的原始数据行序：数组第 0 个元素对应 UV v=0（纹理底部）还是 v=1（顶部）。
    /// 用 2x2 RGBA32 探针一次性测定（GetPixel 的坐标约定是文档化确定的：y=0 为底部），
    /// 避免遮罩上下翻转导致弹坑被画到镜像位置
    /// </summary>
    private static bool DetectRawRowFlip()
    {
        Texture2D probe = null;
        try
        {
            probe = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Color32 marker = new Color32(255, 0, 0, 255);
            Color32 plain = new Color32(0, 0, 0, 255);
            probe.SetPixelData(new Color32[] { marker, plain, plain, plain }, 0);
            probe.Apply(false, false);
            // (0,0) 是底部像素：读到的不是我们写的第 0 个元素 → 行序与 UV 相反
            return probe.GetPixel(0, 0).r < 0.5f;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SnowController] 遮罩行序探测失败，按行序一致处理：{e.Message}");
            return false;
        }
        finally
        {
            DestroyRuntimeObject(probe);
        }
    }

    /// <summary>
    /// 懒创建遮罩：地形未就绪返回 false；地形实例变化（新战斗/换图）时按新地形重建。
    /// 遮罩只覆盖地形范围（与 TerrainUtils.WSToUV 同一套换算），地形外由 Shader 侧 saturate 兜底为满雪
    /// </summary>
    private static bool EnsureMask()
    {
        Terrain terrain = TerrainUtils.Main;
        if (terrain == null || terrain.terrainData == null)
        {
            return false;
        }

        if (_snowMaskArray != null && _maskTerrain == terrain)
        {
            return true;
        }

        ResetMask();
        _maskTerrain = terrain;

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.GetPosition();
        _terrainOrigin = new Vector2(terrainPos.x, terrainPos.z);
        // 与 TerrainUtils.WSToUV 一致：X/Z 用同一个 size.x 换算（地图地形为正方形）
        _terrainSizeXZ = terrainData.size.x;
        if (_terrainSizeXZ <= 0f)
        {
            return false;
        }

        _maskTiles = Mathf.Clamp(MaskTiles, 1, 4);
        _tileRes = Mathf.Clamp(MaskTileResolution, 64, 2048);
        _tileCount = _maskTiles * _maskTiles;

        // 一次性探测原始数据行序，决定上传时是否需要翻转行
        _rawRowFlipped = DetectRawRowFlip();
        _flipBuffer = _rawRowFlipped ? new byte[_tileRes * _tileRes] : null;
        _useSliceCopy = (SystemInfo.copyTextureSupport & CopyTextureSupport.DifferentTypes) != 0;

        if (_useSliceCopy)
        {
            _uploadTex = new Texture2D(_tileRes, _tileRes, TextureFormat.R8, false, true)
            {
                name = "SnowMaskUpload",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.DontSave
            };
        }

        _snowMaskArray = new Texture2DArray(_tileRes, _tileRes, _tileCount, TextureFormat.R8, false, true)
        {
            name = "SnowMaskArray",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        // 逐片初始化为满雪（R8 单通道：255=满雪，0=无雪）
        _tilePixels = new byte[_tileCount][];
        _tileDirty = new bool[_tileCount];
        for (int i = 0; i < _tileCount; i++)
        {
            _tilePixels[i] = new byte[_tileRes * _tileRes];
            System.Array.Fill(_tilePixels[i], (byte)255);
        }

        if (_useSliceCopy)
        {
            for (int i = 0; i < _tileCount; i++)
            {
                UploadTile(i);
            }
        }
        else
        {
            // 退化路径：一次填满所有切片再整体上传（避免逐片 Apply 重复上传）
            for (int i = 0; i < _tileCount; i++)
            {
                _snowMaskArray.SetPixelData(GetUploadBuffer(i), i, 0);
            }
            _snowMaskArray.Apply(false, false);
        }

        Shader.SetGlobalTexture(SnowMaskId, _snowMaskArray);
        Shader.SetGlobalVector(SnowMaskRectId, new Vector4(_terrainOrigin.x, _terrainOrigin.y, 1f / _terrainSizeXZ, 1f / _terrainSizeXZ));
        Shader.SetGlobalFloat(SnowMaskTilesId, _maskTiles);
        return true;
    }
}

/// <summary>
/// 积雪覆盖渲染器特性：在指定渲染阶段（默认 AfterRenderingOpaques）用雪材质把配置层内的物体重画一遍，
/// 通过 Alpha 混合只在朝上的表面（头顶/肩膀等）叠出雪色，实现"给物体顶上加一层雪皮"。
/// 支持多条配置（snowEntries）：不同层级可各配一个层遮罩 + 材质实例，用不同 _SnowAmount/_SnowThreshold 等
/// 参数控制积雪强度（如 Ground 层降低积雪量），地形/不需要积雪的物体不配置即完全不受影响。
/// 运行时可通过材质或 Shader.SetGlobalFloat("_SnowAmount") 动态控制积雪量（0~1）
/// </summary>
public class SnowRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class SnowEntry
    {
        /// <summary>雪材质（使用 SnowOverlay Shader），不同层级可用不同参数的材质实例</summary>
        public Material snowMaterial = null;
        /// <summary>该条目需要积雪的物体所在层（如 "Snowable"）</summary>
        public LayerMask snowLayerMask = 0;
    }

    /// <summary>多条积雪配置：不同层级用不同材质参数（积雪量/阈值/颜色等）</summary>
    public List<SnowEntry> snowEntries = new List<SnowEntry>();

    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

    private SnowRenderPass _snowPass;

    // URP 光照 uniform PropertyID
    private static readonly int _MainLightPosition = Shader.PropertyToID("_MainLightPosition");
    private static readonly int _MainLightColor = Shader.PropertyToID("_MainLightColor");
    private static readonly int _MainLightOcclusionProbes = Shader.PropertyToID("_MainLightOcclusionProbes");
    private static readonly int _MainLightLayerMask = Shader.PropertyToID("_MainLightLayerMask");

    public override void Create()
    {
        _snowPass = new SnowRenderPass(snowEntries, renderPassEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _snowPass.Setup(renderer);
        renderer.EnqueuePass(_snowPass);
    }

    /// <summary>
    /// 积雪渲染 Pass：逐条目用 override 雪材质重提交对应层内几何体，
    /// 独立执行时 URP 主光全局变量已失效，绘制前需手动设置 _MainLightPosition/_MainLightColor 等 uniform，
    /// 保证雪 Shader 内 GetMainLight() 光照正确（与 OutlineRenderPass 同款处理）
    /// </summary>
    private class SnowRenderPass : ScriptableRenderPass
    {
        private readonly List<SnowEntry> _entries;
        private ScriptableRenderer _renderer;

        private static readonly ShaderTagId UniversalForwardTagId = new ShaderTagId("UniversalForward");

        // 雪材质参数 PropertyID（Volume 覆盖写入用）
        private static readonly int SnowColorId = Shader.PropertyToID("_SnowColor");
        private static readonly int SnowThresholdId = Shader.PropertyToID("_SnowThreshold");
        private static readonly int SnowSoftnessId = Shader.PropertyToID("_SnowSoftness");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");

        // Volume 控制状态：用于检测 Volume"从有到无"（如隐藏 Volume 物体）后复位残留状态
        private bool _volumeControlling;
        // Volume 覆盖前的材质参数备份，Volume 消失时还原，避免材质被永久改写
        private readonly Dictionary<Material, SnowMaterialBackup> _materialBackups = new Dictionary<Material, SnowMaterialBackup>();

        private struct SnowMaterialBackup
        {
            public Color color;
            public float threshold;
            public float softness;
            public float noise;
        }

        public SnowRenderPass(List<SnowEntry> entries, RenderPassEvent renderPassEvent)
        {
            _entries = entries;
            this.renderPassEvent = renderPassEvent;
        }

        public void Setup(ScriptableRenderer renderer)
        {
            _renderer = renderer;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 先上传本帧累积的遮罩修改（不做积雪开关判断：非雪天打出的弹坑，在下次下雪时也应是无雪的）
            SnowController.FlushMask();

            if (_entries == null || _entries.Count == 0)
            {
                return;
            }

            // 全局开关：关闭时直接跳过，无任何绘制开销
            if (Shader.GetGlobalFloat(SnowController.SnowEnabledId) <= 0f)
            {
                return;
            }

            // Volume 控制（可选层）：场景中配置了 SnowVolume 时以 Volume 为准；
            // Volume 从有到无（如隐藏 Volume 物体）时复位全局积雪量并还原材质参数，避免雪残留
            SnowVolume snowVolume = VolumeManager.instance.stack.GetComponent<SnowVolume>();
            if (!UpdateVolumeControl(snowVolume, out SnowVolume activeVolume))
            {
                return;
            }

            RenderTargetIdentifier cameraColorTarget = _renderer.cameraColorTargetHandle;

            CommandBuffer cmd = CommandBufferPool.Get("Draw Snow Overlay");

            // ---- 手动设置主光 uniform，使雪 pass 中的 GetMainLight() 能正常工作 ----
            SetupMainLightConstants(cmd, ref renderingData);

            // 额外光数量置 0（雪 pass 不需要额外光，避免读脏数据）
            cmd.SetGlobalVector(Shader.PropertyToID("_AdditionalLightsCount"), Vector4.zero);

            cmd.SetRenderTarget(cameraColorTarget);

            SortingSettings sortingSettings = new SortingSettings(renderingData.cameraData.camera)
            {
                criteria = renderingData.cameraData.defaultOpaqueSortFlags
            };

            // 逐条目绘制：每个层级用各自的雪材质（积雪强度由材质参数区分）
            for (int i = 0; i < _entries.Count; i++)
            {
                SnowEntry entry = _entries[i];
                if (entry == null || entry.snowMaterial == null || entry.snowLayerMask == 0)
                {
                    continue;
                }

                // 将 Volume 中勾选覆盖的参数写入该条目材质（未勾选的保留材质自身参数）
                if (activeVolume != null)
                {
                    ApplyVolumeParams(entry.snowMaterial, activeVolume);
                }

                DrawingSettings drawingSettings = new DrawingSettings(UniversalForwardTagId, sortingSettings)
                {
                    overrideMaterial = entry.snowMaterial,
                    overrideMaterialPassIndex = 0
                };

                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all, entry.snowLayerMask);

                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Volume 控制状态机（严格模式：只有 Volume 存在且激活时雪才生效）：
        // - Volume 存在且激活：写入全局积雪量倍率，返回 true
        // - Volume 不存在或未激活：复位全局积雪量为 0 并还原材质参数（雪关闭），返回 false
        private bool UpdateVolumeControl(SnowVolume snowVolume, out SnowVolume activeVolume)
        {
            if (snowVolume != null && snowVolume.IsActive())
            {
                _volumeControlling = true;
                Shader.SetGlobalFloat(SnowController.GlobalSnowAmountId, snowVolume.snowAmount.value);
                activeVolume = snowVolume;
                return true;
            }

            activeVolume = null;
            if (_volumeControlling)
            {
                _volumeControlling = false;
                RestoreMaterialBackups();
            }
            Shader.SetGlobalFloat(SnowController.GlobalSnowAmountId, 0f);
            return false;
        }

        // 将 Volume 中勾选覆盖(overrideState)的参数写入材质，未勾选的保留材质自身参数
        private void ApplyVolumeParams(Material material, SnowVolume volume)
        {
            // 首次覆盖前备份材质当前参数，供 Volume 消失时还原
            if (!_materialBackups.ContainsKey(material))
            {
                _materialBackups[material] = new SnowMaterialBackup
                {
                    color = material.GetColor(SnowColorId),
                    threshold = material.GetFloat(SnowThresholdId),
                    softness = material.GetFloat(SnowSoftnessId),
                    noise = material.GetFloat(NoiseStrengthId)
                };
            }

            if (volume.snowColor.overrideState)
            {
                material.SetColor(SnowColorId, volume.snowColor.value);
            }
            if (volume.snowThreshold.overrideState)
            {
                material.SetFloat(SnowThresholdId, volume.snowThreshold.value);
            }
            if (volume.snowSoftness.overrideState)
            {
                material.SetFloat(SnowSoftnessId, volume.snowSoftness.value);
            }
            if (volume.noiseStrength.overrideState)
            {
                material.SetFloat(NoiseStrengthId, volume.noiseStrength.value);
            }
        }

        // 还原所有被 Volume 覆盖过的材质参数（Volume 消失时调用）
        private void RestoreMaterialBackups()
        {
            foreach (KeyValuePair<Material, SnowMaterialBackup> kvp in _materialBackups)
            {
                Material material = kvp.Key;
                if (material == null)
                {
                    continue;
                }

                material.SetColor(SnowColorId, kvp.Value.color);
                material.SetFloat(SnowThresholdId, kvp.Value.threshold);
                material.SetFloat(SnowSoftnessId, kvp.Value.softness);
                material.SetFloat(NoiseStrengthId, kvp.Value.noise);
            }
            _materialBackups.Clear();
        }

        // 手动设置主光 uniform（与 OutlineRendererFeature 同款逻辑）
        private void SetupMainLightConstants(CommandBuffer cmd, ref RenderingData renderingData)
        {
            Vector4 lightPos = new Vector4(0, -1, 0, 0); // w=0 表示方向
            Vector4 lightColor = Vector4.zero;
            Vector4 lightOcclusionProbes = Vector4.zero;
            int lightLayerMask = 0;

            ref LightData lightData = ref renderingData.lightData;
            if (lightData.mainLightIndex >= 0 && lightData.mainLightIndex < lightData.visibleLights.Length)
            {
                VisibleLight mainLight = lightData.visibleLights[lightData.mainLightIndex];
                var light = mainLight.light;

                if (mainLight.lightType == LightType.Directional)
                {
                    Vector4 dir = -mainLight.localToWorldMatrix.GetColumn(2);
                    lightPos = new Vector4(dir.x, dir.y, dir.z, 0.0f);
                }
                else
                {
                    Vector4 pos = mainLight.localToWorldMatrix.GetColumn(3);
                    lightPos = new Vector4(pos.x, pos.y, pos.z, 1.0f);
                }

                lightColor = mainLight.finalColor;

                if (light != null)
                {
                    var lightBakingOutput = light.bakingOutput;
                    bool isSubtractive = lightBakingOutput.isBaked && lightBakingOutput.lightmapBakeType == LightmapBakeType.Mixed && lightBakingOutput.mixedLightingMode == MixedLightingMode.Subtractive;
                    lightColor.w = isSubtractive ? 0f : 1f;

                    if (lightBakingOutput.lightmapBakeType == LightmapBakeType.Mixed &&
                        0 <= lightBakingOutput.occlusionMaskChannel &&
                        lightBakingOutput.occlusionMaskChannel < 4)
                    {
                        lightOcclusionProbes[lightBakingOutput.occlusionMaskChannel] = 1.0f;
                    }
                }
            }
            else
            {
                // 降级方案：直接从场景 RenderSettings.sun 获取
                Light sun = RenderSettings.sun;
                if (sun != null && sun.isActiveAndEnabled && sun.type == LightType.Directional)
                {
                    Vector4 dir = -sun.transform.forward;
                    lightPos = new Vector4(dir.x, dir.y, dir.z, 0.0f);
                    lightColor = sun.color * sun.intensity;
                    lightColor.w = 1f;
                }
            }

            cmd.SetGlobalVector(_MainLightPosition, lightPos);
            cmd.SetGlobalVector(_MainLightColor, lightColor);
            cmd.SetGlobalVector(_MainLightOcclusionProbes, lightOcclusionProbes);
            cmd.SetGlobalInt(_MainLightLayerMask, lightLayerMask);

            // GetMainLight() 中 light.distanceAttenuation = unity_LightData.z，必须为 1
            cmd.SetGlobalVector(Shader.PropertyToID("unity_LightData"), new Vector4(0, 0, 1, 0));
        }
    }
}
