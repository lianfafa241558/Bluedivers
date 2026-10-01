using System.Collections.Generic;
using FPSGame.Core;
//using FPSGame.MapUtils;
using FPSGame.Attributes;
using UnityEngine;
using FPSGame.GameContract;

namespace FPSGame.Data
{

[CreateAssetMenu(fileName = "new Data", menuName = "Data/地图")]
public class MapData_SO : ScriptableObject
{



    public string AreaName;
    [InspectorName("任务点")]
    public MapItemInfo[] mapItemInfos;


    [TextArea(4,10)]
    public string AreaDesc;


    [SpritePreview(8,4)]
    public Sprite AreaBackground;
    [SpritePreview]
    public Sprite Icon, Map;
    public Color color;


    [Divider]
    [Header("对象")]
    [InspectorName("敌对类型")]
    public EnemyVarietyType enemyVarietyType;

    [InspectorName("特产")]
    public OOPartEnum[] product;
    [InspectorName("次要资源")]
    public OOPartEnum[] otherProduct;

    [InspectorName("兴趣点")]
    public SKVP<GameObject, int>[] interestPoints;
    [InspectorName("场景点")]
    public SKVP<GameObject, int>[] scenePoints;
    [InspectorName("岩石原型")]
    public GameObject[] stonePrototypes;
    [InspectorName("树原型")]
    public GameObject[] treePrototypes;
    [InspectorName("细节原型")]
    public GameObject[] detailPrototypes;

    [Divider]
    [Header("地形")]
    [InspectorName("纹理配置")]
    public TerrainItemInfo[] TerrainItem;


    [Divider]
    [Header("天空盒")]

    [InspectorName("天空盒天顶色")]
    [Tooltip("地平线以上、越靠近正上方越接近本色")]
    public Gradient skyColor;
    [InspectorName("天空盒赤道色")]
    [Tooltip("地平线附近的一圈颜色（天空盒里 y=0 处上下两半球都取它）；一般比天顶色略亮或略灰，做出地平线霾感")]
    public Gradient equatorColor;
    public Gradient fogColor;


    [InspectorName("岩石生成倍率")]
    public float StoneSpawnMultiplier = 1f;

    [InspectorName("树生成倍率")]
    public float TreeSpawnMultiplier = 1f;

    [InspectorName("悬崖生成倍率")]
    public float RockCoverMultiplier = 1f;

    [InspectorName("细节生成倍率")]
    public float DetailSpawnMultiplier = 1f;


    [Divider]
    [Header("天气")]
    [InspectorName("天气概率")]
    [Tooltip("开局抽取天气的权重表（Value 为权重，越大越容易出现）；留空时默认晴天")]
    public List<SKVP<WeatherType, int>> WeatherInfos;



    [System.Serializable]
    [Singleline]
    public struct MapItemInfo
    {
        [InspectorName("名称")]
        public string name;
        [InspectorName("坐标")]
        public Vector2Int pos;
        [InspectorName("敌对")]
        public EnemyVarietyType enemyVarietyType;
        [InspectorName("地形")]
        public TerrainType terrainType;
    }





}




[System.Serializable]
[Singleline]
public struct TerrainItemInfo
{
    [InspectorName("纹理")]
    public Texture diffuseTexture;
    [InspectorName("大小")]
    public Vector2 tileSize;
}
}
