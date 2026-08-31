using System;
using System.Collections.Generic;
using FPSGame.Furn;
using GameContract;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// WeaponPlayerController 的 IEquippable 分部实现：让武器可被 EquipController 管理，
    /// 从而出现在"丢弃装备"轮盘中并通过轮盘卸载。
    /// 拾取时 OnInstall 入支援槽，卸载时 OnUninstall 落地为可再拾取实体。
    /// </summary>
    public partial class WeaponPlayerController : IEquippable
    {
        /// <summary>装备我的玩家（OnUninstall 落地时取位置用）</summary>
        I_Actor m_EquipOwner;

        /// <summary>入槽前缓存的武器根物体层级，卸载落地时恢复（否则留在 FPS 武器层会导致主相机不可见/穿模）</summary>
        int m_CachedLayer = -1;

        /// <summary>落地时玩家前方偏移</summary>
        const float DropForwardDistance = 1f;

        /// <summary>显式实现规避与基类 Owner(GameObject) 的同名冲突</summary>
        I_Actor IEquippable.Owner => m_EquipOwner;

        string IEquippable.ID => WeaponName;

        /// <summary>武器不占用跳跃键</summary>
        bool IEquippable.HaveFlag(EquippableFlagEnum flag) => false;

        /// <summary>武器与背包类装备（护盾/喷气背包）可共存，互不卸载</summary>
        bool IEquippable.NeedUninstall(IEquippable newEquip) => false;

        /// <summary>
        /// 装备事件（由 EquipController.InstallEquip 调用）：拾取地面武器入支援槽。
        /// </summary>
        void IEquippable.OnInstall(I_Actor actor, Func<IEnumerable<IEquippable>> getEquippableList)
        {
            m_EquipOwner = actor;
            var wm = actor.gameObject.GetComponent<PlayerWeaponsManager>();
            if (wm != null)
            {
                // 捡起前缓存武器原层级，卸载时恢复
                m_CachedLayer = gameObject.layer;
                wm.EquipGroundWeapon(this);
            }
        }

        /// <summary>
        /// 卸载事件（由 EquipController.UninstallEquip 调用）：武器出槽并落地为可再拾取实体。
        /// </summary>
        void IEquippable.OnUninstall()
        {
            // 出槽 + 断订阅 + 自动切换下一把
            var wm = m_EquipOwner != null
                ? m_EquipOwner.gameObject.GetComponent<PlayerWeaponsManager>()
                : null;
            if (wm != null)
            {
                wm.DetachWeapon(this);
            }

            // 落地：脱离玩家，摆到玩家面前
            transform.SetParent(null, true);
            Vector3 dropPos = m_EquipOwner != null && m_EquipOwner.gameObject != null
                ? m_EquipOwner.CenterPos + m_EquipOwner.transform.forward * DropForwardDistance
                : transform.position;
            dropPos.y = Mathf.Max(dropPos.y, 0f);
            transform.position = dropPos;

            // 恢复模型可见：武器可能从未被切换（WeaponRoot 入槽时 SetActive(false)），
            // 直接激活 WeaponRoot，避免卸载落地后模型消失（不调 ShowWeapon(true) 以免触发换枪音效/清理）
            if (WeaponRoot != null)
            {
                WeaponRoot.SetActive(true);
            }

            // 恢复原层级（移除 FPS 武器层）
            if (m_CachedLayer >= 0)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = m_CachedLayer;
                }
            }

            // 重新启用交互组件，使其重新注册进 Furniture_Attached.list 可被再次拾取
            var pickup = GetComponent<Furniture_WeaponPickup>();
            if (pickup != null)
            {
                pickup.ResetForPickup();
                pickup.enabled = true;
            }
        }

        /// <summary>装备控制器订阅的销毁事件（物体被销毁时触发）</summary>
        event Action<IEquippable> IEquippable.OnEquipDestroy
        {
            add => m_OnEquipDestroy += value;
            remove => m_OnEquipDestroy -= value;
        }

        /// <summary>OnEquipDestroy 的后端事件字段</summary>
        Action<IEquippable> m_OnEquipDestroy;

        /// <summary>物体被销毁时通知装备控制器</summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_OnEquipDestroy?.Invoke(this);
        }
    }
}
