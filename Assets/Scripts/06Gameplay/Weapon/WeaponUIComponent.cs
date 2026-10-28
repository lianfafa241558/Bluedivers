using System.Collections;
using System.Collections.Generic;
using PEMaths;

using UnityEngine;
using UnityEngine.UI;
using FPSGame.Utils;

namespace FPSGame.Weapon
{

    /// <summary>
    /// 武器相关 UI 的数据驱动组件。
    /// </summary>
    [AddComponentMenu("武器/武器 UI 组件")]
    public class WeaponUIComponent : MonoBehaviour
    {

        [Header("显示总弹药")]
        [SerializeField]
        List<Text> T_AllAmmos;
        [Header("显示当前弹药")]
        [SerializeField]
        List<Text> T_NowAmmos;
        [Header("显示后备弹药")]
        [SerializeField]
        List<Text> T_SpareAmmos;
        [Header("显示总剩余弹药百分比")]
        [SerializeField]
        List<Image> I_RemainTotalAmmos;
        [Header("显示弹匣剩余弹药百分比")]
        [SerializeField]
        List<Image> I_RemainAmmos;
        [Header("显示蓄力百分比")]
        [SerializeField]
        List<Image> I_ChargeRatio;
        [Header("显示冷却百分比")]
        [SerializeField]
        List<Image> I_CoolRatio;

        [Header("冷却条配色")]
        [InspectorName("冷热条按热量染色")]
        [Tooltip("勾上后 I_CoolRatio 的颜色由下面这条渐变决定；不勾就保持 Image 自己的颜色")]
        [SerializeField]
        bool TintCoolRatio;

        [InspectorName("冷却条渐变色（0=冷 → 1=满热）")]
        [Tooltip("⚠ 必须同时勾上面那一项才生效。别指望留空＝不染色：Gradient 是类，Unity 会给未序列化的字段补一个白色默认值")]
        [SerializeField]
        Gradient CoolRatioGradient;

        [InspectorName("反转填充（空=冷、满=过热）")]
        [Tooltip("只翻转填充方向，不影响取色：勾上 = 初始空、越热越满、满条即红；不勾 = 满条是已冷却")]
        [SerializeField]
        bool ReverseCoolRatioFill;

        WeaponController m_weapon;

        /// <summary>上一帧写进 Image 的填充量（0~1）：只在变化时才写，别每帧标脏 Canvas</summary>
        private float _lastCoolRatio = -1f;

        private void Awake()
        {
            m_weapon = GetComponent<WeaponController>();
            if (!m_weapon)
            {
                Debug.LogError("[武器UI] 同物体上没有 WeaponController，已停用：" + name, this);
                enabled = false;
                return;
            }
            m_weapon.OnChargeRatioUpdate += ChargeRatioUI;
            m_weapon.OnUIUpdate += UpdateUI;
        }
        private void OnDestroy()
        {
            if (!m_weapon) return;
            m_weapon.OnChargeRatioUpdate -= ChargeRatioUI;
            m_weapon.OnUIUpdate -= UpdateUI;
        }

        void Update()
        {
            UpdateCoolRatio();
        }

        /// <summary>
        /// 冷却/热量条：热量武器（`HeatPerShot > 0`）按热量，其它武器仍按射击间隔的冷却进度。
        /// ⚠ 每帧无脑写 fillAmount/color 会把 Canvas 标脏重算 ⇒ 只在数值变了才写。
        /// </summary>
        void UpdateCoolRatio()
        {
            if (I_CoolRatio.Count == 0) return;

            float heat;   // 0 = 冷 / 可开火，1 = 满热 / 必须等
            if (m_weapon.HasHeatSystem)
            {
                heat = m_weapon.HeatRatio;
            }
            else
            {
                if (!m_weapon.ShootInterval) return;   // 还没 LogicInit（预览实例）
                heat = 1f - m_weapon.ShootInterval.ScaleValue.RawFloat;
            }

            // 反转只动填充方向，取色永远按热量 ⇒ 红不红跟这个开关无关
            float fill = ReverseCoolRatioFill ? heat : 1f - heat;

            if (Mathf.Abs(fill - _lastCoolRatio) < 0.001f) return;
            _lastCoolRatio = fill;

            // ⚠ Gradient 是类，Unity 会给"没序列化的字段"补一个白色默认值 ⇒ 只能用显式开关，不能靠 null 判断
            bool tinted = TintCoolRatio && CoolRatioGradient != null;
            Color tint = tinted ? CoolRatioGradient.Evaluate(heat) : default;
            for (int i = 0; i < I_CoolRatio.Count; i++)
            {
                var image = I_CoolRatio[i];
                if (!image) continue;   // 列表里留空槽是常见的（只填部分 HUD）
                image.fillAmount = fill;
                if (tinted) image.color = tint;
            }
        }

        void UpdateUI()
        {
            var weapon = m_weapon;

            //总弹药
            int A = weapon.CurrentTotalAmmo.RawInt;
            int B = weapon.TotalAmmo.RawInt;
            T_AllAmmos.ForEach(item => item.text = "" + Tool.FillZero(A, B.ToString().Length));
            //弹匣弹药
            if (!weapon.InfiniteMagazine)
            {
                A = weapon.Magazine.CurrValue.RawInt;
                B = weapon.Magazine.FinalValue.RawInt;
                T_NowAmmos.ForEach(item => item.text = "" + Tool.FillZero(A, B.ToString().Length));
            }
            else
            {
                T_NowAmmos.ForEach(item => item.text = "");
            }
            //后备弹药
            if (!weapon.InfiniteAmmo)
            {
                A = weapon.Ammo.CurrValue.RawInt;
                B = weapon.Ammo.FinalValue.RawInt;
                T_SpareAmmos.ForEach(item => item.text = "" + Tool.FillZero(A, B.ToString().Length));
            }
            else{
                T_SpareAmmos.ForEach(item => item.text = "");
            }
            //弹匣百分比
            var s = weapon.Magazine.ScaleValue.RawFloat;
            I_RemainAmmos.ForEach(item => item.fillAmount = s);
            //总弹药百分比
            s = weapon.CurrentTotalAmmoRatio.RawFloat;
            I_RemainTotalAmmos.ForEach(item => item.fillAmount = s);
        }


        void ChargeRatioUI(float value)
        {
            // ⚠ 用 for 不用 ForEach：这既是每帧路径（闭包 GC），也要顺便跳过空槽（HUD 只填了部分字段时很常见）
            for (int i = 0; i < I_ChargeRatio.Count; i++)
            {
                var image = I_ChargeRatio[i];
                if (image) image.fillAmount = value;
            }
        }


    }
}
