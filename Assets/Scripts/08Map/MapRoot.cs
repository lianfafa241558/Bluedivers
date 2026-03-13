using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;
using UnityEngine.AI;
using Utils;

public class MapRoot : MonoBehaviour
{
    [Foldout("地形设置", true)]
    public Terrain terrain;
    [Range(0, 1)]
    public float EffectiveRange;
    [Header("里面的位置是指起点，不是指中心！")]
    public BoundsInt rect;
    public GameObject wallPrefab;
    public Material wallMat;
    [Foldout("点位", true)]
    public Transform unitRoot;

    public Transform cam;

    //public LayerMask airWall;
    private void Awake()
    {
        //Debug.LogError("Root初始化");
        InitTerrain();
        GenerateTerrain();
        CreatAirWall();
    }

    private void OnDestroy()
    {
        TerrainUtils.Main = null;
    }

    void OnDrawGizmosSelected()
    {
        //碰撞范围
        //Gizmos.color = Color.yellow;
        //Gizmos.DrawWireCube(rect.center, rect.size);
        Gizmos.color = Color.green;
        float range = rect.size.x / 2;
        for (int i = 0; i < 36; ++i)
        {
            Gizmos.DrawLine(
                rect.center + new Vector3(Mathf.Sin(Mathf.PI / 18 * i) * range, 0, Mathf.Cos(Mathf.PI / 18 * i) * range),
                rect.center + new Vector3(Mathf.Sin(Mathf.PI / 18 * (i + 1)) * range, 0, Mathf.Cos(Mathf.PI / 18 * (i + 1)) * range)
            );
        }

    }

    [ContextMenu("生成Rect")]
    public void GenerateTerrain()
    {
        var locpos = terrain.transform.position;
        /*
        if (Physics.Raycast(new(new(0, 50, 0), Vector3.down), out var hit, 100f))
        {
            locpos.y = 0;
        }*/
        locpos.y = -Constants.MapBorder / 2;
        var size = terrain.terrainData.size;
        rect = new((locpos+ Vector3.one* +Constants.MapBorder/2).ToInt(), new Vector3Int((int)(size.x-Constants.MapBorder), (int)size.y, (int)(size.z - Constants.MapBorder)));
        var sur = GetComponent<NavMeshSurface>();
        sur.size = size;
        sur.center = rect.center;


    }

    public void CreatAirWall()
    {
        float radius = rect.size.x / 2/* * EffectiveRange*/;
        float perimeter = radius * 3.1416f / 9f;//2Pi*r/36
        for (int i = 0; i < 360; i += 20)
        {
            var go = Instantiate(wallPrefab, terrain.transform).transform;
            go.gameObject.name = "airWall" + (i);
            go.position = rect.center + (Quaternion.Euler(0, i, 0) * Vector3.forward) * radius - (rect.center.y + 10) * Vector3.up;
            go.eulerAngles = new(0, i, 0);
            go.localScale = perimeter / 8.34f * Vector3.one;

        }

    }
    /*
    private void Update()
    {
        if (!cam)
        {
            if(Camera.main)cam = Camera.main.transform;
            return;
        }
        Vector4 camPos = cam.position;
        wallMat.SetVector("_Pos", camPos);
    }*/

    public float GroundHeight(Vector2 worldPos)
    {
        var mapPos = worldPos*(_heightMapResolution / _terrainSize.x);
        return _TerrainData.GetHeight((int)mapPos.x, (int)mapPos.y);//* _terrainSize.y;
    }

    #region 修改地形

    private TerrainData _TerrainData;
    private int _heightMapResolution;//地形分辨率
    private int _alphaMapResolution;//材质分辨率
    private Vector3 _terrainSize;//地形尺寸

    void InitTerrain()
    {
        TerrainUtils.Main = terrain;
        _TerrainData = terrain.terrainData;
        _heightMapResolution = _TerrainData.heightmapResolution;
        _alphaMapResolution = _TerrainData.alphamapResolution;
        _terrainSize = _TerrainData.size;
    }

    /// <summary> 创建弹坑 </summary>
    public void CreateCrater(Vector3 worldPosition, float innerRadius, float outerRadius, float depth,bool allowUp)
    {
        //Debug.LogError("在"+ worldPosition+"创建范围"+radius+"深度"+depth+"的弹坑");
        // 1. 坐标转换（使用缓存数据）
        Vector3 terrainLocalPos = worldPosition - Terrain.activeTerrain.transform.position;
        Vector2 normalizedPos = new Vector2(
            terrainLocalPos.x / _terrainSize.x,
            terrainLocalPos.z / _terrainSize.z
        );

        // 2. 计算影响区域（使用预计算分辨率）
        int heightMapOuterRadius = Mathf.CeilToInt(outerRadius / _terrainSize.x * _heightMapResolution);
        int heightMapInnerRadius = Mathf.CeilToInt(innerRadius / _terrainSize.x * _heightMapResolution);
        int alphaMapRadius = Mathf.CeilToInt(outerRadius / _terrainSize.x * _alphaMapResolution);

        float terrainHeight = _TerrainData.GetHeight((int)(normalizedPos.x* _heightMapResolution), (int)(normalizedPos.y * _heightMapResolution));
        //例如:爆炸中心点高12，地面高10，原深度5，半径10，实际深度就要5-2=3;
        
        float power = depth - (terrainLocalPos.y - terrainHeight);
        //radius = (power / depth) * radius;
        //如果不允许抬升,那小的就跳过
        if (!allowUp && power <= 0) return;
        TerrainUtils.ModifyHeightMap(normalizedPos, (int)innerRadius, (int)outerRadius, power, isSet:false);        

        // 3. 高度图修改（局部区域）
        //ModifyHeightMap(normalizedPos, heightMapInnerRadius, heightMapOuterRadius, power / _terrainSize.y, allowUp);

        // 4. AlphaMap修改（局部区域）
        //if(outerRadius>=1) ModifyAlphaMap(normalizedPos, alphaMapRadius);
    }
    /// <summary>修改高度图</summary>
    private void ModifyHeightMap(Vector2 normalizedPos, int innerRadius,int outerRadius, float depth,bool isSet)
    {
        int xBase = Mathf.Clamp((int)(normalizedPos.x * _heightMapResolution) - outerRadius, 0, _heightMapResolution);
        int yBase = Mathf.Clamp((int)(normalizedPos.y * _heightMapResolution) - outerRadius, 0, _heightMapResolution);
        int size = Mathf.Clamp(2 * outerRadius, 0, _heightMapResolution - Mathf.Max(xBase, yBase));

        float[,] heights = _TerrainData.GetHeights(xBase, yBase, size, size);
        float centerHeight = _TerrainData.GetHeight((int)(normalizedPos.x* _heightMapResolution), (int)(normalizedPos.y* _heightMapResolution)) ;
        centerHeight -= depth * _TerrainData.size.y;


        Vector2 center = new Vector2(outerRadius, outerRadius);
        float invRadius = 1f / outerRadius;//范围的倒数，让dis标准化
        float innerScale = innerRadius/(outerRadius+0f);//内半径的系数(比如0.8)
        Debug.LogError("内圈系数"+ innerScale+"内圈大小"+ innerRadius+"外圈大小"+ outerRadius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                //标准化之后[0,1]
                float distance = Vector2.Distance(new Vector2(x, y), center) * invRadius;

                if (distance <= 1f)
                {
                    //在外圈线性到1，内圈直接1
                    float power = Mathf.Clamp01((1-distance) / (1-innerScale));
                    if (isSet)
                    {
                        heights[y, x] = Mathf.Lerp(heights[y, x], centerHeight / _TerrainData.size.y, power);
                        //heights[y, x] = centerHeight / _TerrainData.size.y - depth;
                    }
                    else
                    {
                        heights[y, x] = Mathf.Max(0, heights[y, x] - depth * power / _TerrainData.size.y);
                    }
                    
                }
            }
        }
        _TerrainData.SetHeights(xBase, yBase, heights);
    }
    /// <summary>修改材质图</summary>
    private void ModifyAlphaMap(Vector2 normalizedPos, int radius)
    {
        int xBase = Mathf.Clamp((int)(normalizedPos.x * _alphaMapResolution) - radius, 0, _alphaMapResolution);
        int yBase = Mathf.Clamp((int)(normalizedPos.y * _alphaMapResolution) - radius, 0, _alphaMapResolution);
        int size = Mathf.Clamp(2 * radius, 0, _alphaMapResolution - Mathf.Max(xBase, yBase));

        float[,,] alphaMaps = _TerrainData.GetAlphamaps(xBase, yBase, size, size);

        Vector2 center = new Vector2(radius, radius);
        float invRadius = 1f / radius;
        float maxDistance = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float normalizedDistance = Mathf.Clamp01(distance / maxDistance);

                if (normalizedDistance <= 1f)
                {
                    // 使用平滑曲线计算权重
                    float targetWeight = 1 - Mathf.Pow(normalizedDistance, 2);

                    // 保留原始权重总和用于归一化
                    float originalSum = 0f;
                    for (int l = 0; l < alphaMaps.GetLength(2); l++)
                    {
                        if (l != 4)
                        {
                            originalSum += alphaMaps[y, x, l];
                        }
                    }

                    // 重新分配权重
                    for (int l = 0; l < alphaMaps.GetLength(2); l++)
                    {
                        if (l == 4)
                        {
                            alphaMaps[y, x, l] = targetWeight;
                        }
                        else
                        {
                            alphaMaps[y, x, l] *= (1 - targetWeight) / originalSum;
                        }
                    }
                }
            }
        }
        _TerrainData.SetAlphamaps(xBase, yBase, alphaMaps);
    }

    #endregion


}
