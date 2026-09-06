using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _outlinePass.Setup(renderer);
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

        private static readonly ShaderTagId OutlineShaderTagId = new ShaderTagId("Outline");

        public OutlineRenderPass(OutlineSettings settings)
        {
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;
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
            RenderTargetIdentifier cameraColorTarget = _renderer.cameraColorTargetHandle;

            CommandBuffer cmd = CommandBufferPool.Get("Draw Outlines");

            // ---- 手动设置主光 uniform，使 Outline pass 中的 GetMainLight() 能正常工作----
            SetupMainLightConstants(cmd, ref renderingData);

            // 额外光数量置 0（Outline 不需要额外光，避免读脏数据导致奇怪效果）
            cmd.SetGlobalVector(Shader.PropertyToID("_AdditionalLightsCount"), Vector4.zero);

            // 设置渲染目标
            cmd.SetRenderTarget(cameraColorTarget);

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

            // 过滤设置
            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all, _settings.outlineLayerMask);

            // 执行绘制
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
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