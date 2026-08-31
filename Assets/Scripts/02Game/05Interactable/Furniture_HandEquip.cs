using FPSGame.Attribute;
using FPSGame.Furn;
using FPSGame.Gameplay;
using UnityEngine;

/// <summary>
/// 手持装备的交互家具：地面可交互实体，玩家靠近交互后把 HandEquip 装备给玩家（拿在手里）。
/// 装备后本组件被禁用（跟随玩家手部，脱离交互列表），经"丢弃装备"轮盘卸载后落地复位可再拾取。
/// </summary>
public class Furniture_HandEquip : Furniture_Base
{
    public override string Desc => "捡起[" + ShowName + "]";

    HandEquip m_Equip;

    protected override void Awake()
    {
        base.Awake();
        m_Equip = GetComponent<HandEquip>();
    }

    /// <summary>
    /// 交互判断：基类条件 && 有装备 && 交互者是玩家 && 未持有该装备。
    /// </summary>
    public override bool CanOperate(GameObject unit)
    {
        if (!base.CanOperate(unit)) return false;
        if (m_Equip == null) return false;
        if (unit == null || !unit.TryGetComponent(out EquipController equipController)) return false;

        // 已持有则不可重复拾取
        return !equipController.Equips.ContainsKey(m_Equip);
    }

    /// <summary>
    /// 交互操作。被携带时组件为禁用态，但仍会被轮盘回调调用：
    /// - inOperate=false（地面可拾取态）：拾取，InstallEquip 给玩家，随后禁用本组件（跟随手部）。
    /// - inOperate=true（已被携带，轮盘点击卸载）：UninstallEquip，落地复位后 re-enable。
    /// 不调用 base.Operate()，避免触发 FurnitureOperate 事件/语音副作用。
    /// </summary>
    public override void Operate()
    {
        var user = owner;
        if (user == null || m_Equip == null) return;
        if (!user.TryGetComponent(out EquipController equipController)) return;

        if (!inOperate)
        {
            equipController.InstallEquip(m_Equip, this);
            inOperate = true;
            canOperate = false;
            // 禁用交互组件，跟随玩家手部（脱离 Furniture_Attached.list 不被交互扫描）
            enabled = false;
        }
        else
        {
            equipController.UninstallEquip(m_Equip);
            // 卸载落地后复位，可被再次拾取
            ResetForPickup();
            enabled = true;
        }
    }

    /// <summary>卸载落地后复位本组件，使其可被再次拾取</summary>
    public void ResetForPickup()
    {
        canOperate = true;
        inOperate = false;
        owner = null;
        pressTime = 0;
    }
}
