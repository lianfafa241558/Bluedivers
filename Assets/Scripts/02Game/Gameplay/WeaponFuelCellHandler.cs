
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    //过热的位置
    [RequireComponent(typeof(WeaponController))]
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

        WeaponController m_Weapon;
        bool[] m_FuelCellsCooled;

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

        void Update()
        {
            if (SimultaneousFuelCellsUsage)
            {
                for (int i = 0; i < FuelCells.Length; i++)
                {
                    FuelCells[i].transform.localPosition = Vector3.Lerp(FuelCellUsedPosition, FuelCellUnusedPosition,
                        m_Weapon.Magazine.ScaleValue.RawFloat);
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

                    float value = Mathf.InverseLerp(lim1, lim2, m_Weapon.Magazine.ScaleValue.RawFloat);
                    value = Mathf.Clamp01(value);

                    FuelCells[i].transform.localPosition =
                        Vector3.Lerp(FuelCellUsedPosition, FuelCellUnusedPosition, value);
                }
            }
        }
    }
}