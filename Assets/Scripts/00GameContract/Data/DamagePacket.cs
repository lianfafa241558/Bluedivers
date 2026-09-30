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
        /// <summary>伤害来源</summary>
        public GameObject DamageSource;
        /// <summary>伤害位置</summary>
        public Vector3 Pos;
        /// <summary>拆毁值（大于目标 Health 拆毁值则秒杀）</summary>
        public int DemolishValue;
        /// <summary>是否直击</summary>
        public bool isDirect;
        /// <summary>击中的碰撞体 </summary>
        public Collider damageAffected;

    }
}
