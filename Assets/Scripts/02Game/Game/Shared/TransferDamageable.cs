using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;
using PEMaths;

using UnityEngine;
namespace Unity.FPS.Game
{

    /// <summary>
    /// 附加肢体(可独立配置护甲等级与爆炸抗性)
    /// </summary>
    [AddComponentMenu("单位/附属肢体", 30)]
    public class TransferDamageable : MonoBehaviour, I_Damagable 
    {

        [SerializeField]
        [InspectorName("弱点")]
        private bool isWeakness;

        /// <summary>独立护甲等级（由穿甲等级AP判定减伤）</summary>
        [SerializeField]
        [InspectorName("护甲等级")]
        private int armorLevel;

        [HideInInspector]
        public Damageable source;

        bool I_Damagable.IsWeakness => isWeakness;

        public I_Damagable Source => source;

        public GameObject ActorGo => Source.ActorGo;

        public int ArmorLevel => armorLevel;

        public float ExplosionResistance => source.ExplosionResistance;

        public void InflictDamage(DamagePacket packet)
        {
            Source.InflictDamage(packet);
        }

    }
}