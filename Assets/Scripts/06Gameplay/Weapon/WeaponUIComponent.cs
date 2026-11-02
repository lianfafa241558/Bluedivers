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

        [InspectorName("显示总弹药(文本)")]
        [SerializeField]
        List<Text> T_AllAmmos;
        [InspectorName("显示当前弹药(文本)")]
        [SerializeField]
        List<Text> T_NowAmmos;
        [InspectorName("显示后备弹药(文本)")]
        [SerializeField]
        List<Text> T_SpareAmmos;
        [InspectorName("显示总剩余弹药百分比(图片)")]
        [SerializeField]
        List<Image> I_RemainTotalAmmos;
        [InspectorName("显示弹匣剩余弹药百分比(图片)")]
        [SerializeField]
        List<Image> I_RemainAmmos;
        [InspectorName("显示蓄力百分比(图片)")]
        [SerializeField]
        List<Image> I_ChargeRatio;
        [InspectorName("显示冷却百分比(图片)")]
        [SerializeField]
        List<Image> I_CoolRatio;

        [InspectorName("显示冷却百分比(文本)")]
        [SerializeField]
        List<Text> T_CoolRatio;

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

        [InspectorName("冷却条跟随平滑速度")]
        [Tooltip("热量是「每发一档」的阶梯（热量武器一发 4.5%），直接赋值会让条子跳格；0 = 不平滑（旧行为）。文本和蓄力条不受影响")]
        [SerializeField]
        float CoolRatioSmoothSpeed = 12f;

        WeaponController m_weapon;

        /// <summary>上一帧写进 Image 的填充量（0~1）：只在变化时才写，别每帧标脏 Canvas</summary>
        private float _lastCoolRatio = -1f;

        /// <summary>上一次写进文字的热量百分比：没变就跳过 SetText（省掉每帧的 int→string）</summary>
        private int _lastHeatPercent = int.MinValue;

        /// <summary>平滑后的显示用热量（0=冷，1=满热）：填充与取色共用同一个值，
        /// 否则会出现"条子还在慢慢走、颜色已经跳到下一档"的错位。</summary>
        private float _displayedHeat = -1f;

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

            if (T_CoolRatio.Count > 0)
            {
               
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
                // 百分比没变就不写：原来每帧 ForEach(lambda) 建闭包 + 每个 item 一次 ""+int（各分配一个字符串）
                int heatPercent = (int)(100 * heat);
                if (heatPercent != _lastHeatPercent)
                {
                    _lastHeatPercent = heatPercent;
                    string percentText = heatPercent.ToString();   // 只分配一次，所有条共用
                    for (int i = 0; i < T_CoolRatio.Count; i++)
                    {
                        var item = T_CoolRatio[i];
                        if (item) item.text = percentText;
                    }
                }

            }
            if (I_CoolRatio.Count > 0)
            {

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

                // 阶梯跳变先过一道帧率无关阻尼（热量的离散档位、射击间隔的 20ms 步进都吃这个）
                if (_displayedHeat < 0f) _displayedHeat = heat;
                else if (CoolRatioSmoothSpeed > 0f)
                    _displayedHeat = Mathf.Lerp(_displayedHeat, heat, 1f - Mathf.Exp(-CoolRatioSmoothSpeed * Time.deltaTime));
                else _displayedHeat = heat;

                // 反转只动填充方向，取色永远按热量 ⇒ 红不红跟这个开关无关
                float fill = ReverseCoolRatioFill ? _displayedHeat : 1f - _displayedHeat;

                // 平滑到贴近目标后就不再写：静止时不标脏 Canvas（平滑过程本身每帧要写）
                if (Mathf.Abs(fill - _lastCoolRatio) < 0.001f) return;
                _lastCoolRatio = fill;

                // ⚠ Gradient 是类，Unity 会给"没序列化的字段"补一个白色默认值 ⇒ 只能用显式开关，不能靠 null 判断
                bool tinted = TintCoolRatio && CoolRatioGradient != null;
                Color tint = tinted ? CoolRatioGradient.Evaluate(_displayedHeat) : default;
                for (int i = 0; i < I_CoolRatio.Count; i++)
                {
                    var image = I_CoolRatio[i];
                    if (!image) continue;   // 列表里留空槽是常见的（只填部分 HUD）
                    image.fillAmount = fill;
                    if (tinted) image.color = tint;
                }
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
