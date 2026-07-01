using System.Collections.Generic;
using System.Linq;
using Core;
using GameContract;
using PEMaths;

using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.Game
{
 

    /// <summary>
    /// 可以被攻击 没有这个组件就不造成伤害)
    /// </summary>
    public class Damageable : TickBehaviour, I_Damagable
    {
        /// <summary>友伤倍率</summary>
        private const float SensibilityToSelfdamage = 0.5f;

        [SerializeField]
        [InspectorName("弱点")]
        private bool isWeakness;

        [Header("伤害抗性")]
        [InspectorName("伤害抗性")]
        [SerializeField]
        private List<KVP<DamageTypeEnum, float>> showArmorLists;

        [SerializeField]
        [InspectorName("全抗性")]
        private float AllArmor;


        [InspectorName("基础护甲值")]
        public int armorValue;
        [Header("关联护甲")]
        [SerializeField]
        private List<TransferDamageable> LineArmor;

        [Header("护甲破坏效果")]
        [SerializeField]
        private List<ArmorBreakEffect> armorBreakEffect;
        [InspectorName("流血速度")]
        public int BleedValue;

        public event UnityAction<Damageable> OnDamage;
        public event UnityAction OnDestroyPart;
        public Health Health { get; private set; }
        private Actor Actor { get; set; }



        /// <summary>护甲破坏(计算流血)</summary>
        private GameObject ArmorBreaker;

        bool I_Damagable.IsWeakness => isWeakness;

        GameObject I_Damagable.ActorGo => Actor.gameObject;

        public I_Damagable Source => this;



        public float GetArmor(DamageTypeEnum type)
        {
            if (armors.TryGetValue(type,out var re))
            {
                return re;
            }
            return 0;
        }

        private Dictionary<DamageTypeEnum, float> armors;
        /// <summary>
        /// 主体一定过爆炸判定
        /// </summary>
        [HideInInspector]
        public bool isMain;


        private Collider[] m_colliders;//包括关联护甲

       
        public PEInt remainArmor;

        [DisplayField]
        [SerializeField]
        private float showArmor;

        public float GetArmorRatio() => remainArmor.RawFloat / armorValue;

        void Awake()
        {
            remainArmor = armorValue;
            showArmor = armorValue;
            List<Collider> list = GetComponents<Collider>().ToList();
            foreach (var item in LineArmor)
            {
                item.source = this;
                list.AddRange(item.GetComponents<Collider>());
            }
            m_colliders = list.ToArray();
            //在层次结构中查找处于同一级别或更高级别的组件
            Health = GetComponent<HealthEnemy>();
            if (!Health)
            {
                Health = GetComponentInParent<Health>();
            }
            Actor = GetComponent<Actor>();
            if (!Actor)
            {
                Actor = GetComponentInParent<Actor>();
            }
            armors = new();
            foreach (DamageTypeEnum type in System.Enum.GetValues(typeof(DamageTypeEnum)))
            {
                armors.Add(type,1);
            }
            //比如护甲里面填0.5，收到的伤害-50%
            foreach (var item in showArmorLists)
            {
                armors[item.Key]-=item.Value-AllArmor;
            }
        }

        public Collider ClosestCollider(Vector3 pos) {
            if (m_colliders.Length == 0)
            {
                Debug.LogError("没有碰撞组件"+gameObject.name,gameObject);
                return null;
            }
            if (m_colliders.Length == 1) return m_colliders[0];
            var closest = m_colliders[0];
            //Debug.LogError("可伤害组件"+gameObject+"的碰撞数量"+ m_colliders.Length);
            float minDistance = Vector3.Distance(pos, closest.ClosestPointOnBounds(pos));
            for (int i = 1; i < m_colliders.Length; i++) {
                float currentDistance = Vector3.Distance(pos, m_colliders[i].ClosestPointOnBounds(pos));
                if (currentDistance < minDistance) {
                    minDistance = currentDistance;
                    closest = m_colliders[i];
                }
            }
            return closest;
        }

        /// <summary>
        /// 检查自己是否被遮挡(爆炸判定)
        /// </summary>
        /// <param name="sourcePoint">来源</param>
        /// <returns>是否被遮挡</returns>
        public bool ExplosionBlocking(Vector3 sourcePoint,out Collider collider)
        {
            if (isMain)//主体一定过判定
            {
                collider = m_colliders[0];
                return true;
            }
            collider = null;
            //爆炸免疫的直接跳过
            if (armors[DamageTypeEnum.Explosion] <= 0) return false;

            RaycastHit hit;
            // 批量射线检查
            foreach (var item in m_colliders)
            {
                Vector3 direction = item.bounds.center - sourcePoint;
                if (!Physics.Raycast(sourcePoint, direction, out hit, direction.magnitude))
                {
                    // 射线未命中任何物体，按理来说不该由这个情况，但是应该算没有阻挡
                    collider = item;
                    return true;
                }
                else if (hit.collider == item)
                {
                    // 射线命中目标Collider，未被遮挡
                    collider = item;
                    return true;
                }
                // 其他情况都被遮挡
            }

            return false;
        }




        public void InflictDamage(I_Damagable source, PEInt damage, List<SKVP<DamageTypeEnum,float>> damageGroups,bool noSource, GameObject damageSource,Vector3 pos) {
            if (!Health) return;

            Actor SourceActor=null;
            if (damageSource)
            {
                SourceActor = damageSource.GetComponent<Actor>();
            }

            // 友军减伤
            if (Actor && SourceActor && Actor.Team == SourceActor.Team)
            {
                //Debug.LogWarning("友军减伤"+ damage+" "+ damage * SensibilityToSelfdamage);
                damage *= (PEInt)SensibilityToSelfdamage;
            }
            if (isWeakness)
            {
                damage *= (1+(PEInt)damageGroups.GetValue(DamageTypeEnum.Weakness));
            }

            List<KVP<DamageTypeEnum, PEInt>> finalDamageGroups = new();
            if (damageGroups.Count == 0) {
                finalDamageGroups.Add(new(DamageTypeEnum.Gun, (damage * (PEInt)source.GetArmor(DamageTypeEnum.Gun))));
            }
            else {
                foreach (var item in damageGroups) {
                    //基础伤害*伤害系数*抗性
                    PEInt value = (damage * (PEInt)item.Value * (PEInt)source.GetArmor(item.Key));
                    finalDamageGroups.Add(new(item.Key, value));
                    if (item.Key == DamageTypeEnum.Destruction) {
                        ArmorDestruction(value, damageSource);
                    }
                }
            }
            
            Health.TakeDamage(finalDamageGroups, noSource, damageSource, ClosestCollider(pos), pos);
            
        }

        //计算护甲破坏
        private void ArmorDestruction(PEInt value,GameObject source)
        {
            if (remainArmor > 0)
            {
                remainArmor -= value;
                showArmor = remainArmor.RawFloat;
                if (remainArmor<=0)
                {
                    foreach (var item in armorBreakEffect)
                    {
                        item.go.SetActive(item.state);
                        item.go.transform.localScale *= item.scale;
                    }
                    ArmorBreaker = source;
                    OnDestroyPart?.Invoke();
                }
                else
                {
                    OnDamage?.Invoke(this);
                }
            }

        }
        public override bool Tick()
        {
            if (armorValue>0&&remainArmor <= 0)
            {
                Health.TakeDamage(new() {new( DamageTypeEnum.Real,BleedValue)}, true, ArmorBreaker, m_colliders[0], m_colliders[0].bounds.center);
            }
            return true;
        }

#if UNITY_EDITOR
        [ContextMenu("自动配置")]
        private void AutoSetting()
        {
            LineArmor = transform.GetComponentsInChildren<TransferDamageable>().ToList();
            if (armorValue > 0) armorBreakEffect.Add(new() { go=gameObject});
        }

#endif


    }
    [System.Serializable]
    public class ArmorBreakEffect {
        public GameObject go;
        public bool state;
        public float scale;

    }

}