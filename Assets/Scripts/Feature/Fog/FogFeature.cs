using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Experimental.Rendering.Universal.RenderObjects;

/// <summary>
/// 雾效渲染器特性：挂载在 URP Renderer 上注入 FogPass，配合 FogVolme Volume 组件实现屏幕空间距离雾
/// </summary>
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
        //排除位于不透明白名单且层级为layerMask的物体
        myPass.filteringSettings = new FilteringSettings(RenderQueueRange.all, ~layerMask);


    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        //检查当前相机的层级是否在排除层级中
        /*
        if ((layerMask & (1 << renderingData.cameraData.camera.gameObject.layer))==0)
        {
            return; // 如果当前渲染的层级在 excludedLayer 中，则跳过
        }*/

        myPass.SetValue(distanceMaterial,renderingData.cameraData.camera);
        renderer.EnqueuePass(myPass);


    }
}

/// <summary>
/// 雾效渲染 Pass：每帧检查 Volume 栈中 FogVolme 是否激活，将其雾色/浓度/距离参数写入指定材质，
/// 再用该材质对相机颜色缓冲做一次全屏 Blit 混合，实现屏幕空间雾后处理
/// </summary>
public class FogPass : ScriptableRenderPass
{
    // 使用 RTHandle 替代 RenderTargetHandle
    private RTHandle tempTargetHandle;
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
        if (fogVolme.IsActive())
        {
            CommandBuffer cmd = CommandBufferPool.Get("FogCmd");

            var dec = renderingData.cameraData.cameraTargetDescriptor;
            dec.msaaSamples = 1;
            dec.depthBufferBits = 0;

            // 使用 RenderingUtils.ReAllocateIfNeeded 创建临时 RTHandle
            RenderingUtils.ReAllocateIfNeeded(ref tempTargetHandle, dec, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "tempfog");

            material.SetColor(fogColorId, fogVolme.fogColor.value);
            material.SetFloat(fogIntensityId, fogVolme.intensity.value);
            material.SetFloat(fogDistanceId, fogVolme.distance.value);

            // 使用 RTHandle 替代 nameID，直接使用 RTHandle
            // 注意：需要获取当前相机的颜色目标
            RTHandle cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

            cmd.Blit(cameraColorTarget, tempTargetHandle, material);
            cmd.Blit(tempTargetHandle, cameraColorTarget);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    public FogPass()
    {
        // 不需要额外初始化，在 Execute 中动态设置
    }


    public void SetValue(Material material, Camera camera)
    {
        this.material = material;
        this.camera = camera;
    }
}

