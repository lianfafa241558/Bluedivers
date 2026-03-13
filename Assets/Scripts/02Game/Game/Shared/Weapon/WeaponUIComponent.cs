using System.Collections;
using System.Collections.Generic;
using PEMaths;
using Unity.BaseTool;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace Unity.FPS.Game
{

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

        WeaponController m_weapon;

        private void Awake()
        {
            m_weapon = GetComponent<WeaponController>();
            m_weapon.OnChargeRatioUpdate += ChargeRatioUI;
            m_weapon.OnUIUpdate += UpdateUI;
        }
        private void OnDestroy()
        {
            m_weapon.OnChargeRatioUpdate -= ChargeRatioUI;
            m_weapon.OnUIUpdate -= UpdateUI;
        }

        void Update()
        {
            var fill = m_weapon.ShootInterval.ScaleValue.RawFloat;
            for (int i = 0; i < I_CoolRatio.Count; i++)
            {
                I_CoolRatio[i].fillAmount = fill;
            }
            //↓Lambda + ForEach 会导致大量GC
            //I_CoolRatio.ForEach(item => item.fillAmount = (Time.time - m_LastTimeShot) / DelayBetweenShots);

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
            I_ChargeRatio.ForEach(item => item.fillAmount = value);
        }


    }
}