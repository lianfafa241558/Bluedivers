
using System.Collections.Generic;
using UnityEngine;
using FPSGame.Core;
using FPSGame.Utils;
using FPSGame.GameContract;
using FPSGame.Game;
using FPSGame.GameData;

namespace FPSGame.Managers
{

/// <summary>
/// 欧帕兹属性、图标与预制体的查询管理。
/// </summary>

public class PropertyManager : Singleton<PropertyManager>
{
    [SerializeField]
    DisplayDic<OOPartEnum, Property> propertys;

    private DisplayDic<OOPartEnum, int> user => ArchivesData_SO.Current.propertys;

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

    /// <summary>按价目表扣资源。⚠ 不校验余额：调用方必须在弹确认框前判"够不够"（TipWnd 的 costs 已按 need/have 着色并隐藏买不起的按钮）。</summary>
    public void Pay(IList<SKVP<OOPartEnum, int>> costs)
    {
        if (costs == null) return;
        for (int i = 0; i < costs.Count; ++i)
        {
            SetCount(costs[i].Key, -costs[i].Value);
        }
    }

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
