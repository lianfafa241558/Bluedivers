using System.Collections.Generic;
using FPSGame.Core;
using PEMaths;
using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 伤害结算参数包（<see cref="IHealth.TakeDamage"/> 的唯一入参）。
    /// <para>▍为什么不直接留 9 个位置参数：尾部 4 个原本是 <c>true,false,-1,xxx</c> 这类无名字面量，
    /// <c>response</c> / <c>isWeakness</c> 两个相邻 bool 极易写反还编译得过。</para>
    /// <para>⚠ 默认值陷阱：<c>default(DamageSettleArgs)</c> 的 <see cref="Response"/> 是 <c>false</c>（原方法默认 <c>true</c>）
    /// ⇒ 一律用 <see cref="Of"/> 构造，别裸 <c>new DamageSettleArgs{}</c>，否则受击表现与 AI 反应会静默消失。</para>
    /// </summary>
    public struct DamageSettleArgs
    {
        /// <summary>伤害成分（类型 + **每个成分的数值**，不是系数）。
        /// <para>▍本地包：由 <c>Damageable.InflictDamage</c> 按"伤害 × 系数"拆好；远端包：直接就是线上
        /// <c>DamageMsg.Groups</c> 里那份数值（2026-11-04 改口径，两端因此不需要再分摊）。</para>
        /// <para>含 <c>Destruction</c>（破甲已在 <c>Damageable</c> 落地，<c>Health</c> 对它返回 0）。</para></summary>
        public List<SKVP<DamageTypeEnum, PEInt>> Groups;
        /// <summary>无源伤害（下游不当作"有人打我"处理）</summary>
        public bool NoSource;
        /// <summary>**起源**（统计口径）：击杀归属 / 命中次数该记在谁头上</summary>
        public GameObject Origin;
        /// <summary>**直接来源**（显示 / AI 口径）：真正发出这一发的东西；null ⇒ 回落 <see cref="Origin"/></summary>
        public GameObject Caused;
        /// <summary>受击碰撞体（算受击法线用；网络伤害没有真实命中体，允许 null）</summary>
        public Collider Affected;
        /// <summary>受击点</summary>
        public Vector3 Pos;
        /// <summary>是否派发受击响应（OnHit / 弹痕 / AI 反应）</summary>
        public bool Response;
        /// <summary>是否弱点命中（表现用）</summary>
        public bool IsWeakness;
        /// <summary>拆毁值：≥ 目标自身拆毁值则秒杀；-1 = 不带拆毁</summary>
        public int DemolishValue;
        /// <summary>本发的**成分数值**（类型 + 每个成分拆解后的量）：结算期间由 Health 暴露为 <c>LastRawGroups</c>，供联机上报。
        /// <remarks>与 <see cref="Groups"/> 内容相同（同一个列表对象），但它**只表示"可以上报"**：流血 / 异常状态 tick
        /// 这类直接调 <c>TakeDamage</c> 的路径置 null ⇒ 接收端退回中性成分（不会顺手给积蓄槽补一次）。</remarks>
        public List<SKVP<DamageTypeEnum, PEInt>> RawGroups;
        /// <summary>是否跳弹（直击且穿甲高于部位护甲 2 级以上）：只给准星提示用，不参与伤害数值。</summary>
        public bool Ricochet;

        /// <summary>常用路径：只给核心 5 项，其余取原方法默认值（response=true / isWeakness=false / demolish=-1）。</summary>
        /// <remarks>要改额外开关时先取包再逐字段赋值（C# 9 没有 <c>with</c>）。</remarks>
        public static DamageSettleArgs Of(List<SKVP<DamageTypeEnum, PEInt>> groups, bool noSource,
            GameObject origin, Collider affected, Vector3 pos)
        {
            return new DamageSettleArgs
            {
                Groups = groups,
                NoSource = noSource,
                Origin = origin,
                Caused = origin,
                Affected = affected,
                Pos = pos,
                Response = true,
                IsWeakness = false,
                DemolishValue = -1,
                RawGroups = null,
                Ricochet = false,
            };
        }
    }
}
