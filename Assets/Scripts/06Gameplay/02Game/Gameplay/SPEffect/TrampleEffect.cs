using FPSGame.Core.Interface;
using FPSGame.GameContract;
using PEMaths;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 踩踏持续效果：每 TickInterval(默认 0.5s) 对范围内所有单位逐个结算一次动能伤害。
    /// - 动能而非爆炸：以 isDirect 直击包结算，绕开爆炸抗性与爆炸遮挡判定
    /// - 打击范围内任意单位(允许友伤)，仅排除自身
    /// - 每个单位只结算一次(取离踩踏中心最近的肢体)，跨跳可重复结算
    /// - 可选附带击退
    /// 结算骨架复用 <see cref="FpsHelper.HitAreaKinetic"/>。
    /// 生效时间由外部控制：技能(如 UnitSkill_Charge)冲锋期间激活挂点，或巨型单位脚部常驻。
    /// </summary>
    [AddComponentMenu("持续效果/踩踏")]
    public class TrampleEffect : SustainedEffect
    {
        [Header("击退")]
        /// <summary>击退冲量：目标受力速度增量 = 本值 / 其重量(无速度上限，重量越大推得越少)</summary>
        [InspectorName("击退力度(冲量,0=不击退)")]
        [SerializeField]
        private float KnockbackForce = 0f;
       
        [InspectorName("击退竖直倍率(1=保持原方向)")]
        [SerializeField]
        private float KnockbackUpScale = 1f;

        /// <summary>自身单位缓存(用于排除自己)</summary>
        I_Actor m_Self;

        protected override void OnEnable()
        {
            base.OnEnable();
            // 重新启用(技能激活/池化复用)时清空缓存，允许 Owner 变化后重新解析
            m_Self = null;
        }

        protected override void ApplyEffect()
        {
            I_Actor self = ResolveSelf();

            FpsHelper.HitAreaKinetic(new FpsHelper.KineticAreaHitData
            {
                data = DamageData,
                groups = DamageData.DamageGroupExplosion,//成分需配成 {动能,1}
                pos = (PEVector3)DamageAnchor.position,
                soure = self.IsValidMono() ? self.gameObject : Owner,
                exclude = self,
                filter = null,//允许友伤：不做队伍过滤
                knockbackForce = (PEInt)KnockbackForce,
                knockbackRadius = DamageData.GetShockwaveRadius(1),
                knockbackUpScale = (PEInt)KnockbackUpScale,
            });
        }

        /// <summary>解析自身单位：优先伤害来源 Owner，其次挂点所属单位(常驻脚部挂点)</summary>
        I_Actor ResolveSelf()
        {
            if (m_Self.IsValidMono()) return m_Self;
            if (Owner) m_Self = Owner.GetComponentInParent<I_Actor>();
            if (!m_Self.IsValidMono()) m_Self = GetComponentInParent<I_Actor>();
            return m_Self;
        }
    }
}
