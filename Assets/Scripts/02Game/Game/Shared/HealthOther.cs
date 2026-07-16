using Unity.BaseTool;
using UnityEngine;
using Utils;

namespace Unity.FPS.Game
{
    public class HealthOther : Health
    {
        /// <summary>
        /// 死亡后自动摧毁自身(物体)
        /// </summary>
        [InspectorName("死亡后自动摧毁自身(物体)")]
        public bool AutoDestroy;
        protected override void Start()
        {
            base.Start();
        }
        protected override void HandleDeath(GameObject source)
        {
            base.HandleDeath(source);
            if (!m_IsDead&& AutoDestroy) Tool.Destroy(gameObject);
        }
    }
}