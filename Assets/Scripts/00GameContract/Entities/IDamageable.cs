using FPSGame.Core.Interface;
using UnityEngine;

namespace FPSGame.GameContract
{
    public interface IDamageable : IMonoVaild
    {
        GameObject gameObject { get; }
        IDamageable Source { get; }
        /// <summary>护甲等级（绝地潜兵2式，由攻击方穿甲等级 AP 判定减伤）</summary>
        int ArmorLevel { get; }
        /// <summary>爆炸抗性（0~1，1=完全免疫）</summary>
        float ExplosionResistance { get; }

        public void InflictDamage(DamagePacket packet);
        GameObject ActorGo { get; }
        bool IsWeakness { get; }
    }
}
