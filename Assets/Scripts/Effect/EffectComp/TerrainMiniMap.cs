using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//这种方案巨卡无比效果还不好
/*
public class TerrainMiniMap : MonoBehaviour
{
    [Header("渲染设置")]
    public int renderTextureSize = 512;
    public FilterMode filterMode = FilterMode.Bilinear;
    public TextureWrapMode wrapMode = TextureWrapMode.Clamp;

    [Header("地形设置")]
    public Terrain terrain;

    [Header("输出")]
    public RenderTexture outputRenderTexture;

    private TerrainData terrainData;
    private TerrainLayer[] terrainLayers;
    private int layerCount;
    private float[,,] alphaMaps;

    void Start()
    {
        //InitializeTerrainData();
        //GenerateMiniMap();
    }

    void InitializeTerrainData()
    {
        if (terrain == null)
            terrain = GetComponent<Terrain>();

        terrainData = terrain.terrainData;
        terrainLayers = terrainData.terrainLayers;
        layerCount = terrainLayers.Length;

        // 获取Alpha贴图数据
        alphaMaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
    }

    public void GenerateMiniMap()
    {
        if (outputRenderTexture == null ||
            outputRenderTexture.width != renderTextureSize ||
            outputRenderTexture.height != renderTextureSize)
        {
            // 创建新的RenderTexture
            if (outputRenderTexture != null)
                DestroyImmediate(outputRenderTexture);

            outputRenderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 0, RenderTextureFormat.ARGB32);
            outputRenderTexture.filterMode = filterMode;
            outputRenderTexture.wrapMode = wrapMode;
            outputRenderTexture.Create();
        }

        // 创建临时纹理用于渲染
        Texture2D tempTexture = new Texture2D(renderTextureSize, renderTextureSize, TextureFormat.RGBA32, false);
        tempTexture.filterMode = filterMode;
        tempTexture.wrapMode = wrapMode;

        // 执行纹理混合
        BlendTerrainTextures(tempTexture);

        // 将纹理渲染到RenderTexture
        RenderTexture.active = outputRenderTexture;
        Graphics.Blit(tempTexture, outputRenderTexture);
        RenderTexture.active = null;

        // 清理临时纹理
        DestroyImmediate(tempTexture);
    }

    void BlendTerrainTextures(Texture2D targetTexture)
    {
        int width = targetTexture.width;
        int height = targetTexture.height;
        Color[] colors = new Color[width * height];

        // 计算缩放因子
        float scaleX = (float)alphaMaps.GetLength(1) / width;
        float scaleY = (float)alphaMaps.GetLength(0) / height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 计算在Alpha贴图中的对应位置
                int alphaX = Mathf.Min(Mathf.FloorToInt(x * scaleX), alphaMaps.GetLength(1) - 1);
                int alphaY = Mathf.Min(Mathf.FloorToInt(y * scaleY), alphaMaps.GetLength(0) - 1);

                // 获取混合权重
                float[] weights = new float[layerCount];
                for (int i = 0; i < layerCount; i++)
                {
                    weights[i] = alphaMaps[alphaY, alphaX, i];
                }

                // 采样并混合颜色
                Color finalColor = Color.black;
                for (int i = 0; i < layerCount; i++)
                {
                    if (terrainLayers[i] != null && terrainLayers[i].diffuseTexture != null)
                    {
                        // 计算UV坐标
                        float u = (float)x / width;
                        float v = (float)y / height;

                        // 采样纹理颜色
                        Color texColor = terrainLayers[i].diffuseTexture.GetPixelBilinear(u * terrainLayers[i].diffuseTexture.width, v * terrainLayers[i].diffuseTexture.height);
                        finalColor += texColor * weights[i];
                    }
                }

                colors[y * width + x] = finalColor;
            }
        }

        targetTexture.SetPixels(colors);
        targetTexture.Apply();
    }

    void OnDestroy()
    {
        if (outputRenderTexture != null)
        {
            outputRenderTexture.Release();
            DestroyImmediate(outputRenderTexture);
        }
    }

    // 编辑器下的测试按钮
    [ContextMenu("生成小地图")]
    private void GenerateContourTextureInEditor()
    {
        Start();
    }
}*/