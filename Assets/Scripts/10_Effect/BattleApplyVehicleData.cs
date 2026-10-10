using RootMotion.FinalIK;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using FPSGame.Utils;
using FPSGame.Data;
using FPSGame.Gameplay;
using FPSGame.GameData;
using FPSGame.Weapon;

namespace FPSGame.Effect
{

/// <summary>
/// 把载具数据应用到模型与贴图。
///
/// <para>▍归属口径（联机，用户 2026-10-10）：载具一般由**战备呼叫**生成 ⇒ **谁呼叫的算谁的**，
/// 由 <c>VFXAirdropEffect</c> 生成后调 <see cref="SetOwnerSid"/> 打标；不是呼叫出来的
/// （场景摆好的 / 任务脚本放的）⇒ **按房主（sid 0，默认值）**。</para>
///
/// <para>▍可被反复刷新：<c>VehicleCustomState.RegisterRefresh</c> 让 09 的桥在"配置/名单到达"时
/// 回调 <see cref="Apply"/>（<c>Apply</c> 自带"无变化跳过"）。</para>
/// </summary>
[AddComponentMenu("变体/载具数据应用")]
public class BattleApplyVehicleData : MonoBehaviour
{

    public VehicleData_SO data;
    public Transform weaponPointL, weaponPointR;
    public Transform LookPoint;
    [HideInInspector]
    public MpbController mpb;

    /// <summary>这辆车的**归属玩家** sid（0 = 房主）。默认房主 —— 场景摆好的 / 任务脚本放的载具都按房主的配置渲染。</summary>
    private uint _ownerSid;

    /// <summary>上一次应用的快照（用于跳过重复重刷：名单每次同步都会触发 <see cref="VehicleCustomState.RefreshAll"/>）。</summary>
    private bool _applied;
    private uint _appliedSid;
    private ArchivesData_SO.ArchVehicleData _appliedCfg;

    private VehicleWeaponsManager _manager;

    /// <summary>注册到 <see cref="VehicleCustomState"/> 的重刷回调（存成字段才能反注册）。</summary>
    private System.Action _refreshHandler;

    private void Awake()
    {
        _manager = GetComponent<VehicleWeaponsManager>();

        // 联机：配置/名单到达时重刷外观。09 的桥看不见本层（10 在 09 之上）⇒ 只能反向注册回调。
        _refreshHandler = Apply;
        VehicleCustomState.RegisterRefresh(_refreshHandler);

        // 默认按**房主**渲染：场景摆好的车 / 任务脚本放的车都算房主的；
        // 呼叫出来的车随后由 VFXAirdropEffect 用 SetOwnerSid(呼叫者) 纠正 ⇒ "谁叫的算谁的"。
        Apply();
    }

    private void OnDestroy()
    {
        if (_refreshHandler != null) VehicleCustomState.UnregisterRefresh(_refreshHandler);
    }

    /// <summary>
    /// 【空投 / 桥调用】设定这辆车的**归属玩家**（0 = 房主），并立刻按它重装外观。
    /// <para>▍口径（用户 2026-10-10）：载具一般由战备呼叫生成 ⇒ **谁呼叫的算谁的**；
    /// 不是呼叫出来的 ⇒ 按房主（就是这里的默认值）。</para>
    /// </summary>
    public void SetOwnerSid(uint sid)
    {
        _ownerSid = sid;
        Apply();
    }

    /// <summary>
    /// 按当前归属（<see cref="_ownerSid"/>）重装外观。
    /// <para>▍可重入：先清掉上一次装的武器 ⇒ 重复调用（归属纠正 / 配置到达）不会装两份武器。</para>
    /// <para>▍**无变化直接跳过**：<c>RefreshAll</c> 在名单每次同步时都会被调用（切准备状态也会），
    /// 每次都重建武器/贴图是白烧。</para>
    /// <para>▍取不到该 sid 的配置时退回本机存档（至少不是空外观）；配置到了 <c>RefreshAll</c> 会再刷一次。</para>
    /// </summary>
    public void Apply()
    {
        if (data == null) return;

        VehicleCustomState.TryGet(_ownerSid, data.vehicleName, out var cfg);

        if (_applied && _appliedSid == _ownerSid && SameCustom(_appliedCfg, cfg)) return;

        _applied = true;
        _appliedSid = _ownerSid;
        _appliedCfg = Clone(cfg);   // 存档里那份是"活对象"（VehicleWnd 会原地改）⇒ 必须快照才能比较出变化

        ApplyCustom(cfg);
    }

    /// <summary>两份载具改装是否等价（只比字段值；null 与 null 视为相同）。</summary>
    private static bool SameCustom(ArchivesData_SO.ArchVehicleData a, ArchivesData_SO.ArchVehicleData b)
    {
        if (a == null || b == null) return a == null && b == null;

        return a.leftWeaponIndex == b.leftWeaponIndex
            && a.rightWeaponIndex == b.rightWeaponIndex
            && a.skinIndex == b.skinIndex
            && a.blendIndex == b.blendIndex
            && Mathf.Approximately(a.blendScale.RawFloat, b.blendScale.RawFloat);
    }

    /// <summary>值快照（不保留引用 —— 本机那份是存档里的活对象）。</summary>
    private static ArchivesData_SO.ArchVehicleData Clone(ArchivesData_SO.ArchVehicleData src)
    {
        if (src == null) return null;
        return new ArchivesData_SO.ArchVehicleData
        {
            leftWeaponIndex = src.leftWeaponIndex,
            rightWeaponIndex = src.rightWeaponIndex,
            skinIndex = src.skinIndex,
            blendIndex = src.blendIndex,
            blendScale = src.blendScale.RawFloat,
        };
    }

    /// <summary>
    /// 把一份载具改装落到模型上（**可重入**：先清掉上一次装的武器，避免重复调用装两份武器）。
    /// <para><paramref name="arch"/> 为 null 时退回本机存档（保持改造前的行为）。</para>
    /// </summary>
    private void ApplyCustom(ArchivesData_SO.ArchVehicleData arch)
    {
        if (data == null) return;

        if (arch == null)
        {
            var local = ArchivesData_SO.Current;
            if (local != null) local.VehicleCustomDic.TryGet(data.vehicleName, out arch);
        }
        if (arch == null) return;

        _manager?.ClearWeapons();

        // 防止存档 index 越界
        int rightIdx = Mathf.Clamp(arch.rightWeaponIndex, 0, data.weaponRights.Length - 1);
        int leftIdx = Mathf.Clamp(arch.leftWeaponIndex, 0, data.weaponLefts.Length - 1);
        int skinIdx = Mathf.Clamp(arch.skinIndex, 0, data.Diffs.Length - 1);
        int blendIdx = Mathf.Clamp(arch.blendIndex, 0, data.Blends.Length - 1);

        AimIK ik;
        if (data.weaponRights.Length > 0)
        {
            var rightModel = Instantiate(data.weaponRights[rightIdx].go, weaponPointR);
            rightModel.transform.localPosition = Vector3.zero;
            rightModel.transform.localRotation = Quaternion.identity;

            if (rightModel.transform.TryGetComponentInChildren(out ik))
            {
                ik.solver.target = LookPoint;
            }
            _manager?.AddWeapon(rightModel.GetComponent<WeaponController>(), data.weaponRights[rightIdx].battleIcon);
        }

        if (data.weaponLefts.Length > 0)
        {
            var leftModel = Instantiate(data.weaponLefts[leftIdx].go, weaponPointL);
            leftModel.transform.localPosition = Vector3.zero;
            leftModel.transform.localRotation = Quaternion.identity;
            if (leftModel.transform.TryGetComponentInChildren(out ik))
            {
                ik.solver.target = LookPoint;
            }
            _manager?.AddWeapon(leftModel.GetComponent<WeaponController>(), data.weaponLefts[leftIdx].battleIcon);
        }

        if (mpb == null) mpb = new(transform);
        mpb.Set("_BaseMap", data.Diffs[skinIdx].texture)
        .Set("_BlendingScale", arch.blendScale.RawFloat)
        .Set("_BlendingMap", data.Blends[blendIdx].texture).Apply();
    }
}
}
