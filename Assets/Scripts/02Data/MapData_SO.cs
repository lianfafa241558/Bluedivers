using System.Collections.Generic;
using Core;
using FpsGame.MapUtils;
using FPSGame.Attribute;
using UnityEngine;

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


    [Divider]
    [Header("地形")]
    [InspectorName("纹理配置")]
    public TerrainItemInfo[] TerrainItem;
    public Gradient fogColor;

    [InspectorName("树生成倍率")]
    [Tooltip("乘在地形树的生成密度上（1=用地形预设值，0=本图不长树，2=密度翻倍）")]
    public float TreeSpawnMultiplier = 1f;

    [InspectorName("悬崖生成倍率")]
    [Tooltip("乘在巨型悬崖（地形覆盖石）的生成数量上（1=用地形预设值，0=本图不放悬崖）")]
    public float RockCoverMultiplier = 1f;


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



    /*
    public static TaskManager._MapCfg source;

    [ContextMenu("拷贝")]
    void _Copy()
    {
        // 1. 复制基础字符串字
        AreaName = source.MapName;
        AreaDesc = source.AreaDesc;

        // 2. 复制 Sprite 资源
        AreaBackground = source.AreaBackground;
        Icon = source.Icon;
        Map = source.Map;

        // 3. 复制枚举数组（特产）
        product = source.product;

        // 4. 复制敌对类型
        enemyVarietyType = source.enemyVarietyType;


        mapItemInfos = new MapItemInfo[source.mapItemInfos.Length];
        for (int i = 0; i < source.mapItemInfos.Length; ++i)
        {
            mapItemInfos[i] = new MapItemInfo() {
                name = source.mapItemInfos[i].name,
                noTask = source.mapItemInfos[i].noTask,
                pos = source.mapItemInfos[i].pos,
            };
        }

        // 6. 复制兴趣点数 KVP
        interestPoints = new KVP<GameObject, int>[source.interestPoints.Length];
        for (int i = 0; i < source.interestPoints.Length; i++)
        {
            interestPoints[i] = new KVP<GameObject, int>(source.interestPoints[i].Key, source.interestPoints[i].Value);
        }

        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        UnityEditor.AssetDatabase.RenameAsset(path, "MD_" + source.MapName);

        UnityEditor.SerializedObject serializedAsset = new(this);
        serializedAsset.FindProperty("m_Name").stringValue = "MD_" + source.MapName;
        serializedAsset.ApplyModifiedProperties();

        UnityEditor.AssetDatabase.SaveAssets();
    }
    */

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
