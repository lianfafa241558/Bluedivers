using UnityEngine;

namespace FPSGame.DayNightSystem
{
    /// <summary>
    /// 昼夜系统的大脑，负责管理所有的昼夜模块
    /// </summary>
    [AddComponentMenu("昼夜系统/昼夜系统核心")]
    public class DayNightBrain : MonoBehaviour
    {
        [InspectorName("昼夜状态")]
        [SerializeField] private DayNightState state = new DayNightState();

        private IDayNightModule[] _modules;

        private void Awake()
        {
            _modules = GetComponentsInChildren<IDayNightModule>();

            foreach (var module in _modules)
            {
                module.Initialize(state);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (var module in _modules)
            {
                module.Tick(state, dt);
            }
        }

        private void OnDestroy()
        {
            if (_modules == null) return;
            foreach (var module in _modules)
            {
                module.Dispose();
            }
        }
    }
}