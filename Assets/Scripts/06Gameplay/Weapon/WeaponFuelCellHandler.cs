
using FPSGame.Game;
using UnityEngine;

namespace FPSGame.Weapon
{
    //过热的位置
    [RequireComponent(typeof(WeaponController))]
    [AddComponentMenu("武器/武器热量表现")]
    public class WeaponFuelCellHandler : MonoBehaviour
    {
        [InspectorName("同时收回所有燃料电池")]
        public bool SimultaneousFuelCellsUsage = false;

        [InspectorName("表示武器上燃料电池的GameObject列表")]
        public GameObject[] FuelCells;

        [InspectorName("使用时电池的局部位置")]
        public Vector3 FuelCellUsedPosition;

        [InspectorName("使用前电池的局部位置")]
        public Vector3 FuelCellUnusedPosition = new Vector3(0f, -0.1f, 0f);

        [InspectorName("跟随平滑速度")]
        [Tooltip("越大跟得越快；0 = 旧行为（比例直接当目标值，跳变原样传到电池上）")]
        public float SmoothSpeed = 12f;

        WeaponController m_Weapon;
        bool[] m_FuelCellsCooled;

        /// <summary>当前显示用的比例。原始比例是**跳变**的（每发热量/每发弹匣都是一档一档走），
        /// 直接塞进 Lerp 就是"一顿一顿" ⇒ 先用帧率无关的指数阻尼追它。</summary>
        float m_DisplayedScale;

        /// <summary>首帧直接把电池摆到位，避免第一次 Update 从默认值滑一段。</summary>
        bool m_ScaleInited;

        void Start()
        {
            m_Weapon = GetComponent<WeaponController>();
            DebugUtility.HandleErrorIfNullGetComponent<WeaponController, WeaponFuelCellHandler>(m_Weapon, this,
                gameObject);

            m_FuelCellsCooled = new bool[FuelCells.Length];
            for (int i = 0; i < m_FuelCellsCooled.Length; i++)
            {
                m_FuelCellsCooled[i] = true;
            }
        }

        /// <summary>插值源统一成"剩余量"：0 = 打空/满热，1 = 满弹/冷。
        /// <para>⚠ 热量武器必须走热量：它的 <c>Magazine</c> 只是能量池，且散热时不会回升
        /// ⇒ 拿弹匣比例插值会让电池单向沉底，跟"过热→冷却"循环完全脱节。</para></summary>
        float GetRemainScale()
        {
            return m_Weapon.HasHeatSystem ? 1f - m_Weapon.HeatRatio : m_Weapon.Magazine.ScaleValue.RawFloat;
        }

        void Update()
        {
            var target = GetRemainScale();
            if (!m_ScaleInited)
            {
                m_DisplayedScale = target;
                m_ScaleInited = true;
            }
            else if (SmoothSpeed > 0f)
            {
                // 指数阻尼：与帧率无关（1 - e^(-k·dt)），比 Mathf.Lerp 固定系数稳
                m_DisplayedScale = Mathf.Lerp(m_DisplayedScale, target, 1f - Mathf.Exp(-SmoothSpeed * Time.deltaTime));
            }
            else
            {
                m_DisplayedScale = target;
            }

            if (SimultaneousFuelCellsUsage)
            {
                for (int i = 0; i < FuelCells.Length; i++)
                {
                    FuelCells[i].transform.localPosition = Vector3.Lerp(FuelCellUsedPosition, FuelCellUnusedPosition,
                        m_DisplayedScale);
                }
            }
            else
            {
                // TODO: needs simplification
                for (int i = 0; i < FuelCells.Length; i++)
                {
                    float length = FuelCells.Length;
                    float lim1 = i / length;
                    float lim2 = (i + 1) / length;

                    float value = Mathf.InverseLerp(lim1, lim2, m_DisplayedScale);
                    value = Mathf.Clamp01(value);

                    FuelCells[i].transform.localPosition =
                        Vector3.Lerp(FuelCellUsedPosition, FuelCellUnusedPosition, value);
                }
            }
        }
    }
}