using UnityEngine;

namespace FPSGame.DayNightSystem
{
    /// <summary>
    /// 控制太阳和星星的旋转，模拟昼夜变化
    /// </summary>
    [AddComponentMenu("昼夜系统/天体旋转模块")]
    public class CelestialRotationModule : MonoBehaviour, IDayNightModule
    {

        [InspectorName("旋转的根物体")][SerializeField] private Transform rotator;
        [InspectorName("旋转的星体物体")][SerializeField] private Transform starsRotator;

        [InspectorName("星体额外旋转速度")][SerializeField] private float starsSpeed = 0f;

        private float _starsCurrentAngle;

        public void Initialize(DayNightState state)
        {
            if (rotator != null) 
                state.CurrentAngle = rotator.eulerAngles.z;
        }

        public void Tick(DayNightState state, float deltaTime)
        {
            if (rotator != null)
            {
                rotator.localRotation = Quaternion.Euler(0f, 0f, state.CurrentAngle);
            }

            if (starsRotator != null)
            {
                _starsCurrentAngle += starsSpeed * deltaTime;
                starsRotator.localRotation = Quaternion.Euler(0f, 0f, _starsCurrentAngle);
            }
        }

        public void Dispose() { }
    }
}