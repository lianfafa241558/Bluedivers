using System;
using System.Collections.Generic;
using Core;
using FPSGame.Gameplay;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;

/// <summary>
/// 武器背包：装备后按住使用背包键(UseBag)开火。
/// 内部挂一个 WeaponEnemyController（敌我通用武器），由本组件驱动其射击，武器朝向保持 prefab 默认。
/// 通过 IVehicleUIController（BagBase 已实现）把武器弹药/冷却/换弹状态实时返回给 UI。
/// 子弹射完（弹匣+后备耗尽）后自动从装备控制器卸载并让物体消失。
/// </summary>
public class WeaponBag : BagBase
{
    #region 参数

    [InspectorName("武器(WeaponEnemyController)")]
    public WeaponEnemyController weapon;

    [InspectorName("激活时的物体")]
    public GameObject activeGo;

    #endregion

    private bool m_FirePressed;

    protected override void Update()
    {
        // 注意：不调用 base.Update()，WeaponBag 没有充电概念，直接驱动武器并刷新 UI

        if (!Owner.IsValid()) return;
        if (!weapon) return;

        // 使用背包键(UseBag)控制开火
        bool inputDown = m_InputHandler.GetUseBagDown();
        bool inputHeld = m_InputHandler.GetUseBagHeld();
        bool inputUp = inputHeld == false && m_FirePressed;
        m_FirePressed = inputHeld;

        // 有键按下才提交射击指令，避免自动武器一直开火
        if (inputDown || inputHeld || inputUp)
        {
            weapon.ShootInputs(inputDown, inputHeld, inputUp);
        }

        // 每帧把武器状态返回给 UI
        UpdateWeaponUI();

        // 子弹射完自动卸载并消失
        if (weapon.Exhausted)
        {
            AutoUnload();
        }
    }

    /// <summary>
    /// 子弹射完：从玩家的装备控制器卸载自身，并让整个物体消失。
    /// </summary>
    private void AutoUnload()
    {
        // 卸载会触发 OnUninstall（隐藏武器 + activeGo，OnStateChange(false)）
        if (Owner.IsValid())
        {
            Owner.gameObject.GetComponent<EquipController>()?.UninstallEquip(this);
        }
        // 整个物体消失（隐藏，便于对象池复用）
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 把武器的弹药/冷却/换弹状态映射到 IVehicleUIController 回调，供 UI 显示。
    /// </summary>
    private void UpdateWeaponUI()
    {
        // 弹匣余量比例，驱动血条填充
        OnFillChange?.Invoke(true, weapon.Magazine.ScaleValue.RawFloat);
        // 当前弹匣 / 总弹量 文本
        OnTextChange?.Invoke(true, weapon.Magazine.CurrValue.RawInt + "/" + weapon.TotalAmmo);

        // 换弹中或射击冷却中显示红色
        bool busy = weapon.IsReloading || weapon.ShootInterval.ScaleValue < 1;
        OnColorChange?.Invoke(true, busy ? new Color(1, 0.5f, 0.5f, 0.35f) : new Color(0.9f, 0.96f, 1, 0.35f));
    }

    public override void OnInstall(I_Actor actor, Func<IEnumerable<IEquippable>> getEquippableList)
    {
        base.OnInstall(actor, getEquippableList);
        if (activeGo) activeGo.SetActive(true);

        if (weapon)
        {
            weapon.Owner = Owner.gameObject;
            weapon.ShowWeapon(true);
        }

        OnStateChange?.Invoke(true);
    }

    public override void OnUninstall()
    {
        if (weapon) weapon.ShowWeapon(false);
        if (activeGo) activeGo.SetActive(false);
        base.OnUninstall();//内部会 OnStateChange?.Invoke(false)
    }
}
