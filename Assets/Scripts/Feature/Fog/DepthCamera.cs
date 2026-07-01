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
        // 设置深度模式，这样Shader就可以通过_CameraDepthTexture读取深度纹理
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
            Graphics.Blit(temp, (RenderTexture)null); // 将处理后的结果渲染到屏幕上

            RenderTexture.ReleaseTemporary(temp);


            camera.targetTexture = x;
        }
    }
}
