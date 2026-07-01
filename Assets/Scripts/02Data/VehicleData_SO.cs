using System.Collections;
using System.Collections.Generic;

using UnityEngine;

[CreateAssetMenu(fileName = "new Data", menuName = "Data/载具配置")]
public class VehicleData_SO : ScriptableObject
{
    [InspectorName("名称")]
    public string vehicleName;
    [InspectorName("图标")]
    public Sprite icon;
    [TextArea(3,5)]
    [InspectorName("描述")]
    public string desc;

    [InspectorName("左武器")]
    public WeaponInfo[] weaponLefts;
    [InspectorName("右武器")]
    public WeaponInfo[] weaponRights;
    [InspectorName("贴图")]
    public TextureInfo[] Diffs;
    [InspectorName("纹理")]
    public TextureInfo[] Blends;


    [System.Serializable]
    public class WeaponInfo
    {
        public GameObject go;
        public string name;
        public Sprite icon;
        public Sprite battleIcon;
    }

    [System.Serializable]
    public class TextureInfo
    {
        public Texture texture;
        public string name;
        public Sprite icon;
    }
}
