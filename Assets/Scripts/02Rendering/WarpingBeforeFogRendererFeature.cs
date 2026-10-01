using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FPSGame.Rendering
{

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
/// <c>_CameraOpaqueTexture</c>，Warping 抓屏依赖它），又必须早于 450（全屏雾 Pass）。
/// 详见 <see cref="BeforeFogHandDrawPassBase"/>。
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
        _warpingPass = new WarpingBeforeFogPass(settings);
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
    /// 只绑颜色不绑深度（抓屏扰动面片不应参与深度遮挡）。
    /// </summary>
    private class WarpingBeforeFogPass : BeforeFogHandDrawPassBase
    {
        private const string ProfilerTag = "Draw Warping Before Fog";
        private const string DefaultShaderTag = "WarpingEffect";

        private readonly WarpingSettings _settings;

        public WarpingBeforeFogPass(WarpingSettings settings)
            : base(ProfilerTag, DefaultShaderTag, settings.shaderTag)
        {
            _settings = settings;
        }

        protected override LayerMask DrawLayerMask => _settings.layerMask;
        protected override CameraType RenderCamera => _settings.renderCamera;
        protected override bool BindDepthTarget => false;
    }
}
}
