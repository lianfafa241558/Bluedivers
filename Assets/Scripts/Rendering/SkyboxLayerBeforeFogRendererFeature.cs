using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 天空层（<c>Assets/Shader/Environment/SkyboxLayer.shader</c>：星星 / 月亮 / 云）专用的渲染器特性。
/// <para>
/// 这些物体是包围相机、尺度 13000~15000（月亮面片在 z=-10000）的"天空层"，
/// 远在相机远裁剪面之外，全靠 shader 里的 <c>ZClip Off</c> 才能画出来 —— 代价是它们的深度会被钳到远平面，
/// 与天空盒正好同一个深度。
/// </para>
/// <para>
/// 于是出现两种都不可用的状态：
/// <br/>① 队列 &lt; 2500：进 URP 的不透明 Pass(300) 绘制，可见但随后天空盒 Pass(400) 会在"相同深度"上重画，
/// 把整片天空层擦掉 —— 表现就是"吃雾了，但看不见了"。
/// <br/>② 队列 Transparent(3000+)：在 450 的透明 Pass 里绘制，位置在天空盒之后所以看得见，
/// 但全屏雾 Pass 的注入点 BeforeRenderingTransparents(450) 排在 URP 透明 Pass 之前，
/// 天空层整个盖在雾之上 —— 表现就是"看得见，但完全不吃雾"。
/// </para>
/// <para>
/// 做法：把 SkyboxLayer.shader 的 Pass 改成自定义 LightMode <c>SkyboxLayerBeforeFog</c>，
/// 由本特性在 445 单独绘制一遍。445 是唯一可行窗口：
/// 必须晚于 AfterRenderingSkybox(400)（否则被天空盒擦掉），又必须早于全屏雾 Pass(450)（否则不吃雾）。
/// 这样天空层既在天空盒之上、又在雾之下；且雾 Pass 读取的深度纹理里这些像素仍是远平面深度，
/// 它们得到的雾浓度与天空盒完全一致，不会与天空出现色差。
/// </para>
/// <para>
/// 注意：SkyboxLayer.shader 的 Pass 已改为自定义 LightMode，本特性被禁用或移除后天空层将不再显示
/// （不再由 URP 默认透明 Pass 兜底）。结构与 <see cref="WarpingBeforeFogRendererFeature"/> 一致。
/// </para>
/// </summary>
public class SkyboxLayerBeforeFogRendererFeature : ScriptableRendererFeature
{
    /// <summary>天空层手绘 Pass 的配置</summary>
    [System.Serializable]
    public class SkyboxLayerSettings
    {
        /// <summary>参与手绘的层（天空层默认都在 Default 层，保持 Everything 即可）</summary>
        [InspectorName("参与手绘的层")]
        public LayerMask layerMask = -1;

        /// <summary>自定义 ShaderTag，必须与 SkyboxLayer.shader 中 Pass 的 LightMode 完全一致</summary>
        [InspectorName("Shader Tag（须与 SkyboxLayer.shader 的 LightMode 一致）")]
        public string shaderTag = "SkyboxLayerBeforeFog";

        /// <summary>生效的相机类型（与 FullScreenFogRendererFeature 保持一致）</summary>
        [InspectorName("生效相机类型")]
        public CameraType renderCamera = CameraType.Game | CameraType.SceneView;
    }

    public SkyboxLayerSettings settings = new SkyboxLayerSettings();

    private SkyboxLayerBeforeFogPass _skyboxLayerPass;

    /// <inheritdoc/>
    public override void Create()
    {
        _skyboxLayerPass = new SkyboxLayerBeforeFogPass(settings)
        {
            // 445：晚于天空盒(400)、早于全屏雾 Pass(450)
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 5)
        };
    }

    /// <inheritdoc/>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_skyboxLayerPass == null)
        {
            return;
        }

        _skyboxLayerPass.Setup(renderer);
        renderer.EnqueuePass(_skyboxLayerPass);
    }

    /// <summary>
    /// 天空层手绘 Pass：用自定义 ShaderTag 把天空层几何在天空盒之后、全屏雾之前重画一遍。
    /// 不需要手动设置主光 uniform（SkyboxLayer 是无光照的背景 Shader）。
    /// </summary>
    private class SkyboxLayerBeforeFogPass : ScriptableRenderPass
    {
        private const string ProfilerTag = "Draw Skybox Layer Before Fog";
        private const string DefaultShaderTag = "SkyboxLayerBeforeFog";

        private readonly SkyboxLayerSettings _settings;
        private readonly ShaderTagId _shaderTagId;

        private ScriptableRenderer _renderer;

        public SkyboxLayerBeforeFogPass(SkyboxLayerSettings settings)
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
            // 必须同时绑定颜色与深度：天空层要能正常被地形等遮挡（shader 里 ZTest LEqual），
            // 只绑颜色会丢掉深度测试，云/星空会整片盖在地面上
            CoreUtils.SetRenderTarget(cmd, _renderer.cameraColorTargetHandle, _renderer.cameraDepthTargetHandle);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            // 透明物排序：队列优先（星星/月亮 3000 在前，云 3500 在后），同队列按距离由远到近
            var sortingSettings = new SortingSettings(cameraData.camera)
            {
                criteria = SortingCriteria.CommonTransparent
            };
            var drawingSettings = new DrawingSettings(_shaderTagId, sortingSettings);
            // 天空层材质队列都在透明段（3000/3500），只筛透明队列范围
            var filteringSettings = new FilteringSettings(RenderQueueRange.transparent, _settings.layerMask);

            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
        }
    }
}
