using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainAuxiliary : MonoBehaviour
{
   [ContextMenu("旋转")]
   private void Rotate()
    {
        Terrain targetTerrain = GetComponent<Terrain>();
        // 获取地形数据
        TerrainData terrainData = targetTerrain.terrainData;

        // 1. 旋转高度图
        RotateHeightmap180(terrainData);

        // 2. 旋转纹理图（splat map）
        RotateSplatmap180(terrainData);

        // 刷新地形
        targetTerrain.Flush();

    }

    /// <summary>
    /// 旋转高度图180度
    /// </summary>
    private void RotateHeightmap180(TerrainData terrainData)
    {
        int heightmapWidth = terrainData.heightmapResolution;
        int heightmapHeight = terrainData.heightmapResolution;

        // 获取原始高度数据
        float[,] heights = terrainData.GetHeights(0, 0, heightmapWidth, heightmapHeight);
        float[,] newHeights = new float[heightmapWidth, heightmapHeight];

        // 180度反转坐标
        for (int x = 0; x < heightmapWidth; x++)
        {
            for (int z = 0; z < heightmapHeight; z++)
            {
                int newX = heightmapWidth - 1 - x;
                int newZ = heightmapHeight - 1 - z;
                newHeights[newX, newZ] = heights[x, z];
            }
        }

        // 应用新高度数据
        terrainData.SetHeights(0, 0, newHeights);
    }

    /// <summary>
    /// 旋转纹理图180度
    /// </summary>
    private void RotateSplatmap180(TerrainData terrainData)
    {
        int alphamapWidth = terrainData.alphamapWidth;
        int alphamapHeight = terrainData.alphamapHeight;

        // 获取原始纹理数据
        float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
        float[,,] newSplatmapData = new float[alphamapWidth, alphamapHeight, splatmapData.GetLength(2)];

        // 180度反转坐标
        for (int x = 0; x < alphamapWidth; x++)
        {
            for (int z = 0; z < alphamapHeight; z++)
            {
                int newX = alphamapWidth - 1 - x;
                int newZ = alphamapHeight - 1 - z;

                for (int layer = 0; layer < splatmapData.GetLength(2); layer++)
                {
                    newSplatmapData[newX, newZ, layer] = splatmapData[x, z, layer];
                }
            }
        }

        // 应用新纹理数据
        terrainData.SetAlphamaps(0, 0, newSplatmapData);
    }





}
