using Unity.BaseTool;
using UnityEngine;

[CreateAssetMenu(fileName = "new Data", menuName = "Data/地图")]
public class MapData_SO : ScriptableObject
{

    public MapItemInfo[] mapItemInfos;
    [TextArea]
    public string AreaDesc;
    public Sprite AreaBackground;
    [CustomLabel("特产")]
    public OOPartEnum[] product;

    
    [System.Serializable]
    public struct MapItemInfo
    {
        public string name;
        public Vector2Int pos;
        public bool noTask;
    }

    /*
    public static MapInfo source;

    [ContextMenu("拷贝")]
    void _Copy()
    {
        mapItemInfos = source.mapItemInfos;
        AreaDesc = source.AreaDesc;
        AreaBackground = source.AreaBackground;
        product = source.product;
        string path = AssetDatabase.GetAssetPath(this);
        AssetDatabase.RenameAsset(path, "MD_" + source.name);

        SerializedObject serializedAsset = new SerializedObject(this);
        serializedAsset.FindProperty("m_Name").stringValue = "MD_" + source.name;
        serializedAsset.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
    }
    */

}
