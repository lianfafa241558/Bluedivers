
using UnityEngine;
using System.Linq;
using Core;

public class PropertyManager : Singleton<PropertyManager>
{
    [SerializeField]
    DisplayDic<OOPartEnum, Property> propertys;

    private DisplayDic<OOPartEnum, int> user => ArchiveSvc.Archive.propertys;

    [System.Serializable]
    public struct Property
    {
        public string name;
        public GameObject prefab;
        public Sprite icon;
    }
    public Sprite GetIcon(OOPartEnum property) => propertys[property].icon;
    public string GetName(OOPartEnum property) => propertys[property].name;
    public GameObject GetPrefab(OOPartEnum property) => propertys[property].prefab;
    public int GetCount(OOPartEnum property) => user[property];
    public int SetCount(OOPartEnum property,int value) => user[property]+= value;

    public GameObject CreatOOPart()
    {
        if (RandomUtils.Bool(70))
        {
            return GetPrefab(TaskManager.Instance.nowTask.SpecialtyPropertys.RandomTake());
        }
        else
        {
            return GetPrefab(TaskManager.Instance.nowTask.OtherPropertys.RandomTake());
        }
    }

}

public enum OOPartEnum
{
    /// <summary>青辉石</summary>
    [InspectorName("青辉石")]Pyroxene,
    /// <summary>电池</summary>
    [InspectorName("电池")] Battery,
    /// <summary>埴轮</summary>
    [InspectorName("埴轮")] Crystal,
    /// <summary>十二面体</summary>
    [InspectorName("十二面体")] Dodecahedron,
    /// <summary>以太</summary>
    [InspectorName("以太")] Ether,
    /// <summary>透镜</summary>
    [InspectorName("透镜")] Glasses,
    /// <summary>圆盘</summary>
    [InspectorName("圆盘")] Pendant,
    /// <summary>手稿</summary>
    [InspectorName("手稿")] Voynich,
}

