using System.Collections.Generic;
using System.Linq;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Gameplay;

namespace FPSGame.Gameplay
{

/// <summary>
/// 玩家手持与背包装备的安装卸载总控。
/// </summary>
[AddComponentMenu("玩家/装备控制器")]
public class EquipController : MonoBehaviour
{

    Dictionary<IEquippable, IFurniture> equips;

    IActor m_actor;
 
    void Awake()
    {
        equips = new ();
        m_actor = GetComponent<IActor>();
    }

    public IEnumerable<IEquippable> AllEquips()=> equips.Keys;

    public IEnumerable<IFurniture> AllFurns() => equips.Values;

    public Dictionary<IEquippable, IFurniture> Equips=> equips;

    /// <summary>
    /// 是否持有手持装备（HandEquip）。手持装备会占用玩家双手/空手状态，
    /// 期间应禁止滚轮/数字键切换武器（否则会被自动丢下）。
    /// </summary>
    public bool IsHoldingHandEquip()
    {
        foreach (var key in equips.Keys)
        {
            if (key is HandEquip) return true;
        }
        return false;
    }

    /// <summary>
    /// 丢下玩家手上的手持装备（HandEquip），每次只丢一件，返回是否真的丢下了。
    /// 卸载走 <see cref="UninstallEquip"/>，与"丢弃装备"轮盘（<c>Furniture_HandEquip.Operate</c> 卸载分支）一致：
    /// 清 IK、落地、恢复移速、按需切回主武器。供交互控制器在"手持物品时按交互键"时调用。
    /// </summary>
    /// <param name="replace">
    /// true = 替换式丢下（本帧紧接着会捡起另一件）。此时跳过两件事：
    /// ① 不切回主武器（<see cref="HandEquip.SkipRestoreWeaponOnUninstall"/>）——否则先切主武器、又立刻切空手，撞在武器切换状态机上；
    /// ② 不播"卸载"语音（<see cref="UninstallEquip"/> 的 <c>silent</c>）——紧接着会播"安装"语音，两条连着播会打架。
    /// </param>
    public bool TryDropHandEquip(bool replace = false)
    {
        // 先取出目标再卸载：UninstallEquip 会修改 equips，不能在枚举过程中直接改
        IEquippable handEquip = null;
        foreach (var key in equips.Keys)
        {
            if (key is HandEquip)
            {
                handEquip = key;
                break;
            }
        }
        if (handEquip == null) return false;

        if (replace && handEquip is HandEquip hand) hand.SkipRestoreWeaponOnUninstall = true;
        UninstallEquip(handEquip, silent: replace);
        return true;
    }

    /// <summary>
    /// 只通过交互组件装载，自己不调用
    /// </summary>
    public void InstallEquip(IEquippable equip,IFurniture furniture)
    {
        if(equips.TryAdd(equip, furniture))
        {
            // 装备物品语音
            if (m_actor != null) GlobalEventBus.PlayMeetSpeech(m_actor.gameObject, SpeechTypeEnum.Install);

            equip.OnInstall(m_actor, AllEquips);
            equip.OnEquipDestroy += HandleEquipDestroy;
            //不管最后通不通过，都会调用这个
            var toUninstall = equips.Where(kv => kv.Key.NeedUninstall(equip) && kv.Key != equip).ToList();
            foreach (var item in toUninstall)
            {
                Debug.Log("尝试卸载"+item.Key.ID);
                item.Value.Operate();
            }

            UpdateJumpKeyOccupied();
        }
    }

    /// <summary>
    /// 只通过交互组件卸载，furn调用
    /// </summary>
    /// <param name="silent">
    /// true 时不播"卸载"语音。供"替换式丢下"（<see cref="TryDropHandEquip"/>）使用：
    /// 紧接着就会安装另一件并播"安装"语音，两条连着播会打架。
    /// </param>
    public void UninstallEquip(IEquippable equip, bool silent = false)
    {
        if (equips.Remove(equip))
        {
            // 卸载物品语音
            if (!silent && m_actor != null) GlobalEventBus.PlayMeetSpeech(m_actor.gameObject, SpeechTypeEnum.Uninstall);

            equip.OnUninstall();
            equip.OnEquipDestroy -= HandleEquipDestroy;
            UpdateJumpKeyOccupied();
        }
    }

    /// <summary>
    /// 更新跳跃键占用状态：遍历所有装备，若有装备带 UseSpace 标志则占用跳跃键
    /// </summary>
    private void UpdateJumpKeyOccupied()
    {
        var controller = GetComponent<BaseSelfMoveableController>();
        if (controller != null)
        {
            controller.UseUpJump = equips.Keys.Any(kv => kv.HaveFlag(EquippableFlagEnum.UseSpace));
        }
    }

    public void HandleEquipDestroy(IEquippable equip)
    {
        equips.Remove(equip);
        //equip.OnEquipDestroy -= HandleEquipDestroy;
    }

}
}
