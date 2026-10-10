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

        GlobalEventBus.OnGainExp?.Invoke(ID, level, expScale);
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

    /// <summary>
    /// 每类武器**选中的模组下标**（下标 = <c>(int)WeaponTypeEnum</c>）。
    /// <para>▍与 <see cref="GetWeaponUpgrade"/> 分开：那个只导出 <c>selectIndex</c>（改装档位），
    /// 模组是**独立**选择（<c>WeaponUpgradeData.selectModuleIndex</c>）—— 联机同步缺它时，
    /// 盟友那侧只能按 0 装（看起来"我装了模组、别人看不见"）。</para>
    /// </summary>
    public int[] GetWeaponModules(string ID)
    {
        var re = new int[6];
        var role = roleDataDic[ID];
        var data = Resources.Load<RoleData_SO>("GameData/Role/RD_" + ID).weapons;
        for (int i = 0; i < 6; ++i)
        {
            var weapon = data[(WeaponTypeEnum)i][role.weaponSelect[(WeaponTypeEnum)i] % data[(WeaponTypeEnum)i].Count];
            re[i] = weaponUpgradeDic.TryGet(ID + "_" + weapon.WeaponName, new(ID + "_" + weapon.WeaponName, weapon.UpgradeCount().Length)).selectModuleIndex;
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

/// <summary>
/// 【联机】各玩家的**载具改装**表（数据自持，仿 <c>TeamState</c>/<c>BattleState</c>）。
///
/// <para>▍为什么需要它：载具（外骨骼 / 炮台）在各端都读**本机存档**渲染
/// （<c>BattleApplyVehicleData.Awake</c>）⇒ 别人看你的载具用的是**他自己**的配置，各看各的。
/// 这里按 sid 存一份同步来的配置，渲染侧按"这台载具的持有者（驾驶者）"去取。</para>
///
/// <para>▍写入方：09 侧的网络桥（<c>TeamNetBridge</c>）；读取方：06/10 的载具渲染组件。
/// 本类在 06_Gameplay/Data，对 06/09/10 都可见，且不依赖任何网络类型。</para>
///
/// <para>▍⭐ **车的归属口径（用户 2026-10-10 口径）**：载具一般由**战备呼叫**生成 ⇒ **谁呼叫的算谁的**；
/// 不是呼叫出来的（场景摆好的、任务脚本放的）⇒ **按房主（sid 0）**。</para>
/// </summary>
public static class VehicleCustomState
{
    /// <summary>本机在房主那边的会话 sid（房主 = 0）。由 09 的桥在开房/入房成功时写入。</summary>
    public static uint LocalSid { get; private set; }

    private static readonly Dictionary<uint, Dictionary<string, ArchivesData_SO.ArchVehicleData>> BySid
        = new Dictionary<uint, Dictionary<string, ArchivesData_SO.ArchVehicleData>>();

    /// <summary>已注册的载具渲染目标（<c>BattleApplyVehicleData</c> 在 Awake 自登记）。
    /// <para>▍为什么要这个注册表：配置/名单到达时要**重刷场景里已存在的载具**（新玩家进房那一刻，
    /// 场景里的载具是按"默认归属(房主)"渲染的，但房主那份配置可能刚到；进战斗场景后也可能后到）。
    /// 而 09 的桥看不见 10_Effect（10 在 09 之上）⇒ 只能用 <see cref="Action"/> 反向回调。</para></summary>
    private static readonly List<Action> RefreshTargets = new List<Action>();

    /// <summary>写入本机会话 sid（决定 <see cref="TryGet"/> 走"本机存档"还是"同步表"）。</summary>
    public static void SetLocalSid(uint sid) => LocalSid = sid;

    /// <summary>写入某玩家的载具配置（sid = 0 = 房主自己）。传 null/空表 = 清掉该 sid。</summary>
    public static void Set(uint sid, Dictionary<string, ArchivesData_SO.ArchVehicleData> map)
    {
        if (map == null || map.Count == 0) { BySid.Remove(sid); return; }
        BySid[sid] = map;
    }

    /// <summary>关房/退房时清空（否则下一房会带出上一房的配置）。</summary>
    public static void Clear()
    {
        BySid.Clear();
        LocalSid = 0;
    }

    /// <summary>【载具侧】登记一个"可以重刷自己外观"的目标（同 <see cref="UnregisterRefresh"/> 成对）。</summary>
    public static void RegisterRefresh(Action cb)
    {
        if (cb != null && !RefreshTargets.Contains(cb)) RefreshTargets.Add(cb);
    }

    /// <summary>【载具侧】注销（目标销毁时必须调，否则会留在表里变悬空委托）。</summary>
    public static void UnregisterRefresh(Action cb) => RefreshTargets.Remove(cb);

    /// <summary>【09 桥调用】配置/名单变化后重刷场景里所有已注册载具的外观（各目标自己决定归属 sid）。</summary>
    public static void RefreshAll()
    {
        if (RefreshTargets.Count == 0) return;
        // 快照遍历：回调里可能反注册（换场景/销毁）
        var snapshot = RefreshTargets.ToArray();
        for (int i = 0; i < snapshot.Length; ++i) snapshot[i]?.Invoke();
    }

    /// <summary>
    /// 取"某玩家"的某辆载具改装。
    /// <para>▍本机（<paramref name="sid"/> == <see cref="LocalSid"/>，或该 sid 没同步过）**直接读本机存档**：
    /// 这样舰桥里改完立刻生效，不用等自己那条同步绕房主一圈回来。</para>
    /// </summary>
    public static bool TryGet(uint sid, string vehicleName, out ArchivesData_SO.ArchVehicleData data)
    {
        data = null;
        if (string.IsNullOrEmpty(vehicleName)) return false;

        Dictionary<string, ArchivesData_SO.ArchVehicleData> remote;
        if (sid != LocalSid && BySid.TryGetValue(sid, out remote) && remote != null)
        {
            if (remote.TryGetValue(vehicleName, out data) && data != null) return true;
            return false;   // 该 sid 有同步表但没有这辆车 ⇒ 不回退（避免"用本机配置冒充别人的"）
        }

        var arch = ArchivesData_SO.Current;
        if (arch == null) return false;
        return arch.VehicleCustomDic.TryGet(vehicleName, out data) && data != null;
    }
}

}
