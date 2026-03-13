using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        myPass.SetValue(renderer.cameraColorTarget, Material); //传递摄像机图像，和材质，给Pass 处理
    }
}

public class MyVolumeFeaturePass : ScriptableRenderPass
{
    private Material material;//接受从Feature 面板设置的材质
    private RenderTargetIdentifier source;//接受相机图像

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        //执行后处理
        if (material == null)
        {
            return;
        }
        // 只处理主相机
        if (!renderingData.cameraData.isDefaultViewport) return;

        // 获取主相机的渲染目标（替代外部传入的source）
        //var mainCameraTarget = renderingData.cameraData.renderer.cameraColorTarget;
        //好像不需要

        CommandBuffer cmd = CommandBufferPool.Get();
        //source  //源图像
        var dec = renderingData.cameraData.cameraTargetDescriptor; //目标图像
        RenderTargetHandle tempTargetHandle = new RenderTargetHandle();
        cmd.GetTemporaryRT(tempTargetHandle.id, dec);
        
        cmd.Blit(source, tempTargetHandle.Identifier(), material);
        //核心命令CommandBuffer
        cmd.Blit(tempTargetHandle.Identifier(), source); //相当于 Graphics.Blit

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void SetValue(RenderTargetIdentifier source, Material material)
    {
        this.material = material; //接受面板材质
        this.source = source;
    }
}