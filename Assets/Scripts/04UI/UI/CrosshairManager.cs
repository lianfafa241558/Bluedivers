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
                //Debug.LogError("提前找到新玩家" + m_WeaponsManager.gameObject, m_WeaponsManager.gameObject);
                OnPlayerCreate(null);
                isStartHave = true;
            }
            else
            {
                isStartHave = false;
            }
            GlobalEventSub.OnPlayerCreate += OnPlayerCreate;
        }

        protected override void OnDestroy()
        {
            GlobalEventSub.OnPlayerCreate -= OnPlayerCreate;
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
            //Debug.LogError("准星更新为新玩家" + m_WeaponsManager.gameObject, m_WeaponsManager.gameObject);
            m_WeaponsManager.OnSwitchedToWeapon += SwitchWeapon;
            m_WeaponsManager.OnAim += OnAim;
            SwitchWeapon(m_WeaponsManager.GetActiveWeapon(), false);

        }
        protected override void SetAnimGo()
        {
            //如果是新插入进创建一个实例，已有就隐藏现在的，切过去
            if (m_DicSightGo.TryGetValue(m_Weapons, out var animator))
            {
                if (m_ActiveSightGo) m_ActiveSightGo.transform.SetParent(Tool.GetExchangeArea(), true);
                //if (m_ActiveSightGo) m_ActiveSightGo.gameObject.SetActive(false);
                m_ActiveSightGo = animator;
                animator.transform.SetParent(transform, true);
                //animator.gameObject.SetActive(true);
                animator.SetTrigger(Constants.k_AnimResetParameter);

            }
            else
            {
                if (m_ActiveSightGo) m_ActiveSightGo.transform.SetParent(Tool.GetExchangeArea(),true);
                //if (m_ActiveSightGo) m_ActiveSightGo.gameObject.SetActive(false);
                m_ActiveSightGo = Instantiate(m_Weapons.Sight, transform);
                m_DicSightGo.Add(m_Weapons, m_ActiveSightGo);
            }
            m_ActiveSightGo.SetFloat(Constants.k_AnimChatgetSpeedParameter, 1 / Mathf.Max(m_Weapons.AttrFinal(WeaponAttrType.ChargeDuration).RawFloat, 0.1f));
        }

        /// <summary>
        /// 只要是开镜就隐藏准星
        /// </summary>
        /// <param name="state"></param>
        void OnAim(bool state)
        {
            if (m_Weapons.AimingHideCrosshair)
            {
                if (state)
                {
                    m_ActiveSightGo.transform.SetParent(Tool.GetExchangeArea(), true);
                }
                else
                {
                    m_ActiveSightGo.transform.SetParent(transform, true);
                }
                //m_ActiveSightGo.gameObject.SetActive(!state);
            }
        }
    }
}