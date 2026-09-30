using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FPSGame.Rendering
{

/// <summary>
/// 「全屏雾之前手绘」通用的渲染 Pass 基类（由各自的 <see cref="ScriptableRendererFeature"/> 包装）。
/// <para>
/// 445 是唯一可行的绘制窗口：必须晚于 AfterRenderingSkybox(400)
/// （天空盒已画完、URP 的 CopyColorPass 已生成 <c>_CameraOpaqueTexture</c>），
/// 又必须早于全屏雾 Pass(450 / BeforeRenderingTransparents)，
/// 这样被手绘的物体才既位于天空盒之上、又吃得到全屏雾。
/// </para>
/// <para>
/// 子类只需给出 Profiler 名称、默认 Shader Tag，以及三个取值：绘制层、生效相机、是否绑深度。
/// </para>
/// </summary>
public abstract class BeforeFogHandDrawPassBase : ScriptableRenderPass
{
    private readonly ShaderTagId _shaderTagId;

    /// <summary>参与手绘的层</summary>
    protected abstract LayerMask DrawLayerMask { get; }

    /// <summary>生效的相机类型（与 FullScreenFogRendererFeature 保持一致）</summary>
    protected abstract CameraType RenderCamera { get; }

    /// <summary>
    /// 是否同时绑定深度目标。
    /// 需要被地形等遮挡的物体（如天空层）必须绑；纯抓屏扰动物体（如 Warping）只绑颜色。
    /// </summary>
    protected abstract bool BindDepthTarget { get; }

    private ScriptableRenderer _renderer;

    /// <param name="profilerTag">Profiler / CommandBuffer 名称</param>
    /// <param name="defaultShaderTag">配置的 Shader Tag 为空时使用的默认值</param>
    /// <param name="shaderTag">配置的 Shader Tag，必须与目标 Shader 中 Pass 的 LightMode 完全一致</param>
    protected BeforeFogHandDrawPassBase(string profilerTag, string defaultShaderTag, string shaderTag)
    {
        _shaderTagId = new ShaderTagId(string.IsNullOrEmpty(shaderTag) ? defaultShaderTag : shaderTag);
        profilingSampler = new ProfilingSampler(profilerTag);
        // 445：晚于天空盒(400) / _CameraOpaqueTexture 生成，早于全屏雾 Pass(450)
        renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 5);
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
            || (cameraData.cameraType & RenderCamera) == 0)
        {
            return;
        }

        CommandBuffer cmd = CommandBufferPool.Get(profilingSampler.name);
        if (BindDepthTarget)
        {
            // 必须同时绑定颜色与深度：手绘物体要能正常被地形等遮挡，只绑颜色会丢掉深度测试
            CoreUtils.SetRenderTarget(cmd, _renderer.cameraColorTargetHandle, _renderer.cameraDepthTargetHandle);
        }
        else
        {
            cmd.SetRenderTarget(_renderer.cameraColorTargetHandle);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        // 透明物排序：队列优先，同队列按距离由远到近
        var sortingSettings = new SortingSettings(cameraData.camera)
        {
            criteria = SortingCriteria.CommonTransparent
        };
        var drawingSettings = new DrawingSettings(_shaderTagId, sortingSettings);
        // 手绘对象的材质队列都在透明段，只筛透明队列范围
        var filteringSettings = new FilteringSettings(RenderQueueRange.transparent, DrawLayerMask);

        context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
    }
}
}
