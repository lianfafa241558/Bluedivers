using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;

using FPSGame.Game;
using FPSGame.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using FPSGame.Utils;
using FPSGame.Weapon;
using PEMaths;

namespace FPSGame.UI
{
    /// <summary>
    /// 按当前武器切换准星。
    /// </summary>
    [AddComponentMenu("UI/HUD/准星管理")]
    public class CrosshairManager : CrosshairManagerBase
    {

        [SerializeField]
        protected PlayerWeaponsManager m_WeaponsManager;
        protected Dictionary<WeaponPlayerController, Animator> m_DicSightGo = new();
#pragma warning disable CS0414
        [SerializeField]
        bool isStartHave;
#pragma warning restore CS0414

        [Header("热量环")]
        [InspectorName("热量环（一圈填充）")]
        [Tooltip("不使用热量系统的武器会自动隐藏它")]
        [SerializeField] private Image _heatRing;

        [InspectorName("热量渐变色（0=冷 → 1=满热）")]
        [Tooltip("按当前热量取色，可手调冷→热的颜色与透明度")]
        [GradientUsage(true)]
        [SerializeField] private Gradient _heatGradient;

        [InspectorName("热量环跟随平滑速度")]
        [Tooltip("热量是「每发一档」的阶梯，直接赋值会跳格；0 = 不平滑")]
        [SerializeField] private float _heatSmoothSpeed = 12f;

        /// <summary>上次写入的填充量（0~1）：没变就别标脏 Canvas</summary>
        private float _lastHeat = -1f;

        /// <summary>平滑后的显示用热量：填充与取色共用，避免"条子还在走、颜色已跳档"</summary>
        private float _displayedHeat = -1f;

        /// <summary>准星的原始 localScale（按 Animator 实例缓存）：同一个准星会被多把武器复用，
        /// 不缓存就会在每次切武器时把已放大的尺寸再乘一次。</summary>
        private readonly Dictionary<Animator, Vector3> m_SightBaseScale = new();

        /// <summary>当前订阅散布属性的那个属性对象</summary>
        private GameAttribute m_SpreadAttr;

        /// <summary>热量环的原始 localScale（只取一次）：它不在准星层级里，得跟准星一起按同一倍率缩放</summary>
        private Vector3 m_HeatRingBaseScale;
        private bool m_HeatRingScaleCached;

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
            UnitEventBus.OnPlayerCreate += OnPlayerCreate;
            GlobalEventBus.OnViewSwitch += OnViewSwitch;
        }

        protected override void OnDestroy()
        {
            UnitEventBus.OnPlayerCreate -= OnPlayerCreate;
            GlobalEventBus.OnViewSwitch -= OnViewSwitch;
            if (m_WeaponsManager)
            {
                m_WeaponsManager.OnSwitchedToWeapon -= SwitchWeapon;
                m_WeaponsManager.OnAim -= OnAim;
            }
            base.OnDestroy();
        }

        private void OnPlayerCreate(IActor go)
        {
            if(go.IsValidMono()) m_WeaponsManager = go.transform.GetComponent<PlayerWeaponsManager>();
            m_WeaponsManager.OnSwitchedToWeapon += SwitchWeapon;
            m_WeaponsManager.OnAim += OnAim;
            SwitchWeapon(m_WeaponsManager.GetActiveWeapon(), false);
        }
        //TODO:这里可能切换了场景，但是准星没被删除

        private void OnViewSwitch(bool isThirdPerson)
        {
            if(m_WeaponsManager) RefreshCrosshairVisibility(m_WeaponsManager.IsAiming);
        }
        protected override void SwitchWeapon(WeaponPlayerController weapon, bool isSec = false)
        {
            base.SwitchWeapon(weapon, isSec);
            // 切武器后刷新准星（考虑第三人称状态）
            RefreshCrosshairVisibility(m_WeaponsManager.IsAiming);
            RefreshHeatRing();
        }

        protected override void OnWeaponUnbound() => UnbindSpreadAttr();

        protected override void OnWeaponBound(WeaponPlayerController weapon)
        {
            BindSpreadAttr(weapon);
            ApplySightScale();
        }

        /// <summary>
        /// 用「子弹散布角」的 最终值/原始值 等比缩放当前准星（原始 5 → 最终 10 ⇒ 两倍大）。
        /// 由切武器与散布属性变化（升级/装模组）触发，不放在 Update 里。
        /// </summary>
        private void ApplySightScale()
        {
            float scale = GetSpreadScale();

            if (m_ActiveSightGo)
            {
                if (!m_SightBaseScale.TryGetValue(m_ActiveSightGo, out var baseScale))
                {
                    baseScale = m_ActiveSightGo.transform.localScale;
                    m_SightBaseScale.Add(m_ActiveSightGo, baseScale);
                }
                m_ActiveSightGo.transform.localScale = baseScale * scale;
            }

            // 热量环不在准星层级里（是 PlayerWnd 上独立的 UI 元素）⇒ 得单独跟着缩，否则散布变大时环会显得偏小
            if (_heatRing)
            {
                if (!m_HeatRingScaleCached)
                {
                    m_HeatRingBaseScale = _heatRing.transform.localScale;
                    m_HeatRingScaleCached = true;
                }
                _heatRing.transform.localScale = m_HeatRingBaseScale * scale;
            }
        }

        /// <summary>散布倍率。⚠ 原始值/最终值为 0 时比值无意义（最终值 0 会把准星缩成看不见），一律按 1 处理。</summary>
        private float GetSpreadScale()
        {
            if (!m_SpreadAttr) return 1f;
            float prime = m_SpreadAttr.PrimeValue.RawFloat;
            float final = m_SpreadAttr.FinalValue.RawFloat;
            if (prime <= 0f || final <= 0f) return 1f;
            return final / prime;
        }

        private void BindSpreadAttr(WeaponPlayerController weapon)
        {
            UnbindSpreadAttr();
            if (!weapon.IsValid()) return;
            var attr = weapon.cfg[WeaponAttrType.BulletsSpreadAngle];
            if (!attr) return;
            m_SpreadAttr = attr;
            m_SpreadAttr.OnFinalValueChange += OnSpreadAttrChanged;
        }

        private void UnbindSpreadAttr()
        {
            if (!m_SpreadAttr) return;
            m_SpreadAttr.OnFinalValueChange -= OnSpreadAttrChanged;
            m_SpreadAttr = null;
        }

        /// <summary>属性值变了（升级/模组写回）时同步准星尺寸</summary>
        private void OnSpreadAttrChanged(PEInt value) => ApplySightScale();

        private void Update() => RefreshHeatRing();

        /// <summary>
        /// 热量环：填充与取色都用 <see cref="WeaponBaseController.HeatRatio"/>，不用热量的武器直接隐藏。
        /// ⚠ 热量是「每发一档」的阶梯，必须过一道帧率无关阻尼再写；只在数值变化时写 Image（每帧赋值会标脏 Canvas）。
        /// </summary>
        private void RefreshHeatRing()
        {
            if (!_heatRing) return;

            bool show = m_Weapons.IsValid() && m_Weapons.HasHeatSystem;
            if (_heatRing.enabled != show)
            {
                _heatRing.enabled = show;
                _lastHeat = -1f;
                _displayedHeat = -1f;   // 别从上一把枪的热量插值过来
            }
            if (!show) return;

            float heat = m_Weapons.HeatRatio;
            if (_displayedHeat < 0f) _displayedHeat = heat;
            else if (_heatSmoothSpeed > 0f)
                _displayedHeat = Mathf.Lerp(_displayedHeat, heat, 1f - Mathf.Exp(-_heatSmoothSpeed * Time.deltaTime));
            else _displayedHeat = heat;

            if (Mathf.Abs(_displayedHeat - _lastHeat) < 0.001f) return;
            _lastHeat = _displayedHeat;

            _heatRing.fillAmount = _displayedHeat;
            if (_heatGradient != null) _heatRing.color = _heatGradient.Evaluate(_displayedHeat);
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
