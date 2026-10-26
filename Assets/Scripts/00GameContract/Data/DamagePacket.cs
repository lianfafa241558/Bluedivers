using System.Collections.Generic;
using FPSGame.Core;
using PEMaths;
using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 伤害结算参数包（纯数据传输结构体，避免 InflictDamage 长参数列表）
    /// </summary>
    public struct DamagePacket
    {
        /// <summary>基础伤害</summary>
        public PEInt Damage;
        /// <summary>伤害成分（类型+系数）</summary>
        public List<SKVP<DamageTypeEnum, float>> DamageGroups;
        /// <summary>弱点加成</summary>
        public PEInt WeaknessBonus;
        /// <summary>穿甲等级（对所有伤害类型统一有效）</summary>
        public int AP;
        /// <summary>无源伤害</summary>
        public bool NoSource;
        /// <summary>**起源**（统计口径）：这笔伤害该记在谁头上（武器持有者 / 召唤物的呼叫者）。</summary>
        public GameObject DamageOrigin;
        /// <summary>**直接来源**（显示 / AI 口径）：真正发出这一发的东西（玩家本人的枪 / 炮台本身 / 地雷）。
        /// <para>null ⇒ 消费方回落 <see cref="DamageOrigin"/>（兼容老路径）。</para></summary>
        public GameObject DamageCaused;
        /// <summary>伤害位置</summary>
        public Vector3 Pos;
        /// <summary>拆毁值（大于目标 Health 拆毁值则秒杀）</summary>
        public int DemolishValue;
        /// <summary>是否直击</summary>
        public bool isDirect;
        /// <summary>击中的碰撞体 </summary>
        public Collider damageAffected;

        /// <summary>
        /// **伤害是否已结算过减伤乘区**（爆炸抗性 × 穿甲）。
        ///
        /// <para>▍联机镜像专用（2026-10-11 修）：开枪端上报的 <c>Damage</c> 取自受伤回调
        /// （<c>EnemyController._OnDamaged</c>）⇒ 它**已经是乘完护甲/爆炸抗性的最终值**。
        /// 远端落地时若再走一遍那两个乘区，等于把护甲**减两次** ⇒ 两端血量/死亡判定不一致
        /// （表现：客机打中、主机端那只怪还剩不少血）。置 true ⇒ <c>Damageable.InflictDamage</c> 跳过乘区、
        /// 按上报值原样扣。</para>
        /// </summary>
        public bool Resolved;

        /// <summary>本端**本地生成**的效果伤害（毒烟/火焰 tick、地雷自爆、空投舱砸击、踩踏动能、荆棘反伤）。
        /// <para>▍为什么要标：这些东西**每个端各自生成一份、各自结算**，两端都落地 = N 倍伤害
        /// （见 <c>topics/net-dup-damage.md</c>）。</para>
        /// <para>▍口径：**非权威端只对玩家/盟友落地**（他们的血本机权威；且盟友实例在别人机器上没有
        /// <c>Damageable</c>，房主替他们扣不了血），其余目标等房主结算后的伤害广播。
        /// 门只有一处：<c>Damageable.InflictDamage</c>。</para></summary>
        public bool LocalEffect;
    }
}
