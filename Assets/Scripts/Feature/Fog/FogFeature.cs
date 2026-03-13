using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Experimental.Rendering.Universal.RenderObjects;

public class FogFeature : ScriptableRendererFeature
{
    private FogPass myPass;
    public Material distanceMaterial;
    public RenderPassEvent renderPass;
    public LayerMask layerMask;

    //RenderObjectsPass
    //RenderObjects
    public override void Create()
    {
        myPass = new FogPass();
        myPass.renderPassEvent = renderPass;
        myPass.layerMask = layerMask;
        //过滤出位于不透明队列中且层级为layerMask的物体，
        myPass.filteringSettings = new FilteringSettings(RenderQueueRange.all, ~layerMask);


    }


    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        //这个逼东西是限制摄像机物体所在层级的
        /*
        if ((layerMask & (1 << renderingData.cameraData.camera.gameObject.layer))==0)
        {
            return; // 如果当前渲染的层级不在 excludedLayer 中，则跳过
        }*/

        myPass.SetValue(renderer.cameraColorTarget, distanceMaterial,renderingData.cameraData.camera);
        renderer.EnqueuePass(myPass);


    }
}

public class FogPass : ScriptableRenderPass
{
    private RenderTargetIdentifier source;
    private RenderTargetHandle tempTargetHandle;
    private Material material;
    private int fogColorId = Shader.PropertyToID(FogShaderName.FogColor);
    private int fogIntensityId = Shader.PropertyToID(FogShaderName.FogIntensity);
    private int fogDistanceId = Shader.PropertyToID(FogShaderName.FogDistance);

    public LayerMask layerMask;
    public FilteringSettings filteringSettings;
    public ShaderTagId shaderTagId = new ShaderTagId("UniversalForward");
    public Camera camera;

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var fogVolme = VolumeManager.instance.stack.GetComponent<FogVolme>();
        if (fogVolme.IsActive()) {
            
            //创建一个名为"FogCmd"的命令缓冲区
            CommandBuffer cmd = CommandBufferPool.Get("FogCmd");
            /*
            //排序规则
            var sortingSettings = new SortingSettings(camera);
            //渲染物体的设置
            DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
            camera.TryGetCullingParameters(out var cullingParameters);
            //进行剔除
            var cullingResults=context.Cull(ref cullingParameters);
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            */
            //CullingResults cullingResults = renderingData.cullResults;
            //DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, new SortingSettings(camera));



            var dec = renderingData.cameraData.cameraTargetDescriptor;
            dec.msaaSamples = 1;
            dec.depthBufferBits = 0;
            cmd.GetTemporaryRT(tempTargetHandle.id, dec);

            material.SetColor(fogColorId, fogVolme.fogColor.value);
            material.SetFloat(fogIntensityId, fogVolme.intensity.value);
            material.SetFloat(fogDistanceId, fogVolme.distance.value);

            
            cmd.Blit(source, tempTargetHandle.Identifier(), material);
            //context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            cmd.Blit(tempTargetHandle.Identifier(), source);

            context.ExecuteCommandBuffer(cmd);
            

            CommandBufferPool.Release(cmd);
            cmd.ReleaseTemporaryRT(tempTargetHandle.id);

            

        }
    }

    public FogPass()
    {
        //初始化一个临时渲染目标句柄（tempTargetHandle），并给该句柄命名为"tempfog"。
        tempTargetHandle.Init("tempfog");
    }

    public void SetValue(RenderTargetIdentifier source, Material material,Camera camera)
    {
        this.source = source;
        this.material = material;
        this.camera = camera;
    }
}


