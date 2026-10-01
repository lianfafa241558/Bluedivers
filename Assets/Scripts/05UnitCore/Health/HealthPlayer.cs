
using FPSGame.Core;
using FPSGame.Attributes;
using PEMaths;

using UnityEngine;
using FPSGame.GameContract;
namespace FPSGame.Game
{
    /// <summary>
    /// 玩家生命组件：在 <see cref="Health"/> 之外增加「生命缓慢自回」与「护盾恢复」。
    /// <para>
    /// 护盾恢复与护盾音效已统一到 <see cref="ShieldBehaviour"/>（不再与 HealthShield 各写一份）：
    /// 本组件在运行时自动挂载它，并把下面这些 Inspector 配置注入进去，
    /// 因此玩家 prefab 上的原有字段与数值保持不变。
    /// </para>
    /// </summary>
    public class HealthPlayer : Health
    {

        [InspectorName("生命恢复范围")] public float HealthRestoreScale;
        [InspectorName("生命恢复速度")] public float HealthRestoreSpeed;

        // ↓ 以下护盾配置保留在玩家组件上（prefab 数据不丢），运行时注入给 ShieldBehaviour
        [InspectorName("护盾恢复速度")] public float ShieldRestoreSpeed;
        [InspectorName("护盾恢复延迟")] public float ShieldDelay;

        [SerializeField] [InspectorName("护盾恢复音效")] AudioClip ShieldRestore;
        [SerializeField] [InspectorName("护盾回满音效")] AudioClip ShieldFull;
        [SerializeField] [InspectorName("护盾破碎音效")] AudioClip ShieldBreak;
        [SerializeField] [InspectorName("护盾受击音效")] AudioClip[] ShieldDamage; 

        private ShieldBehaviour m_Shield;

        protected override void Awake() {
            base.Awake();
            if (FPSGame.Data.BattleState.HaveBooster(BoosterType.Shield)) MaxShield += 10;
            CurrentShield = MaxShield;

            // 护盾恢复/音效统一交给 ShieldBehaviour（运行时挂载 + 注入配置，不改 prefab 数据）
            m_Shield = GetComponent<ShieldBehaviour>();
            if (m_Shield == null) m_Shield = gameObject.AddComponent<ShieldBehaviour>();
            m_Shield.Configure(ShieldRestoreSpeed, ShieldDelay, ShieldRestore, ShieldFull, ShieldBreak, ShieldDamage);

            if (HealthRestoreSpeed > 0)
            {
                InvokeRepeating(nameof(RestoreHealth), Constants.LoginFrame.RawFloat, Constants.LoginFrame.RawFloat);
            }
        }

        /// <summary>生命缓慢自回（护盾恢复见 <see cref="ShieldBehaviour"/>）</summary>
        private void RestoreHealth() {
            if (m_IsDead) return;
            if (HealthRestoreSpeed > 0 && GetHpRatio() < HealthRestoreScale) {
                CurrentHealth += (PEInt)(Time.deltaTime * HealthRestoreSpeed);
            }
        }

        /// <summary>复活</summary>
        public override void Revive()
        {
            base.Revive();
            CurrentHealth = Mathf.Max(MaxHealth/10,1);
            CurrentShield = 0;
        }

    }
}
