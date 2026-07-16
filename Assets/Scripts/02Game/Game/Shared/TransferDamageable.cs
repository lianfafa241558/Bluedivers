using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;
using PEMaths;
using Unity.BaseTool;
using UnityEngine;
namespace Unity.FPS.Game
{
    /// <summary>
    /// 附加肢体(在这种类型的组件下填写爆炸抗性没用)
    /// </summary>
    public class TransferDamageable : MonoBehaviour, I_Damagable 
    {

        [SerializeField]
        [InspectorName("弱点")]
        private bool isWeakness;

        [HideInInspector]
        public Damageable source;

        bool I_Damagable.IsWeakness => isWeakness;

        public I_Damagable Source => source;

        public GameObject ActorGo => Source.ActorGo;

        [Header("伤害抗性")]
        [InspectorName("伤害抗性")]
        [SerializeField]
        private DisplayDic<DamageTypeEnum, float> extraLists;


        public float GetArmor(DamageTypeEnum type)
        {
            extraLists.TryGet(type, out  var re);
            if(!source.IsValid()) Debug.LogError("物体"+gameObject.name+"没有关联根组件",gameObject);
            return source.GetArmor(type)+re;
        }

        public void InflictDamage(I_Damagable source, PEInt damage, List<KVP<DamageTypeEnum, float>> damageGroups, bool noSource, GameObject damageSource, Vector3 pos)
        {
            Source.InflictDamage(source, damage, damageGroups, noSource, damageSource, pos);
        }


    }
}