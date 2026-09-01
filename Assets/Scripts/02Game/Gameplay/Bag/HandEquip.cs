using System;
using System.Collections.Generic;
using FPSGame.Furn;
using GameContract;
using RootMotion.FinalIK;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 手持装备：实现 IEquippable，由 EquipController 管理，可通过"丢弃装备"轮盘卸载。
    /// 装备后：模型挂在玩家 HandPoint 上、设置左右手 IK、强制玩家切空手、移速 -50%；
    /// 卸载后：清除 IK、恢复移速、落地为可再拾取实体、自动切回主武器。
    /// 触发自动丢下：切到其他武器 / 玩家倒地 / 进入不可携带载具（CanEnterVehicle=false）。
    /// </summary>
    public class HandEquip : MonoBehaviour, IEquippable
    {
        [InspectorName("移速惩罚系数（装：x系数，卸：/系数）")]
        public float MoveSpeedScaleModifier = 0.5f;

        [InspectorName("是否可带上载具（false 则进载具自动丢下）")]
        public bool CanEnterVehicle = false;

        [InspectorName("左手")]
        public Transform LHand;

        [InspectorName("右手")]
        public Transform RHand;

        [InspectorName("左肘 Bend Goal（可选，未配置则不生效）")]
        public Transform LElbow;

        [InspectorName("右肘 Bend Goal（可选，未配置则不生效）")]
        public Transform RElbow;

        [InspectorName("手持时局部偏移")]
        public Vector3 HandOffset = Vector3.zero;

        [InspectorName("手持时局部旋转")]
        public Vector3 HandEuler = Vector3.zero;

        I_Actor m_Owner;
        PlayerController m_Player;
        PlayerWeaponsManager m_Weapons;
        EquipController m_EquipController;
        Health m_Health;
        PlayerMountPoint m_MountPoint;

        /// <summary>入槽前缓存的装备根层级，卸载落地时恢复（否则留在第一人称武器层导致主相机不可见/交互异常）</summary>
        int m_CachedLayer = -1;

        /// <summary>被动丢下（切武器/倒地/进载具）时跳过恢复切主武器</summary>
        bool m_SkipRestoreWeapon;

        /// <summary>IEquippable.Owner（I_Actor）显式实现</summary>
        I_Actor IEquippable.Owner => m_Owner;

        string IEquippable.ID => GetComponent<IFurniture>()?.Id ?? name;

        /// <summary>手持装备不占用跳跃键</summary>
        bool IEquippable.HaveFlag(EquippableFlagEnum flag) => false;

        /// <summary>与背包类装备可共存，互不卸载</summary>
        bool IEquippable.NeedUninstall(IEquippable newEquip) => false;

        /// <summary>
        /// 装备：挂到玩家 HandPoint、设置左右手 IK、强制切空手、施加移速惩罚、订阅玩家事件。
        /// </summary>
        public void OnInstall(I_Actor actor, Func<IEnumerable<IEquippable>> getEquippableList)
        {
            m_Owner = actor;
            if (actor == null || actor.gameObject == null) return;

            m_Player = actor.gameObject.GetComponent<PlayerController>();
            m_Weapons = actor.gameObject.GetComponent<PlayerWeaponsManager>();
            m_EquipController = actor.gameObject.GetComponent<EquipController>();
            m_Health = actor.gameObject.GetComponent<Health>();
            m_MountPoint = actor.gameObject.GetComponent<PlayerMountPoint>();

            // 强制玩家切空手（装备在手中，不可持武器）
            if (m_Weapons != null)
            {
                m_Weapons.SwitchToWeaponIndex(PlayerWeaponsManager.SlotOf(WeaponTypeEnum.Empty), true, false,true);
            }

            // 挂到玩家承载组件的拾取道具点（HandPoint），由 IK 让手吸附到 LHand/RHand
            if (m_MountPoint != null)
            {
                Transform parent = m_MountPoint.HandPoint ? m_MountPoint.HandPoint : m_MountPoint.transform;
                transform.SetParent(parent, false);
                transform.localPosition = HandOffset;
                transform.localEulerAngles = HandEuler;

                // 设置左右手 IK，让玩家手吸附到装备的握点（手肘点用于引导手臂弯曲方向）
                m_MountPoint.SetHandIK(LHand, RHand, LElbow, RElbow);
            }
            else if (m_Player != null)
            {
                Transform parent = m_Player.transform;
                transform.SetParent(parent, false);
                transform.localPosition = HandOffset;
                transform.localEulerAngles = HandEuler;
            }

            // 装备切到第一人称武器层（与入槽武器一致），否则第一人称 WeaponCamera 看不到；
            // 缓存原层级，卸载落地时恢复
            m_CachedLayer = gameObject.layer;
            int fpsLayer =
                Mathf.RoundToInt(Mathf.Log(LayerDefinition.WeaponLayers.value,
                    2)); //此函数将层掩码转换为层索引（同 PlayerWeaponsManager.EquipWeapon）
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = fpsLayer;
            }


            // 移速惩罚
            if (m_Player != null)
            {
                m_Player.MoveSpeedScale *= MoveSpeedScaleModifier;
            }

            // 订阅玩家事件：切武器 / 倒地 / 进载具
            m_SkipRestoreWeapon = false;
            if (m_Weapons != null) m_Weapons.OnSwitchedToWeapon += OnWeaponSwitched;
            if (m_Health != null) m_Health.OnDie += OnPlayerDie;
            if (m_Player != null) m_Player.OnEnterVehicle += OnEnterVehicle;
        }

        /// <summary>
        /// 卸载：清除 IK、恢复移速、落地为可再拾取实体、退订事件、按需切回主武器。
        /// </summary>
        public void OnUninstall()
        {
            // 退订玩家事件
            if (m_Weapons != null) m_Weapons.OnSwitchedToWeapon -= OnWeaponSwitched;
            if (m_Health != null) m_Health.OnDie -= OnPlayerDie;
            if (m_Player != null) m_Player.OnEnterVehicle -= OnEnterVehicle;

            // 清除左右手 IK
            if (m_MountPoint != null)
            {
                m_MountPoint.ClearHandIK();
            }

            // 恢复移速
            if (m_Player != null)
            {
                m_Player.MoveSpeedScale /= MoveSpeedScaleModifier;
            }

            // 落地（保持模型可见）
            transform.SetParent(null, true);

            // 恢复原层级（移除第一人称武器层，主相机与交互系统才能看到落地装备）
            if (m_CachedLayer >= 0)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = m_CachedLayer;
                }
                m_CachedLayer = -1;
            }

            // 重新启用交互组件，使其可被再次拾取
            // （切武器/倒地/进载具的被动丢下走此路径，轮盘卸载走 Furniture_HandEquip.Operate 卸载分支，
            //   两条路径都必须复位交互组件）
            var pickup = GetComponent<Furniture_HandEquip>();
            if (pickup != null)
            {
                pickup.ResetForPickup();
                pickup.enabled = true;
            }

            // 被动丢下（切武器/倒地/进载具）时跳过，避免覆盖玩家刚切换的目标武器
            if (!m_SkipRestoreWeapon && m_Weapons != null)
            {
                m_Weapons.SwitchWeapon(true);
            }

            m_Owner = null;
            m_Player = null;
            m_Weapons = null;
            m_EquipController = null;
            m_Health = null;
            m_MountPoint = null;
        }

        /// <summary>玩家切到其他武器（非空手）时丢下装备</summary>
        void OnWeaponSwitched(WeaponPlayerController weapon, bool isSec)
        {
            // 空手武器是装备自己强制切换的，忽略；切到其他武器则丢下
            if (weapon == null || weapon.WeaponTypeEnum == WeaponTypeEnum.Empty) return;
            Drop();
        }

        /// <summary>玩家倒地时丢下装备</summary>
        void OnPlayerDie(GameObject source)
        {
            Drop();
        }

        /// <summary>玩家进入载具时，若不可携带则丢下装备</summary>
        void OnEnterVehicle()
        {
            if (!CanEnterVehicle)
            {
                Drop();
            }
        }

        /// <summary>丢下装备（走 UninstallEquip → OnUninstall）</summary>
        void Drop()
        {
            if (m_EquipController == null) return;
            m_SkipRestoreWeapon = true;
            m_EquipController.UninstallEquip(this);
        }

        /// <summary>装备控制器订阅的销毁事件（物体被销毁时触发）</summary>
        event Action<IEquippable> IEquippable.OnEquipDestroy
        {
            add => m_OnEquipDestroy += value;
            remove => m_OnEquipDestroy -= value;
        }

        /// <summary>OnEquipDestroy 的后端事件字段</summary>
        Action<IEquippable> m_OnEquipDestroy;

        /// <summary>装备被销毁时恢复移速（避免移速惩罚残留）并通知装备控制器</summary>
        protected virtual void OnDestroy()
        {
            m_OnEquipDestroy?.Invoke(this);

            if (m_Player != null)
            {
                m_Player.MoveSpeedScale /= MoveSpeedScaleModifier;
                m_Player = null;
            }
            // 退订事件防泄漏
            if (m_Weapons != null) m_Weapons.OnSwitchedToWeapon -= OnWeaponSwitched;
            if (m_Health != null) m_Health.OnDie -= OnPlayerDie;
            if (m_Player != null) m_Player.OnEnterVehicle -= OnEnterVehicle;
        }
    }
}
