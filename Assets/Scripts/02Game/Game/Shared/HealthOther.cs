
using UnityEngine;
using Utils;

namespace Unity.FPS.Game
{
    public class HealthOther : Health
    {
        /// <summary>
        /// 死亡后自动摧毁自身物体）
        /// </summary>
        [InspectorName("死亡后自动摧毁自身物体）")]
        public bool AutoDestroy;

        protected override void HandleDeath(GameObject source)
        {
            bool wasDead = m_IsDead;
            base.HandleDeath(source);
            if (!wasDead&&m_IsDead && AutoDestroy) Tool.Destroy(gameObject);
        }
    }
}