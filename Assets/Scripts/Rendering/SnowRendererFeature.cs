using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 积雪全局控制器：运行时开关与全局积雪量调节的统一入口。
/// SetEnabled(false) 时积雪 Pass 直接跳过（无任何绘制开销）；SetGlobalAmount 可做下雪/化雪渐变
/// </summary>
public static class SnowController
{
    public static readonly int SnowEnabledId = Shader.PropertyToID("_SnowEnabled");
    public static readonly int GlobalSnowAmountId = Shader.PropertyToID("_GlobalSnowAmount");

    static SnowController()
    {
        // 默认开启、积雪量倍率 1（Shader.GetGlobalFloat 对未设置的变量返回 0，需初始化）
        Shader.SetGlobalFloat(SnowEnabledId, 1f);
        Shader.SetGlobalFloat(GlobalSnowAmountId, 1f);
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

        // Volume 控制状态机：
        // - Volume 存在：写入全局积雪量倍率，返回是否激活（积雪量>0）
        // - Volume 不存在且之前在控制：复位全局积雪量为 0 并还原材质参数（雪关闭），返回 false
        // - Volume 从未存在：维持原行为（材质自身参数生效），返回 true
        private bool UpdateVolumeControl(SnowVolume snowVolume, out SnowVolume activeVolume)
        {
            if (snowVolume != null)
            {
                _volumeControlling = true;
                Shader.SetGlobalFloat(SnowController.GlobalSnowAmountId, snowVolume.snowAmount.value);
                activeVolume = snowVolume;
                return snowVolume.IsActive();
            }

            activeVolume = null;
            if (_volumeControlling)
            {
                _volumeControlling = false;
                Shader.SetGlobalFloat(SnowController.GlobalSnowAmountId, 0f);
                RestoreMaterialBackups();
                return false;
            }
            return true;
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
