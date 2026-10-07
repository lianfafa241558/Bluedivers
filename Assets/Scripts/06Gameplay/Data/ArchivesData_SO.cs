using System;
using System.Collections.Generic;
using System.Globalization;
using FPSGame.Core;
using FPSGame.Game;

using UnityEngine;
using FPSGame.Utils;
using FPSGame.GameContract;
using FPSGame.Gameplay;

namespace FPSGame.GameData
{

[CreateAssetMenu(fileName = "new Data", menuName = "Data/存档")]
/// <summary>
/// 存档信息
/// </summary>
public class ArchivesData_SO : ArchivesDataBase_SO
{

    public override string Path() => "KivotosCraftArc.json";

    /// <summary>
    /// 当前存档实例（**数据自持**，2026-09-30 为 P5 加入）。
    ///
    /// <para>▍为什么需要它：玩法层要读 <c>GetRoleCfg(...)</c> / <c>.weaponUpgradeDic</c> /
    /// <c>.VehicleCustomDic</c>，而存档的**持有者** <c>ArchiveLoader</c> 在 01Manager
    /// ⇒ 进 asmdef 后玩法层看不见它。于是把 SO 引用留在**数据自己**身上
    /// （与 <c>AboStateData_SO.Dic</c> 完全同一套路）：上层 <c>ArchiveLoader.Init()</c> 写入，
    /// 全项目直接读 <c>ArchivesData_SO.Current</c>。</para>
    /// </summary>
    public static ArchivesData_SO Current { get; set; }

    #region 属性
    [Header("基础")]

    [Header("角色")]
    public string lastSelectRole;


    #endregion


    #region 角色信息
    [Space]
    [InspectorName("角色信息")]
    public DisplayDic<string, ArchRoleData> roleDataDic = new(true,(id) => {
        return new() {
            ID = id,
            Level = 1,
            Exp = 0,
            weaponSelect = new(true,new List<KVP<WeaponTypeEnum,int>>() {
                new(WeaponTypeEnum.Primary,0),
                new(WeaponTypeEnum.Secondary,0),
                new(WeaponTypeEnum.Special,0),
                new(WeaponTypeEnum.Grenade,0),
                new(WeaponTypeEnum.FlareGun,0),
                new(WeaponTypeEnum.Armor,0),
            })
        };
    });
    public void SetRoleLevel(string ID, int level, int exp)
    {
        if (exp >= 1000)
        {
            level += exp / 1000;
            exp %= 1000;
        }

       var data= roleDataDic[ID];
        data.Level=level;
        data.Exp= exp;
    }

    public void GetRoleLevel(string ID, out int level, out float expScale)
    {
        var data = roleDataDic[ID];

        level = data.Level;
        expScale = data.Exp/1000f;
    }

    public ArchRoleData GetRoleCfg(string ID) {
        return roleDataDic[ID];
    }
    public int[] GetWeaponSelect(string ID)
    {
        return roleDataDic[ID].weaponSelect.Values;
    }

    public void GainRoleExp(string ID,int gainValue, out int level, out float expScale)
    {
        var data= roleDataDic[ID];
        data.Exp += gainValue;
        if (data.Exp / 1000 > 0)
        {
            data.Level += data.Exp / 1000;
            data.Exp %= 1000;
        }

        level = data.Level;
        expScale = data.Exp/1000f;

        GlobalEventSub.OnGainExp?.Invoke(ID, level, expScale);
    }

    #endregion

    #region 地图势力信息
    [Space]
    [InspectorName("势力信息")]
    public DisplayDic<string, List<ArchOccupierData>> occupierDic = new();



    #endregion

    #region 武器改装
    [Space]
    [InspectorName("武器改装")]
    public DisplayDic<string, WeaponUpgradeData> weaponUpgradeDic = new();

    public int[][] GetWeaponUpgrade(string ID)
    {
        var re = new int[6][];
        var role = roleDataDic[ID];
        //Debug.LogError("查询"+ "GameData/Role/RD_" + ID+" 返回"+ Resources.Load<RoleData_SO>("GameData/Role/RD_" + ID));
        var data = Resources.Load<RoleData_SO>("GameData/Role/RD_"+ ID).weapons;
        for(int i = 0; i < 6; ++i)
        {
            //如果超出范围自动回滚
            var weapon = data[(WeaponTypeEnum)i][role.weaponSelect[(WeaponTypeEnum)i]%data[(WeaponTypeEnum)i].Count];
            re[i]=weaponUpgradeDic.TryGet(ID + "_" + weapon.WeaponName,new(ID + "_" + weapon.WeaponName, weapon.UpgradeCount().Length)).selectIndex;
        }
        return re;
    }
    #endregion

    #region 载具改装

    [Space]
    [InspectorName("载具改装")]
    public DisplayDic<string, ArchVehicleData> VehicleCustomDic = new();


    #endregion

    #region 资源
    [Space]
    [InspectorName("资源和道具")]
    [SerializeField]
    public DisplayDic<OOPartEnum, int> propertys = new();

    #endregion

    #region 空投
    [Space]
    [Header("已购买的空投")]
    public List<int> AirdropBuyDic = new();

    [Header("战备偏好")]
    [InspectorName("战备偏好列表")]
    public List<int> AirdropPreferList = new();

    /// <summary>该战备是否已购买</summary>
    public bool IsAirdropBought(int id) => AirdropBuyDic.Contains(id);

    /// <summary>购买战备（去重），返回是否新增</summary>
    public bool BuyAirdrop(int id)
    {
        if (AirdropBuyDic.Contains(id)) return false;
        AirdropBuyDic.Add(id);
        return true;
    }

    /// <summary>该战备是否被偏好</summary>
    public bool IsAirdropPrefer(int id) => AirdropPreferList.Contains(id);

    /// <summary>切换战备偏好，返回切换后是否处于偏好状态</summary>
    public bool ToggleAirdropPrefer(int id)
    {
        if (AirdropPreferList.Remove(id)) return false;
        AirdropPreferList.Add(id);
        return true;
    }

    #endregion
    #region 设置
    [Space]
    [InspectorName("设置")]
    public DisplayDic<string, ArchSettingData> settingDic;

    /// <summary>
    /// 按名字读一项设置（返回 float）。
    ///
    /// <para>▍这是"数据自带查询方法"，也是**全项目读取设置的唯一入口**（2026-10-01 起）：
    /// 玩法层 / 表现层 / UI 一律写 <c>ArchivesData_SO.Current.GetSetting(name)</c>。
    /// 同批删除了 <c>ServiceLocator.Archive</c> 槽、<c>IArchiveService</c> 契约、
    /// <c>ArchiveSvc.GetSetting</c> 转发方法、以及 <c>ArchiveLoader.Archive</c> 别名（27 处调用已改直读）
    /// —— 就是为了不留"第二个入口"。</para>
    /// <para>▍与 <c>AboStateData_SO.Dic</c>、<c>MissionData_SO.Catalog</c> 是同一套路（数据自持）：
    /// 由 <c>ArchiveLoader.Init()</c> 写入 <see cref="Current"/>，全项目直接读数据自身。</para>
    /// </summary>
    public float GetSetting(string name)
    {
        // ⚠ 不要写成 settingDic[name]：DisplayDic 的兜底分支会返回 DefaultValue（其 ArchivesFloat.value 是空串），
        //   并把这份"占位条目"写进字典；之后 Synchronize 会认为该键已存在而拒绝用默认资产补齐，
        //   存档就永久停在缺项状态（打包版曾因此每次启动崩两次）。
        //   这里改用 TryGet：缺键时不产生副作用，并直接报错暴露出来（键应在 ArchiveLoader.Init() 补齐）。
        if (settingDic.TryGet(name, out var item))
        {
            return item.value.RawFloat;
        }

        Debug.LogError($"[存档] 缺少设置项「{name}」，已按 0 处理。请检查 GameData/Archive_Default 资产与该存档的 settingDic。");
        return 0f;
    }
    #endregion




    #region IO


    public void Save()
    {
        SaveFile();
    }

    protected override void InLoad()
    {
        Debug.LogWarning("加载了存档"+name+"数据数量"+roleDataDic.Count);
    }

    //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    //private static void InitRef2() {
    //    InitRef();
    //}



    #endregion
    [System.Serializable]
    public class ArchRoleData
    {
        public string ID;
        public int Level;
        public int Exp;

        public DisplayDic<WeaponTypeEnum, int> weaponSelect;
    }

    [System.Serializable]
    public class ArchOccupierData
    {
        public string name;
        public int value;
    }

    [System.Serializable]
    public class WeaponUpgradeData
    {
        public string ID;
        public int[] selectIndex;
        [SerializeField]
        private int[] buyIndex;

        public int selectModuleIndex;
        [SerializeField]
        private int[] buyModuleIndex;

        public WeaponUpgradeData(string id, int lenght)
        {
            ID = id;
            selectIndex = new int[lenght];
            buyIndex = new int[lenght];
            for (int i = 0; i < lenght; ++i)
            {
                selectIndex[i] = -1;
                buyIndex[i] = 0;
            }
        }
        public bool GetBuy(int y, int x)
        {
            if (y > buyIndex.Length) return false;
            if (x > 2) return false;
            return (buyIndex[y] & (1 << x)) > 0;
        }
        public int BuyCount
        {
            get{
                int count = 0;
                for (int i=0;i<buyIndex.Length;++i)
                {
                    count += buyIndex[i].CountOnes();
                }
                return count;
            }
        }
        public void SetBuy(int y, int x)
        {
            buyIndex[y] |= (1 << x);
        }
        

    }

    [System.Serializable]
    public class ArchVehicleData
    {
        public int leftWeaponIndex;
        public int rightWeaponIndex;
        public int skinIndex;
        public int blendIndex;
        public ArchivesFloat blendScale=0.1f;
    }

    [System.Serializable]
    public class ArchSettingData
    {
        public string titile;
        public SettingBtnType type;
        public ArchivesFloat value;
        public string[] showTexts;
        public Vector2Int sliderRange;
        public string sliderSuffix;
    }
    public enum SettingBtnType { Dropdown, Toggle, Slider }

    [Serializable]
    public struct ArchivesFloat
    {
        const int digit = 2;
        [SerializeField]
        private string value;
        public ArchivesFloat(float value)
        {
            // ⚠ 必须用 InvariantCulture：这段字符串是**跨机器/跨平台**的持久数据，
            //   用当前文化写，在小数点为逗号的区域会写成 "0,10"，读取端必然解析失败。
            this.value = value.ToString("F" + digit, CultureInfo.InvariantCulture); // 保留X位小数
        }

        public static implicit operator ArchivesFloat(float value)
        {
            return new ArchivesFloat(value);
        }

        /// <summary>
        /// 数值读取。⚠ 必须容错：字段本体是 string，空串/非法内容（老存档缺项、文件被改坏）
        /// 会让 <c>float.Parse</c> 抛 FormatException 冲垮整条调用链（曾导致打包版启动即崩），
        /// 而 <c>value ?? "0"</c> 只挡 null、挡不住空串。非法值一律按 0 处理，
        /// 并用 InvariantCulture 与写入端 <see cref="ArchivesFloat(float)"/> 对齐。
        /// </summary>
        public float RawFloat =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;

        public int RawInt => (int)RawFloat;
        public override string ToString()
        {
            return value;
        }
    }
}

}
