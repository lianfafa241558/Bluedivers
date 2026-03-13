using Unity.BaseTool;
using UnityEngine;
using Utils;
public static partial class TerrainUtils
{
    /// <summary>
    /// 获取世界坐标在地形上的相对坐标
    /// </summary>
    private static Vector2 WSToTS(this Terrain terrain, Vector3 pos)
        => (pos - terrain.GetPosition()).ToVector2();
    private static Vector2 WSToTS(this Terrain terrain, Vector2 pos) 
        => pos - terrain.GetPosition().ToVector2();

    /// <summary>
    /// 获取世界坐标对应的实际高度
    /// </summary>
    public static float WSToHeight(this Terrain terrain, Vector3 pos) 
        => terrain.SampleHeight(pos)+terrain.GetPosition().y;
    public static float WSToHeight(this Terrain terrain,Vector2 pos) 
        => terrain.SampleHeight(pos.ToVector3()) + terrain.GetPosition().y;

    /// <summary>
    /// 将世界坐标(米)变为UV[0,1]
    /// </summary>
    public static Vector2 WSToUV(this Terrain terrain, Vector3 pos) 
        => terrain.WSToTS(pos) / terrain.terrainData.size.x;
    public static Vector2 WSToUV(this Terrain terrain, Vector2 pos) 
        => terrain.WSToTS(pos) / terrain.terrainData.size.x;
  

    /// <summary>
    /// 将世界坐标(米)变为高度贴图位置(像素)
    /// </summary>
    public static Vector2Int WSToHS(Vector3 pos, Terrain terrain) 
        =>(terrain.WSToUV(pos)* (terrain.terrainData.heightmapResolution - 1)).ToInt();



    /// <summary>
    /// 将世界坐标(米)变为高度贴图位置(像素)
    /// </summary>
    public static Vector2Int WSToHS(this Terrain terrain,Vector2 pos)
        => (terrain.WSToUV(pos) * (terrain.terrainData.heightmapResolution - 1)).ToInt();

    public static Vector2Int WSToHS(this Terrain terrain, Vector3 pos)
        => (terrain.WSToUV(pos) * (terrain.terrainData.heightmapResolution - 1)).ToInt();


    /// <summary>
    /// 将世界坐标(米)变为纹理贴图位置(像素)
    /// </summary>
    public static Vector2Int WSToAS(this Terrain terrain, Vector2 pos)
        => (terrain.WSToUV(pos) * terrain.terrainData.alphamapResolution).ToInt();

    public static Vector2Int WSToAS(this Terrain terrain, Vector3 pos)
        => (terrain.WSToUV(pos) * terrain.terrainData.alphamapResolution).ToInt();


    /// <summary>
    /// 将世界坐标(米)变为高度贴图长度(像素)
    /// </summary>
    public static int WRToHR(this Terrain terrain, float lenght) 
        => Mathf.FloorToInt(lenght / terrain.terrainData.size.x * (terrain.terrainData.heightmapResolution-1));

    /// <summary>
    /// 将世界长度(米)变为纹理贴图长度(像素)
    /// </summary>
    public static int WRToAR(this Terrain terrain, float lenght) 
        => Mathf.FloorToInt(lenght / terrain.terrainData.size.x * terrain.terrainData.alphamapResolution);

    /// <summary>
    /// 将纹理贴图长度(像素)变为高度贴图长度(像素)
    /// </summary>
    public static int ARToHR(this Terrain terrain, float lenght) 
        => Mathf.FloorToInt(lenght / terrain.terrainData.alphamapResolution * (terrain.terrainData.heightmapResolution - 1));

    /// <summary>
    /// 高度像素点对应的世界坐标
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Vector2 HSToWS(this Terrain terrain, int x, int y) => new Vector2(x, y) / (terrain.terrainData.heightmapResolution - 1) * terrain.terrainData.size.x+terrain.GetPosition().ToVector2();

    /// <summary>
    /// 纹理像素点对应的世界坐标
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Vector2 ASToWS(this Terrain terrain, int x, int y) => new Vector2(x, y) / terrain.terrainData.alphamapResolution * terrain.terrainData.size.x + terrain.GetPosition().ToVector2();

}
