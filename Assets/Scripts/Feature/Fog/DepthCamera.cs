using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class DepthCamera : MonoBehaviour
{
    public Material mat;
    public int width = 512;
    public int height = 512;
    private Camera cam;
    public RenderTexture x;


    private void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnBeginCameraRendering;

        cam = GetComponent<Camera>();
        // 设置模式后就可以在Shader中通过声明_CameraDepthTexture变量来访问它
        cam.depthTextureMode = DepthTextureMode.Depth;
        x = cam.targetTexture = new RenderTexture(width, height, 24);

    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnBeginCameraRendering;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (mat != null && camera == cam)
        {
            CommandBuffer cmd = new CommandBuffer();
            cmd.Blit(null, x, mat);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            RenderTexture temp = RenderTexture.GetTemporary(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.Default);
            Graphics.Blit(x, temp, mat); // 使用材质处理 targetTexture
            Graphics.Blit(temp, (RenderTexture)null); // 输出处理后的结果到屏幕或其他对象

            RenderTexture.ReleaseTemporary(temp);


            camera.targetTexture = x;
        }
    }
}
