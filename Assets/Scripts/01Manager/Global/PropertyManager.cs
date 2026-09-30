
using UnityEngine;
using System.Linq;
using FPSGame.Core;
using FPSGame.Utils;
using FPSGame.GameContract;

namespace FPSGame.Managers
{

/// <summary>
/// 欧帕兹属性、图标与预制体的查询管理。
/// </summary>
[AddComponentMenu("管理/欧帕兹属性管理")]
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


}
