using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 通用全屏后处理渲染器特性：本身不定义具体效果，将面板指定的后处理材质
/// 在指定渲染阶段对相机画面做一次全屏 Blit（效果完全由材质决定）
/// </summary>
public class MyVolumeFeature : ScriptableRendererFeature
{
    public Material Material; //UniversalRenderPipelineAsset_Renderer 面板，设置材质
    public RenderPassEvent renderPassEvent;
    private MyVolumeFeaturePass myPass;


    public override void Create()
    {
        myPass = new MyVolumeFeaturePass();
        myPass.renderPassEvent = renderPassEvent;//设置时间
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {

        renderer.EnqueuePass(myPass);
        myPass.SetValue(Material); //传递摄像机图像，和材质，给Pass 处理
    }
}

/// <summary>
/// 通用全屏后处理 Pass：将相机颜色缓冲经指定材质 Blit 到临时 RT 再 Blit 回来，
/// 完成一次全屏后处理；材质为空或非默认视口时跳过
/// </summary>
public class MyVolumeFeaturePass : ScriptableRenderPass
{
    private RTHandle tempTargetHandle;  // 改用 RTHandle
    private Material material;          // 接受从Feature面板设置的材质
    // 移除 private RenderTargetIdentifier source; // 不再需要外部传入source

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        // 执行后处理
        if (material == null)
        {
            return;
        }
        // 只处理主相机
        if (!renderingData.cameraData.isDefaultViewport) return;

        CommandBuffer cmd = CommandBufferPool.Get("MyVolumeFeaturePass");

        // 获取当前相机的颜色目标（替代外部传入的source）
        var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

        // 获取目标图像的描述符
        var dec = renderingData.cameraData.cameraTargetDescriptor;
        dec.msaaSamples = 1;      // 后处理通常不需要MSAA
        dec.depthBufferBits = 0;  // 不需要深度缓冲区

        // 分配临时RTHandle（替代 GetTemporaryRT）
        RenderingUtils.ReAllocateIfNeeded(ref tempTargetHandle, dec,
            FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempRT");

        // 第一步：从相机颜色目标 Blit 到临时RT（应用材质效果）
        cmd.Blit(cameraColorTarget, tempTargetHandle, material);

        // 第二步：从临时RT Blit 回相机颜色目标（将结果输出到屏幕）
        cmd.Blit(tempTargetHandle, cameraColorTarget);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    // 可选的清理方法：在每帧结束时释放临时RT
    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (tempTargetHandle != null)
        {
            tempTargetHandle.Release();
            tempTargetHandle = null;
        }
    }

    // 更新SetValue方法：不再需要source参数
    public void SetValue(Material material)
    {
        this.material = material;
    }
}