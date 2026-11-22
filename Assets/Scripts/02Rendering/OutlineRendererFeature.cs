using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FPSGame.Rendering
{

/// <summary>
/// 描边渲染器特性：在指定渲染阶段（默认 AfterRenderingOpaques）后，用 "Outline" ShaderTagId
/// 将 outlineLayerMask 层内物体重画一遍（走 Shader 的 Outline 额外 pass），实现物体描边
/// </summary>
public class OutlineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class OutlineSettings
    {
        public Material outlineMaterial = null;
        public LayerMask outlineLayerMask = -1;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        [Tooltip("描边最大距离(米)，0 = 不限。描边是相机剔除后再画一遍全场，远处的线只有一两像素却照样付 draw call")]
        [Min(0f)]
        public float maxDistance = 0f;

        [Tooltip("描边最小屏幕像素高度：估算投影高度低于该值的渲染器这一趟不画（0 = 关闭）。" +
                 "远处小道具的描边本来就只有几像素，砍掉肉眼无感 —— 比按层砍安全（Default 层也有要描边的物体）")]
        [Min(0f)]
        public float minScreenPixels = 0f;

        [Tooltip("只给\"cullingMask 里真有可描边物体\"的相机跑这个 pass。UIFx/武器相机通常一层都命中不了，" +
                 "跳过整趟可以省掉一次全场遍历 + 命令缓冲设置")]
        public bool skipCamerasWithoutOutlineLayers = true;
    }

    public OutlineSettings settings = new OutlineSettings();

    private OutlineRenderPass _outlinePass;

    // URP 光照 uniform PropertyID
    private static readonly int _MainLightPosition = Shader.PropertyToID("_MainLightPosition");
    private static readonly int _MainLightColor = Shader.PropertyToID("_MainLightColor");
    private static readonly int _MainLightOcclusionProbes = Shader.PropertyToID("_MainLightOcclusionProbes");
    private static readonly int _MainLightLayerMask = Shader.PropertyToID("_MainLightLayerMask");

    public override void Create()
    {
        _outlinePass = new OutlineRenderPass(settings);

        // 切换场景（运行时 LoadScene，编辑器里打开场景）后立刻作废分桶，下一帧就重扫一次，
        // 而不是等 10 秒长间隔 —— 否则"刚进新场景描边要等一会儿才出来"。
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_outlinePass != null) _outlinePass.InvalidateBuckets();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 每台相机都会进这里：先把"可描边渲染器"的分层缓存刷新到最新（10s 一次，与相机无关）
        _outlinePass.EnsureBuckets();

        // 该相机一层可描边物体都看不到（UIFx/武器相机）⇒ 整趟 pass 都不用排。
        // ⚠ 只在**运行时**做这个预判：编辑模式（未进 Play）不看分桶，永远排这趟 —— 否则在大厅/
        //   空场景里分桶为 0，Scene 视图就看不到任何描边（"原本能看到的描边没了"就是这个原因）。
        if (settings.skipCamerasWithoutOutlineLayers
            && Application.isPlaying
            && (renderingData.cameraData.camera.cullingMask & _outlinePass.OutlineLayerBits) == 0)
        {
            return;
        }

        // 配了覆盖材质 ⇒ 用同一份描边材质给"这一层里的所有渲染器"描边（不要求它们自己有 Outline pass）；
        // 没配 ⇒ 走各材质自己的 Outline pass（当前工程的用法）。
        if (settings.outlineMaterial != null) _outlinePass.SetupWithMaterial(renderer, settings.outlineMaterial);
        else _outlinePass.Setup(renderer);

        renderer.EnqueuePass(_outlinePass);
    }

    /// <summary>
    /// 描边渲染 Pass：重新提交几何体绘制描边。因独立执行时 URP 主光全局变量已失效，
    /// 绘制前需手动设置 _MainLightPosition/_MainLightColor 等 uniform，保证 Outline pass 内 GetMainLight() 光照正确
    /// </summary>
    private class OutlineRenderPass : ScriptableRenderPass
    {
        private readonly OutlineSettings _settings;
        private ScriptableRenderer _renderer;
        private Material _overrideMaterial;
        private bool _useOverrideMaterial;

        // 按层分组的"可描边"渲染器缓存（10s 重扫一次）+ 本趟被临时静音的列表。
        // ⚠ 分桶只放"材质里真有 Outline pass"的渲染器 —— DrawRenderers 本来也只画这些，
        //   这样每台相机只遍历自己 cullingMask 命中的层，UIFx/武器相机往往一趟只有 0~11 个。
        private readonly List<Renderer>[] _byLayer = new List<Renderer>[32];
        private readonly Dictionary<Shader, bool> _shaderOutlineCache = new Dictionary<Shader, bool>();
        private readonly List<Renderer> _suppressed = new List<Renderer>(256);
        private int _outlineLayerBits = -1;      // 存在可描边物体的层位集；-1 = 还没扫过（不拦任何相机）
        private float _nextScan;

        private static readonly ShaderTagId OutlineShaderTagId = new ShaderTagId("Outline");
        private static readonly ShaderTagId LightModeTagId = new ShaderTagId("LightMode");

        /// <summary>存在"可描边渲染器"的层位集（<c>AddRenderPasses</c> 用它决定要不要排这趟）。</summary>
        public int OutlineLayerBits => _outlineLayerBits;

        public OutlineRenderPass(OutlineSettings settings)
        {
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;
            for (int i = 0; i < _byLayer.Length; i++) _byLayer[i] = new List<Renderer>();
        }

        public void Setup(ScriptableRenderer renderer)
        {
            _renderer = renderer;
            _useOverrideMaterial = false;
        }

        public void SetupWithMaterial(ScriptableRenderer renderer, Material material)
        {
            _renderer = renderer;
            _overrideMaterial = material;
            _useOverrideMaterial = true;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Draw Outlines");

            // ---- 手动设置主光 uniform，使 Outline pass 中的 GetMainLight() 能正常工作----
            SetupMainLightConstants(cmd, ref renderingData);

            // 额外光数量置 0（Outline 不需要额外光，避免读脏数据导致奇怪效果）
            cmd.SetGlobalVector(Shader.PropertyToID("_AdditionalLightsCount"), Vector4.zero);

            // ⚠ 命令必须在 DrawRenderers 之前执行：DrawRenderers 是立即提交，排在它后面的 cmd
            //   （全局 uniform 等）当帧不生效，只能靠"全局残留到下一帧"侥幸。
            // ⚠ 千万别在这里 cmd.SetRenderTarget(colorTarget)：只绑 color 会把当前绑好的深度摘掉，
            //   描边壳失去遮挡关系 ⇒ 描边"穿透"所有物体。AfterRenderingOpaques 时相机颜色+深度已绑好，直接继承。
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            // 排序设置
            SortingSettings sortingSettings = new SortingSettings(renderingData.cameraData.camera)
            {
                criteria = renderingData.cameraData.defaultOpaqueSortFlags
            };

            // 绘制设置
            DrawingSettings drawingSettings;
            if (_useOverrideMaterial && _overrideMaterial != null)
            {
                drawingSettings = new DrawingSettings(OutlineShaderTagId, sortingSettings)
                {
                    overrideMaterial = _overrideMaterial,
                    overrideMaterialPassIndex = 0
                };
            }
            else
            {
                drawingSettings = new DrawingSettings(OutlineShaderTagId, sortingSettings);
            }

            // 与 URP 标准 pass 用同一份 per-object 光照数据（探针 SH / lightmap / unity_LightData 等）。
            // 不设的话 DrawRenderers 默认 PerObjectData.None，重画出来的描边读到的是"上一个物体残留"的光照数据。
            drawingSettings.perObjectData = renderingData.perObjectData;

            // 过滤设置
            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all, _settings.outlineLayerMask);

            // 距离/屏幕尺寸受限：把"不用描边也看不见"的渲染器这一趟临时静音，画完立刻还原 —— 保住 DrawRenderers 的排序/合批
            bool limited = _settings.maxDistance > 0f || _settings.minScreenPixels > 0f;
            if (limited) SuppressFarRenderers(renderingData.cameraData.camera);
            try
            {
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            }
            finally
            {
                if (limited) RestoreSuppressed();
            }
        }

        /// <summary>
        /// 每 10s 重扫一次场景渲染器，**按 layer 分桶**，并且只保留"材质里真有 Outline pass"的
        /// （<c>DrawRenderers</c> 本来也只画这些）。这样每台相机只遍历自己 <c>cullingMask</c> 命中的层，
        /// UIFx/武器相机往往一趟就是 0~11 个，而不是全场 600+。
        /// <para>⚠ 桶是 10s 快照：期间换层的 renderer 可能暂时落在旧桶里 —— 最坏只是"少剔一个"或
        /// "剔了一个本相机看不见的"，后者因为 <c>forceRenderingOff</c> 在 try/finally 内立即还原，无副作用。</para>
        /// </summary>
        public void EnsureBuckets()
        {
            // 编辑模式下分桶只有两个用途：①给 AddRenderPasses 做预判（已旁路）②给距离/像素过滤临时静音。
            // 两个用途在编辑模式都不存在时（默认配置就是），别每 0.5s 扫一遍全场 Renderer。
            // ⚠ 这里**不要**改 _nextScan：保持原值，这样进入 Play 的第一帧会立刻重扫一次（不会等 10 秒）。
            if (!Application.isPlaying && _settings.maxDistance <= 0f && _settings.minScreenPixels <= 0f) return;

            // ⚠ 用 realtimeSinceStartup 而不是 unscaledTime：**编辑模式（未进 Play）**下也要能扫到描边对象，
            //   而 unscaledTime 依赖玩法时钟，在编辑器里不稳定。realtimeSinceStartup 两种模式都匀速推进。
            if (Time.realtimeSinceStartup < _nextScan) return;

            // 分桶为空时用短间隔快速重试：刚进场景 / 刚打开场景 / 刚拖进模型时，对象可能还没就绪，
            // 否则要等满 10 秒描边才出现（实测"进游戏一会儿后描边才显示"就是这个）。
            // 一旦扫到内容就回到 10 秒长间隔，避免每帧全场 Find。
            _nextScan = Time.realtimeSinceStartup + (_outlineLayerBits == 0 ? 0.5f : 10f);

            for (int i = 0; i < _byLayer.Length; i++) _byLayer[i].Clear();
            _outlineLayerBits = 0;

            int mask = _settings.outlineLayerMask.value;
            var all = Object.FindObjectsOfType<Renderer>();
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                if (r == null) continue;

                int layer = r.gameObject.layer;
                if ((mask & (1 << layer)) == 0) continue;
                if (!HasOutlinePass(r)) continue;

                _byLayer[layer].Add(r);
                _outlineLayerBits |= 1 << layer;
            }
        }

        /// <summary>作废分桶，让下一次 <see cref="EnsureBuckets"/> 立刻重扫（切换场景 / 手动刷新时调）。</summary>
        public void InvalidateBuckets()
        {
            _nextScan = 0f;
            _outlineLayerBits = 0;
        }

        /// <summary>渲染器用的材质里是否有 <c>LightMode = Outline</c> 的 pass。
        /// 按 shader 缓存 —— 全场只有 ~20 个 shader，每个只查一次 pass 表。</summary>
        private bool HasOutlinePass(Renderer r)
        {
            // 覆盖材质模式下所有物体都会用同一份描边材质，不能按原材质过滤
            if (_useOverrideMaterial && _overrideMaterial != null) return true;

            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || m.shader == null) continue;

                bool has;
                if (!_shaderOutlineCache.TryGetValue(m.shader, out has))
                {
                    has = ShaderHasOutlinePass(m.shader);
                    _shaderOutlineCache[m.shader] = has;
                }
                if (has) return true;
            }
            return false;
        }

        private static bool ShaderHasOutlinePass(Shader shader)
        {
            for (int p = 0; p < shader.passCount; p++)
            {
                if (shader.FindPassTagValue(p, LightModeTagId).name == "Outline") return true;
            }
            return false;
        }

        /// <summary>
        /// 把"这一趟描边看不见"的渲染器临时设成 <c>forceRenderingOff</c>：超过
        /// <see cref="OutlineSettings.maxDistance"/>，或投影像素高度小于
        /// <see cref="OutlineSettings.minScreenPixels"/>。
        /// <para>▍只遍历 <c>cam.cullingMask &amp; _outlineLayerBits</c> 命中的层 —— 相机根本看不见的层不参与。</para>
        /// <para>⚠ 绕这一圈的原因：<c>DrawRenderers</c> 只吃相机的 cullResults（已按 far 剔过），没有"再按距离筛"的参数，
        /// 而 CullingResults 也没有遍历可见渲染器的公开接口 ⇒ 只能自己维护列表 + 临时静音。</para>
        /// <para>⚠ 必须 try/finally 还原：漏还原会让这些物体整局不再绘制；原本就关着的（区块剔除/隐藏）直接跳过，别抢状态。</para>
        /// </summary>
        private void SuppressFarRenderers(Camera cam)
        {
            bool byDistance = _settings.maxDistance > 0f;
            bool byPixels = _settings.minScreenPixels > 0f;
            if (!byDistance && !byPixels) return;

            int layers = cam.cullingMask & _outlineLayerBits;
            if (layers == 0) return;

            Vector3 camPos = cam.transform.position;
            float maxSqr = byDistance ? _settings.maxDistance * _settings.maxDistance : float.PositiveInfinity;

            // 投影像素高度 ≈ 半径 × pixelScale / 距离（正交与距离无关）。
            // 比较时两边平方，避免每个渲染器一次 sqrt。
            float minPxSqr = 0f;
            float pixelScale = 0f;
            if (byPixels)
            {
                minPxSqr = _settings.minScreenPixels * _settings.minScreenPixels;
                pixelScale = cam.orthographic
                    ? cam.pixelHeight / Mathf.Max(0.01f, cam.orthographicSize)
                    : cam.pixelHeight / Mathf.Max(0.01f, Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
                pixelScale *= pixelScale;   // 后面直接和半径平方比较
            }

            for (int layer = 0; layer < 32; layer++)
            {
                if ((layers & (1 << layer)) == 0) continue;

                var list = _byLayer[layer];
                for (int i = 0; i < list.Count; i++)
                {
                    Renderer r = list[i];
                    if (r == null || !r.enabled || r.forceRenderingOff) continue;

                    Bounds b = r.bounds;                     // 只读一次（原生调用）
                    float sqr = b.SqrDistance(camPos);
                    bool suppress = byDistance && sqr > maxSqr;

                    if (!suppress && byPixels)
                    {
                        float extSqr = b.extents.sqrMagnitude;
                        // 半径取不到（粒子未播放/未初始化）时不要静音，否则整组 FX 会一直消失
                        if (extSqr > 1e-4f)
                        {
                            float kSqr = extSqr * pixelScale;
                            suppress = cam.orthographic
                                ? kSqr < minPxSqr
                                : kSqr < minPxSqr * Mathf.Max(sqr, 1e-4f);
                        }
                    }

                    if (!suppress) continue;
                    r.forceRenderingOff = true;
                    _suppressed.Add(r);
                }
            }
        }

        private void RestoreSuppressed()
        {
            for (int i = 0; i < _suppressed.Count; ++i)
            {
                Renderer r = _suppressed[i];
                if (r != null) r.forceRenderingOff = false;
            }
            _suppressed.Clear();
        }

        // 手动设置主光 uniform（直接从场景 Light 组件获取，不依赖 URP lightData 的生命周期）
        private void SetupMainLightConstants(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 默认值：方向光朝下，颜色黑色
            Vector4 lightPos = new Vector4(0, -1, 0, 0); // w=0 表示方向
            Vector4 lightColor = Vector4.zero;
            Vector4 lightOcclusionProbes = Vector4.zero;
            int lightLayerMask = 0;

            // renderingData.lightData 获取主光源（优先
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

            // GetMainLight() 中light.distanceAttenuation = unity_LightData.z
            // unity_LightData.z 必须为1，否则距离衰减为 0 导致主光无效
            cmd.SetGlobalVector(Shader.PropertyToID("unity_LightData"), new Vector4(0, 0, 1, 0));


        }
    }
}
}
