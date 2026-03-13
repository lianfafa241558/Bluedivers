/*
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//已经集成到一个类里面了、

public class TerrainContourGenerator : MonoBehaviour
{
    [Header("地形参数")]
    public Terrain targetTerrain;
    public float contourInterval = 5f;
    int textureSize = 1024;

    [Header("等高线优化参数")]
    [Range(0.01f, 2f)] public float lineWidth = 0.5f;

    [Header("噪点过滤参数")]
    [Tooltip("邻域检测范围（越大过滤越彻底）")]
    [Range(1, 3)] public int noiseFilterRange = 1;
    [Tooltip("保留像素所需的最小邻域等高线数量（越大过滤越严格）")]
    [Range(1, 24)] public int minNeighborCount = 1;

    [Header("输出")]
    public RenderTexture contourRenderTexture;

    private void Start()
    {
        //GenerateContourTexture();
    }


    public void GenerateContourTexture()
    {

        // 1. 初始化byte数组（全程无bool转换）
        Texture2D contourTex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        contourTex.filterMode = FilterMode.Bilinear;
        int totalPixels = textureSize * textureSize;
        bool[] pixelBytes = new bool[totalPixels];

        // 2. 预缓存地形数据（减少重复计算）
        TerrainData terrainData = targetTerrain.terrainData;
        float terrainWidth = terrainData.size.x;
        float terrainHeight = terrainData.size.z;
        int sampleRange = Mathf.Max(1, Mathf.RoundToInt(lineWidth));
        float heightmapRes = terrainData.heightmapResolution - 1;
        float widthRatio = terrainWidth / textureSize;
        float heightRatio = terrainHeight / textureSize;
        float contourThreshold = contourInterval * lineWidth / textureSize * terrainWidth;

        // 3. 第一步：标记所有候选等高线像素
        for (int y = 0; y < textureSize; y++)
        {
            int yOffset = y * textureSize;
            float terrainZ = y * heightRatio;

            for (int x = 0; x < textureSize; x++)
            {
                int pixelIndex = yOffset + x;
                float terrainX = x * widthRatio;

                bool foundContour = false;
                for (int dy = -sampleRange; dy <= sampleRange && !foundContour; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= textureSize) continue;

                    for (int dx = -sampleRange; dx <= sampleRange && !foundContour; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= textureSize) continue;

                        float nxWorld = terrainX + dx * widthRatio;
                        float nzWorld = terrainZ + dy * heightRatio;

                        // 双线性插值采样高度
                        float heightmapX = nxWorld / terrainWidth * heightmapRes;
                        float heightmapZ = nzWorld / terrainHeight * heightmapRes;
                        float height = terrainData.GetHeight(Mathf.FloorToInt(heightmapX), Mathf.FloorToInt(heightmapZ));
                        // 判断是否为等高线
                        float distance = Mathf.Abs(height % contourInterval);
                        distance = Mathf.Min(distance, contourInterval - distance);
                        if (distance < contourThreshold)
                        {
                            pixelBytes[pixelIndex] = true;
                            foundContour = true;
                        }
                    }
                }
            }
        }

        Color black = new(0,0,0,0);
        // 4. 第二步：过滤孤立噪点（核心新增逻辑）
        // 创建临时数组存储过滤结果，避免边遍历边修改导致的错误
        Color[] filteredBytes = new Color[totalPixels];
        for (int y = 0; y < textureSize; y++)
        {
            int yOffset = y * textureSize;
            for (int x = 0; x < textureSize; x++)
            {
                int pixelIndex = yOffset + x;
                // 只处理标记为等高线的像素
                if (!pixelBytes[pixelIndex]) continue;

                // 统计邻域内的等高线像素数量
                int neighborCount = 0;
                for (int dy = -noiseFilterRange; dy <= noiseFilterRange; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= textureSize) continue;

                    for (int dx = -noiseFilterRange; dx <= noiseFilterRange; dx++)
                    {
                        // 跳过自身
                        if (dx == 0 && dy == 0) continue;

                        int nx = x + dx;
                        if (nx < 0 || nx >= textureSize) continue;

                        int neighborIndex = ny * textureSize + nx;
                        if (pixelBytes[neighborIndex])
                        {
                            neighborCount++;
                            filteredBytes[pixelIndex] = Color.white;
                            // 提前退出：达到阈值就不用继续统计
                            if (neighborCount >= minNeighborCount) break;
                        }
                    }
                    if (neighborCount >= minNeighborCount) break;
                }

                // 邻域数量不足，判定为孤立点，清除
                if (neighborCount < minNeighborCount)
                {
                    filteredBytes[pixelIndex] = black;
                }
            }
        }

        // 5. 写入纹理（使用过滤后的数组）

        //contourTex.LoadRawTextureData(filteredBytes);
        contourTex.SetPixels(filteredBytes);
        contourTex.Apply();
        /*
        // 6. 输出到RenderTexture
        if (contourRenderTexture == null || contourRenderTexture.width != textureSize || contourRenderTexture.height != textureSize)
        {
            if (contourRenderTexture != null) contourRenderTexture.Release();
            contourRenderTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
        }* /
        //contourRenderTexture.filterMode = FilterMode.Bilinear;
        Graphics.Blit(contourTex, contourRenderTexture);

        // 释放资源
        DestroyImmediate(contourTex);
    }

    / *
    private float SampleTerrainHeight(TerrainData terrainData, float worldX, float worldZ)
    {
        float finalHeight = terrainData.GetHeight(Mathf.FloorToInt(worldZ), Mathf.FloorToInt(worldX));
        return finalHeight;
    }
    
    /// <summary>
    /// 双线性插值采样地形高度（核心优化：提升高度采样精度，避免离散误差）
    /// </summary>
    private float SampleTerrainHeightBilinear(TerrainData terrainData, float worldX, float worldZ)
    {
        // 将世界坐标转换为地形本地坐标（0~heightmapResolution-1）
        float heightmapX = worldX / terrainData.size.x * (terrainData.heightmapResolution - 1);
        float heightmapZ = worldZ / terrainData.size.z * (terrainData.heightmapResolution - 1);

        // 获取整数部分和小数部分
        int x0 = Mathf.FloorToInt(heightmapX);
        int x1 = Mathf.Min(x0 + 1, terrainData.heightmapResolution - 1);
        int z0 = Mathf.FloorToInt(heightmapZ);
        int z1 = Mathf.Min(z0 + 1, terrainData.heightmapResolution - 1);

        // 计算插值权重
        float tx = heightmapX - x0;
        float tz = heightmapZ - z0;

        // 采样四个邻域点的高度
        float h00 = terrainData.GetHeight(z0, x0);
        float h01 = terrainData.GetHeight(z1, x0);
        float h10 = terrainData.GetHeight(z0, x1);
        float h11 = terrainData.GetHeight(z1, x1);

        // 双线性插值
        float h0 = Mathf.Lerp(h00, h10, tx);
        float h1 = Mathf.Lerp(h01, h11, tx);
        float finalHeight = Mathf.Lerp(h0, h1, tz);

        return finalHeight;
    }
    * /
    // 编辑器下的测试按钮
    [ContextMenu("生成等高线纹理")]
    private void GenerateContourTextureInEditor()
    {
        GenerateContourTexture();
    }
}*/