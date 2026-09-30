
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.Game
{
    /// <summary>
    /// 非玩家非敌人的生命实现（建筑、道具等）。
    /// </summary>
    [AddComponentMenu("生命/其它生命")]
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
            if (!wasDead&&m_IsDead && AutoDestroy) Destroy();
        }
        //检视器用
        public void Destroy()
        {
            Tool.Destroy(gameObject);
        }
    }
}
