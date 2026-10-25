using UnityEngine;

public static class TerrainDataOps
{
    // ================= 通用边界映射 =================

    private static int MapIndex(int v, int size, bool wrap)
    {
        if (wrap)
        {
            v %= size;
            if (v < 0) v += size;
            return v;
        }
        return (v >= 0 && v < size) ? v : -1;
    }

    // ================= 高度图 =================
    // GetHeights 返回 float[height, width]，注意是 [y, x]！

    public static void FlipHorizontal_Height(TerrainData data)
    {
        int w = data.heightmapResolution;
        int h = data.heightmapResolution;
        var src = data.GetHeights(0, 0, w, h);
        var dst = new float[h, w];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dst[y, w - 1 - x] = src[y, x];

        data.SetHeights(0, 0, dst);
    }

    public static void FlipVertical_Height(TerrainData data)
    {
        int w = data.heightmapResolution;
        int h = data.heightmapResolution;
        var src = data.GetHeights(0, 0, w, h);
        var dst = new float[h, w];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dst[h - 1 - y, x] = src[y, x];

        data.SetHeights(0, 0, dst);
    }

    public static void MoveHeight(TerrainData data, int dx, int dy, bool wrap)
    {
        int w = data.heightmapResolution;
        int h = data.heightmapResolution;
        var src = data.GetHeights(0, 0, w, h);
        var dst = new float[h, w]; // 默认 0

        int[] mapX = new int[w];
        int[] mapY = new int[h];
        for (int x = 0; x < w; x++) mapX[x] = MapIndex(x + dx, w, wrap);
        for (int y = 0; y < h; y++) mapY[y] = MapIndex(y + dy, h, wrap);

        for (int y = 0; y < h; y++)
        {
            int ty = mapY[y];
            if (ty < 0) continue;
            for (int x = 0; x < w; x++)
            {
                int tx = mapX[x];
                if (tx < 0) continue;
                dst[ty, tx] = src[y, x];
            }
        }

        data.SetHeights(0, 0, dst);
    }

    // ================= Alphamap（纹理混合） =================
    // GetAlphamaps 返回 float[width, height, layers]，是 [x, y, layer]！

    public static void FlipHorizontal_Alpha(TerrainData data)
    {
        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        int layers = data.alphamapLayers;
        var src = data.GetAlphamaps(0, 0, w, h);
        var dst = new float[w, h, layers];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                for (int l = 0; l < layers; l++)
                    dst[w - 1 - x, y, l] = src[x, y, l];

        data.SetAlphamaps(0, 0, dst);
    }

    public static void FlipVertical_Alpha(TerrainData data)
    {
        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        int layers = data.alphamapLayers;
        var src = data.GetAlphamaps(0, 0, w, h);
        var dst = new float[w, h, layers];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                for (int l = 0; l < layers; l++)
                    dst[x, h - 1 - y, l] = src[x, y, l];

        data.SetAlphamaps(0, 0, dst);
    }

    public static void MoveAlpha(TerrainData data, int dx, int dy, bool wrap)
    {
        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        int layers = data.alphamapLayers;
        var src = data.GetAlphamaps(0, 0, w, h);
        var dst = new float[w, h, layers]; // 默认 0

        int[] mapX = new int[w];
        int[] mapY = new int[h];
        for (int x = 0; x < w; x++) mapX[x] = MapIndex(x + dx, w, wrap);
        for (int y = 0; y < h; y++) mapY[y] = MapIndex(y + dy, h, wrap);

        for (int y = 0; y < h; y++)
        {
            int ty = mapY[y];
            if (ty < 0) continue;
            for (int x = 0; x < w; x++)
            {
                int tx = mapX[x];
                if (tx < 0) continue;
                for (int l = 0; l < layers; l++)
                    dst[tx, ty, l] = src[x, y, l];
            }
        }

        data.SetAlphamaps(0, 0, dst);
    }
}