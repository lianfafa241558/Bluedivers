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
    /// <c>.VehicleCustomDic</c>，而存档的**持有者** <c>ArchiveLoader</c> 在 09Manager
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

    private const int ExpMeet=1000;

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
        if (exp >= ExpMeet)
        {
            level += exp / ExpMeet;
            exp %= ExpMeet;
        }

       var data= roleDataDic[ID];
        data.Level=level;
        data.Exp= exp;
    }

    public void GetRoleLevel(string ID, out int level, out float expScale)
    {
        var data = roleDataDic[ID];

        level = data.Level;
        expScale = data.Exp/ ExpMeet;
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
        if (data.Exp / ExpMeet > 0)
        {
            data.Level += data.Exp / ExpMeet;
            data.Exp %= ExpMeet;
        }

        level = data.Level;
        expScale = data.Exp/ ExpMeet;

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

    /// <summary>给角色**身上装备的所有武器**加经验（与角色同一条曲线：<see cref="ExpMeet"/> 满一级）。</summary>
    /// <param name="ID">角色 ID</param>
    /// <param name="gainValue">本次经验（与角色结算得到的量相同）</param>
    public void GainWeaponExp(string ID, int gainValue)
    {
        if (gainValue <= 0) return;
        var role = roleDataDic[ID];
        var data = Resources.Load<RoleData_SO>("GameData/Role/RD_" + ID);
        if (data == null) return;   // 角色资源缺失是配置问题，武器经验静默不加（结算界面不该因此中断）
        for (int i = 0; i < 6; ++i)
        {
            var type = (WeaponTypeEnum)i;
            var list = data.weapons[type];
            if (list == null || list.Count == 0) continue;
            var weapon = list[role.weaponSelect[type] % list.Count];
            if (weapon == null) continue;
            int lenght = weapon.UpgradeCount().Length;
            if (lenght == 0) continue;   // Armor 之类没有改装树，加了也没地方用
            weaponUpgradeDic.TryGet(ID + "_" + weapon.WeaponName, new(ID + "_" + weapon.WeaponName, lenght)).GainExp(gainValue);
        }
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
        // ⚠ 正常路径不需要黄字（2026-10-10 用户口径：没用的内容先不打印）
        Debug.Log("加载了存档" + name + "数据数量" + roleDataDic.Count);
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

        /// <summary>武器等级（1 起）。升级所需经验与角色相同：<see cref="ExpMeet"/>。</summary>
        /// <remarks>⚠ 老存档没有这个字段 ⇒ 反序列化得 <c>0</c>（JsonUtility 不走构造函数，字段初始值也不生效），
        /// 所以等级<b>只能经 <see cref="Level"/> 读</b>（自动钳到 ≥1）。</remarks>
        [SerializeField]
        private int level = 1;
        /// <summary>当前等级内已累计的经验</summary>
        public int exp;

        /// <summary>武器等级（读接口对老存档做兼容：<c>level&lt;=0</c> 一律当 1 级）</summary>
        public int Level
        {
            get => level <= 0 ? 1 : level;
            set => level = value;
        }

        /// <summary>当前等级的经验进度（0~1），给 UI 进度条用</summary>
        public float ExpScale => (float)exp / ExpMeet;

        /// <summary>加经验并结算升级。返回是否升级。</summary>
        public bool GainExp(int gainValue)
        {
            int lv = Level;          // 走钳制，别拿裸 level（老存档是 0）
            int e = exp + gainValue;
            if (e < ExpMeet)
            {
                exp = e;
                return false;
            }
            lv += e / ExpMeet;
            exp = e % ExpMeet;
            level = lv;
            return true;
        }

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
            level = 1;
            exp = 0;
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
        if (map == null || map.Count == 0)
        {
            // ⚠ 这条**会**把一个本来有表的 sid 清掉（发送方没有载具改装时 DTO 就是 null）⇒ 必须留痕，别静默
            if (BySid.Remove(sid))
                FPSGame.Utils.NetSyncLog.SyncLog("载具/外观", $"sid={sid} 的载具配置被**清空**（对端那条没带载具改装）");
            return;
        }

        BySid[sid] = map;
        FPSGame.Utils.NetSyncLog.SyncLog("载具/外观", $"sid={sid} 载具配置写入：{DumpMap(map)}");
    }

    /// <summary>诊断用：把一份 sid 配置摊成 <c>车名:皮肤</c> 列表（只看前 4 项，够判"是谁的配置"）。</summary>
    private static string DumpMap(Dictionary<string, ArchivesData_SO.ArchVehicleData> map)
    {
        if (map == null) return "<null>";
        var sb = new System.Text.StringBuilder();
        int n = 0;
        foreach (var kv in map)
        {
            if (n >= 8) { sb.Append($",…(共{map.Count}项)"); break; }
            if (n > 0) sb.Append(", ");
            sb.Append(kv.Key).Append(':').Append(kv.Value != null ? kv.Value.skinIndex.ToString() : "null");
            ++n;
        }
        return sb.Length > 0 ? sb.ToString() : "<空>";
    }

    /// <summary>该 sid 有没有**同步来的**配置表（true = 他这份配置已经到过了）。
    /// <para>▍给渲染侧判"该不该回退本机存档"：**有表却查不到这辆车 ⇒ 不许回退**
    /// （否则就是"用我的配置冒充别人的"，与 <see cref="TryGet"/> 的口径一致）。</para></summary>
    public static bool HasSid(uint sid) => BySid.TryGetValue(sid, out var m) && m != null && m.Count > 0;

    /// <summary>诊断用：某 sid 当前同步到的载具配置（没有表 ⇒ <c>&lt;无表&gt;</c>）。</summary>
    public static string DumpSid(uint sid) => BySid.TryGetValue(sid, out var m) ? DumpMap(m) : "<无表>";

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
        => TryGet(sid, vehicleName, out data, out _);

    /// <summary>同上，另外告诉调用方**数据是从哪来的**（<paramref name="isLocalArchive"/> = 走的是本机存档的兜底）。
    /// <para>▍为什么要这个出口：渲染侧要判"该不该接受这次兜底" —— 该 sid **有同步表却查不到这辆车**时
    /// 绝不能用本机存档冒充（见本方法里的 <c>return false</c>），而"他这份配置还没到"时兜一下是有意的。</para></summary>
    public static bool TryGet(uint sid, string vehicleName, out ArchivesData_SO.ArchVehicleData data, out bool isLocalArchive)
    {
        data = null;
        isLocalArchive = false;
        if (string.IsNullOrEmpty(vehicleName)) return false;

        Dictionary<string, ArchivesData_SO.ArchVehicleData> remote;
        if (sid != LocalSid && BySid.TryGetValue(sid, out remote) && remote != null)
        {
            if (remote.TryGetValue(vehicleName, out data) && data != null) return true;
            return false;   // 该 sid 有同步表但没有这辆车 ⇒ 不回退（避免"用本机配置冒充别人的"）
        }

        var arch = ArchivesData_SO.Current;
        if (arch == null) return false;
        isLocalArchive = true;
        return arch.VehicleCustomDic.TryGet(vehicleName, out data) && data != null;
    }
}

}
