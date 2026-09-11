using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Warping 抓屏扰动特效（Assets/Shader/Warping.shader）的专用渲染器特性。
/// <para>
/// 背景：Warping 材质队列为 Transparent+1，而全屏雾 Pass 的注入点是
/// BeforeRenderingTransparents(450)，即雾在透明物之前合成 —— Warping 随后绘制并整片覆盖
/// （col.a = 1），表现为"一块完全不吃雾的破洞"。
/// </para>
/// <para>
/// 做法：把 Warping 单独提前到 445 绘制（雾之前），让雾把它一并雾化。
/// 445 是唯一可行窗口：必须晚于 AfterRenderingSkybox(400)（URP 的 CopyColorPass 在此刻生成
/// _CameraOpaqueTexture，Warping 抓屏依赖它），又必须早于 450（全屏雾 Pass）。
/// </para>
/// <para>
/// 注意：Warping.shader 的 Pass 已由 UniversalForward 改为 "LightMode" = "WarpingEffect"，
/// 因此该特性被禁用或移除后 Warping 特效将不再显示（不再由 URP 默认透明 Pass 兜底）。
/// </para>
/// </summary>
public class WarpingBeforeFogRendererFeature : ScriptableRendererFeature
{
    /// <summary>Warping 手绘 Pass 的配置</summary>
    [System.Serializable]
    public class WarpingSettings
    {
        /// <summary>参与手绘的层</summary>
        [InspectorName("参与手绘的层")]
        public LayerMask layerMask = -1;

        /// <summary>自定义 ShaderTag，必须与 Warping.shader 中 Pass 的 LightMode 完全一致</summary>
        [InspectorName("Shader Tag（须与 Warping.shader 的 LightMode 一致）")]
        public string shaderTag = "WarpingEffect";

        /// <summary>生效的相机类型（与 FullScreenFogRendererFeature 保持一致）</summary>
        [InspectorName("生效相机类型")]
        public CameraType renderCamera = CameraType.Game | CameraType.SceneView;
    }

    public WarpingSettings settings = new WarpingSettings();

    private WarpingBeforeFogPass _warpingPass;

    /// <inheritdoc/>
    public override void Create()
    {
        _warpingPass = new WarpingBeforeFogPass(settings)
        {
            // 445：晚于 _CameraOpaqueTexture 生成(400)、早于全屏雾 Pass(450)
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 5)
        };
    }

    /// <inheritdoc/>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_warpingPass == null)
        {
            return;
        }

        _warpingPass.Setup(renderer);
        renderer.EnqueuePass(_warpingPass);
    }

    /// <summary>
    /// Warping 手绘 Pass：用自定义 ShaderTag 把 Warping 几何在雾之前重画一遍。
    /// 结构参照 OutlineRendererFeature，但不需要手动设置主光 uniform（Warping 是无光照抓屏 Shader）
    /// </summary>
    private class WarpingBeforeFogPass : ScriptableRenderPass
    {
        private const string ProfilerTag = "Draw Warping Before Fog";
        private const string DefaultShaderTag = "WarpingEffect";

        private readonly WarpingSettings _settings;
        private readonly ShaderTagId _shaderTagId;

        private ScriptableRenderer _renderer;

        public WarpingBeforeFogPass(WarpingSettings settings)
        {
            _settings = settings;
            _shaderTagId = new ShaderTagId(string.IsNullOrEmpty(settings.shaderTag) ? DefaultShaderTag : settings.shaderTag);
            profilingSampler = new ProfilingSampler(ProfilerTag);
        }

        public void Setup(ScriptableRenderer renderer)
        {
            _renderer = renderer;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;

            // 相机堆叠中只有 Base 相机渲染完整场景，与 FullScreenFogRendererFeature 的约定一致
            if (cameraData.renderType != CameraRenderType.Base
                || (cameraData.cameraType & _settings.renderCamera) == 0)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get(ProfilerTag);
            cmd.SetRenderTarget(_renderer.cameraColorTargetHandle);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            // 透明物排序：保证多个 Warping 面片之间的前后关系正确
            var sortingSettings = new SortingSettings(cameraData.camera)
            {
                criteria = SortingCriteria.CommonTransparent
            };
            var drawingSettings = new DrawingSettings(_shaderTagId, sortingSettings);
            // Warping 队列为 Transparent+1，仅在透明队列范围内做筛选
            var filteringSettings = new FilteringSettings(RenderQueueRange.transparent, _settings.layerMask);

            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
        }
    }
}
