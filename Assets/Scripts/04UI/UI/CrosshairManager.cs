using System.Collections.Generic;
using GameContract;

using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.UI
{
    public class CrosshairManager : CrosshairManagerBase
    {

        [SerializeField]
        protected PlayerWeaponsManager m_WeaponsManager;
        protected Dictionary<WeaponPlayerController, Animator> m_DicSightGo = new();
#pragma warning disable CS0414
        [SerializeField]
        bool isStartHave;
#pragma warning restore CS0414

        protected override void Start()
        {
            base.Start();

            m_WeaponsManager = GameObject.FindObjectOfType<PlayerWeaponsManager>();
            if (m_WeaponsManager)
            {
                OnPlayerCreate(null);
                isStartHave = true;
            }
            else
            {
                isStartHave = false;
            }
            GlobalEventSub.OnPlayerCreate += OnPlayerCreate;
            GlobalEventSub.OnViewSwitch += OnViewSwitch;
        }

        protected override void OnDestroy()
        {
            GlobalEventSub.OnPlayerCreate -= OnPlayerCreate;
            GlobalEventSub.OnViewSwitch -= OnViewSwitch;
            if (m_WeaponsManager)
            {
                m_WeaponsManager.OnSwitchedToWeapon -= SwitchWeapon;
                m_WeaponsManager.OnAim -= OnAim;
            }
            base.OnDestroy();
        }

        private void OnPlayerCreate(I_Actor go)
        {
            if(go.IsValid()) m_WeaponsManager = go.transform.GetComponent<PlayerWeaponsManager>();
            m_WeaponsManager.OnSwitchedToWeapon += SwitchWeapon;
            m_WeaponsManager.OnAim += OnAim;
            SwitchWeapon(m_WeaponsManager.GetActiveWeapon(), false);
        }

        private void OnViewSwitch(bool isThirdPerson)
        {
            RefreshCrosshairVisibility(m_WeaponsManager.IsAiming);
        }
        protected override void SwitchWeapon(WeaponPlayerController weapon, bool isSec = false)
        {
            base.SwitchWeapon(weapon, isSec);
            // 切武器后刷新准星（考虑第三人称状态）
            RefreshCrosshairVisibility(m_WeaponsManager.IsAiming);
        }

        protected override void SetAnimGo()
        {
            //如果是新插入进创建一个实例，已有就隐藏现在的，切过去
            if (m_DicSightGo.TryGetValue(m_Weapons, out var animator))
            {
                if (m_ActiveSightGo) m_ActiveSightGo.transform.SetParent(Tool.GetExchangeArea(), true);
                m_ActiveSightGo = animator;
                animator.transform.SetParent(transform, true);
                animator.SetTrigger(Constants.k_AnimResetParameter);
            }
            else
            {
                if (m_ActiveSightGo) m_ActiveSightGo.transform.SetParent(Tool.GetExchangeArea(), true);
                m_ActiveSightGo = Instantiate(m_Weapons.Sight, transform);
                m_DicSightGo.Add(m_Weapons, m_ActiveSightGo);
            }
            m_ActiveSightGo.SetFloat(Constants.k_AnimChatgetSpeedParameter, 1 / Mathf.Max(m_Weapons.AttrFinal(WeaponAttrType.ChargeDuration).RawFloat, 0.1f));
        }

        /// <summary>
        /// 开镜时隐藏准星（第三人称时反转：非瞄准显示，瞄准隐藏）
        /// </summary>
        void OnAim(bool state)
        {
            RefreshCrosshairVisibility(state);
        }

        private void RefreshCrosshairVisibility(bool aimState)
        {
            if (m_Weapons == null || m_ActiveSightGo == null) return;

            bool isThirdPerson = m_WeaponsManager.GetComponent<PlayerController>().IsThirdPerson;
            bool hideCrosshair;

            if (isThirdPerson)
            {
                // 第三人称：非瞄准隐藏，瞄准显示
                hideCrosshair = !aimState;
            }
            else
            {
                // 第一人称：原逻辑
                if (!m_Weapons.AimingHideCrosshair) return;
                hideCrosshair = aimState;
            }

            if (hideCrosshair)
                m_ActiveSightGo.transform.SetParent(Tool.GetExchangeArea(), true);
            else
                m_ActiveSightGo.transform.SetParent(transform, true);
        }
    }
}