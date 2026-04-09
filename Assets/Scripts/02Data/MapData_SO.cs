using Core;
using Unity.BaseTool;
using UnityEngine;

[CreateAssetMenu(fileName = "new Data", menuName = "Data/地图")]
public class MapData_SO : ScriptableObject
{



    public string AreaName;
    public MapItemInfo[] mapItemInfos;
    [TextArea(4,10)]
    public string AreaDesc;
    public Sprite AreaBackground;
    public Sprite Icon, Map;
    [CustomLabel("特产")]
    public OOPartEnum[] product;

    [CustomLabel("敌对类型")]
    public EnemyVarietyType enemyVarietyType;
    [Header("兴趣点")]
    public KVP<GameObject, int>[] interestPoints;

    [System.Serializable]
    public struct MapItemInfo
    {
        public string name;
        public Vector2Int pos;
        public bool noTask;
    }

    /*
    public static TaskManager._MapCfg source;

    [ContextMenu("拷贝")]
    void _Copy()
    {
        // 1. 复制基础字符串字段
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

        // 6. 复制兴趣点数组 KVP
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
