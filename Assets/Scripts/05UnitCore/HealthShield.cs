
using FPSGame.Core;
using FPSGame.Attributes;
using PEMaths;

using UnityEngine;
using FPSGame.GameContract;
namespace FPSGame.Game
{
    /// <summary>
    /// 非玩家单位的护盾组件：为挂载它的单位提供护盾自动恢复与配套音效。
    /// <para>
    /// 护盾恢复/音效逻辑已统一到 <see cref="ShieldBehaviour"/>（不再与 HealthPlayer 各写一份）：
    /// 本组件在运行时自动挂载它，并把下面这些 Inspector 配置注入进去，
    /// 因此 prefab（ShieldBag / Dec_Colossus Boss / ArtifactPlane）上的原有字段与数值不受影响。
    /// </para>
    /// </summary>
    public class HealthShield : Health
    {
        // ↓ 配置保留在本组件上（prefab 数据不丢），运行时注入给 ShieldBehaviour
        [InspectorName("护盾恢复速度")] public float ShieldRestoreSpeed;
        [InspectorName("护盾恢复延迟")] public float ShieldDelay;

        [SerializeField][InspectorName("护盾恢复音效")] AudioClip ShieldRestore;
        [SerializeField][InspectorName("护盾回满音效")] AudioClip ShieldFull;
        [SerializeField][InspectorName("护盾破碎音效")] AudioClip ShieldBreak;
        [SerializeField][InspectorName("护盾受击音效")] AudioClip[] ShieldDamage;

        private ShieldBehaviour m_Shield;

        protected override void Awake()
        {
            base.Awake();
            if (FPSGame.GameContract.ServiceLocator.Battle != null && FPSGame.GameContract.ServiceLocator.Battle.HaveBooster(BoosterType.Shield)) MaxShield += 10;
            CurrentShield = MaxShield;

            // 护盾恢复/音效统一交给 ShieldBehaviour（运行时挂载 + 注入配置，不改 prefab 数据）
            m_Shield = GetComponent<ShieldBehaviour>();
            if (m_Shield == null) m_Shield = gameObject.AddComponent<ShieldBehaviour>();
            m_Shield.Configure(ShieldRestoreSpeed, ShieldDelay, ShieldRestore, ShieldFull, ShieldBreak, ShieldDamage);
        }


        //protected override void HandleDeath() {
        //    base.HandleDeath();
        //}

    }
}
