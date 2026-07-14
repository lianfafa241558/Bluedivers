using UnityEngine;

namespace FPSGame.DayNightSystem
{
    /// <summary>
    /// 修改昼夜系统的时间进度，控制昼夜循环的速度和时间分配
    /// </summary>
    [AddComponentMenu("昼夜系统/时间进度模块")]
    public class TimeProgressionModule : MonoBehaviour, IDayNightModule
    {
        public void Initialize(DayNightState state) {

            System.DateTime now = System.DateTime.Now;
            float minuteSecond = 60 * now.Minute + now.Second;
            // 1800取模，作为初始时间
            state.CurrentAngle = (minuteSecond % state.CycleDurationSecond) / state.CycleDurationSecond*360;

        }

        public void Tick(DayNightState state, float deltaTime)
        {
            float totalSeconds = state.CycleDurationSecond;
            
            // Rule of thirds: 2/3 day, 1/3 night
            float dayDuration = totalSeconds * (2.0f / 3.0f);
            float nightDuration = totalSeconds * (1.0f / 3.0f);

            float speed = state.IsDaytime 
                ? (180f / dayDuration) 
                : (180f / nightDuration);

            state.CurrentAngle += speed * deltaTime;

            // Keep angle between 0 and 360
            if (state.CurrentAngle >= 360f) 
                state.CurrentAngle -= 360f;
        }

        public void Dispose() { }
    }
}