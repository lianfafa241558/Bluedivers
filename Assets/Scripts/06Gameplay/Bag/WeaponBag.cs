using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Gameplay;
using FPSGame.Weapon;

namespace FPSGame.Gameplay
{

/// <summary>
/// 武器背包：装备后按住使用背包键(UseBag)开火。
/// 内部挂一个 WeaponEnemyController（敌我通用武器），由本组件驱动其射击，武器朝向保持 prefab 默认。
/// 通过 IVehicleUIController（BagBase 已实现）把武器弹药/冷却/换弹状态实时返回给 UI。
/// 子弹射完（弹匣+后备耗尽）后自动从装备控制器卸载并让物体消失。
/// </summary>
[AddComponentMenu("装备/武器背包")]
public class WeaponBag : BagBase
{
    #region 参数

    [InspectorName("武器(WeaponEnemyController)")]
    public WeaponEnemyController weapon;

    #endregion

    private bool m_FirePressed;

    protected override void Update()
    {
        // 注意：不调用 base.Update()，WeaponBag 没有充电概念，直接驱动武器并刷新 UI

        if (!Owner.IsValidMono()) return;
        if (!weapon) return;

        // ⚠ 盟友实例上这件背包是**远端重放**装上去的：那边没有 PlayerInputHandler
        //   （BagBase.OnInstall 取到 null）⇒ 不能拿它当输入源，否则每帧 NRE。
        if (m_InputHandler != null)
        {
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
    /// 本机真开了一发 ⇒ 抛给 09 的桥上报（对端只做表现）。
    /// <para>▍为什么挂 <c>weapon.OnShoot</c> 而不是在 Update 里按输入猜：输入"按下/按住"不等于真开火
    /// （弹匣空、冷却中都不发射），按输入猜会给对端放一堆假弹道。</para>
    /// </summary>
    private void HandleWeaponShoot(WeaponBaseController w)
    {
        if (weapon == null || !Owner.IsValidMono()) return;
        var muzzle = weapon.GetMuzzle(0);
        Vector3 dir = muzzle != null ? muzzle.forward : Owner.transform.forward;
        FPSGame.Gameplay.BattleEventBus.BagShoot(Owner.gameObject, dir, weapon.UseDamageIndex);
    }

    /// <summary>
    /// 子弹射完：从玩家的装备控制器卸载自身，并让整个物体消失。
    /// </summary>
    private void AutoUnload()
    {
        // 卸载会触发 OnUninstall（隐藏武器 + activeGo，OnStateChange(false)）
        if (Owner.IsValidMono())
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

    public override void OnInstall(IActor actor, Func<IEnumerable<IEquippable>> getEquippableList)
    {
        base.OnInstall(actor, getEquippableList);
   
        if (weapon)
        {
            weapon.Owner = Owner.gameObject;
            weapon.ShowWeapon(true);
            weapon.OnShoot -= HandleWeaponShoot;   // 幂等：反复装卸不重复挂
            weapon.OnShoot += HandleWeaponShoot;
        }

        OnStateChange?.Invoke(true);
    }

    public override void OnUninstall()
    {
        if (weapon)
        {
            weapon.OnShoot -= HandleWeaponShoot;
            weapon.ShowWeapon(false);
        }

        base.OnUninstall();//内部会 OnStateChange?.Invoke(false)
    }

    private void OnDestroy()
    {
        if (weapon) weapon.OnShoot -= HandleWeaponShoot;
    }
}
}
