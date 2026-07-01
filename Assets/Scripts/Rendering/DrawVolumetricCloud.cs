using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[ExecuteInEditMode]
public class DrawVolumetricCloud : MonoBehaviour
{
    [Header("云层配置")]
    public int horizontalStackSize = 20;
    public float cloudHeight = 5f;

    [Header("渲染资源")]
    public Mesh quadMesh;
    public Material cloudMaterial;

    [Header("天空盒颜色匹配")]
    public bool autoSampleSkyColor = true;

    [Header("性能设置")]
    public bool useGpuInstancing = true;
    public bool castShadows = false;
    public int layer = 0;

    private Matrix4x4[] matrices;

    void Update()
    {
        var targetCamera = Camera.main;

        cloudMaterial.SetFloat("_midYValue", transform.position.y);
        cloudMaterial.SetFloat("_cloudHeight", cloudHeight);

        if (autoSampleSkyColor && RenderSettings.skybox != null)
        {
            Material skyMat = RenderSettings.skybox;
            Texture dayTex = skyMat.GetTexture("_DayTex");
            Texture nightTex = skyMat.GetTexture("_NightTex");
            if (dayTex != null && nightTex != null)
            {
                cloudMaterial.SetTexture("_CloudDayTex", dayTex);
                cloudMaterial.SetTexture("_CloudNightTex", nightTex);
                cloudMaterial.SetFloat("_CloudLerp", skyMat.GetFloat("_Lerp"));
                cloudMaterial.SetColor("_CloudTintColor", skyMat.GetColor("_TintColor"));
                cloudMaterial.SetFloat("_CloudExposure", skyMat.GetFloat("_Exposure"));
            }
        }

        float layerSpacing = cloudHeight / (horizontalStackSize - 1);
        Vector3 startPosition = transform.position - Vector3.up * (cloudHeight / 2f);

        if (useGpuInstancing)
            matrices = new Matrix4x4[horizontalStackSize];

        for (int i = 0; i < horizontalStackSize; i++)
        {
            Vector3 pos = startPosition + Vector3.up * (layerSpacing * i);
            var matrix = Matrix4x4.TRS(pos, transform.rotation, transform.localScale);

            if (useGpuInstancing)
                matrices[i] = matrix;
            else
                Graphics.DrawMesh(quadMesh, matrix, cloudMaterial, layer, targetCamera, 0, null, castShadows, false, false);
        }

        if (useGpuInstancing)
        {
            var shadowCasting = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            Graphics.DrawMeshInstanced(quadMesh, 0, cloudMaterial, matrices, horizontalStackSize, null, shadowCasting, false, layer, targetCamera);
        }
    }
}
