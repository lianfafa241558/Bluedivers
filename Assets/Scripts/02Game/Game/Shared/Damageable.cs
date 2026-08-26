using System.Collections.Generic;
using System.Linq;
using Core;
using FPSGame.Attribute;
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
    [AddComponentMenu("单位/肢体", 30)]
    public class Damageable : TickBehaviour, I_Damagable
    {
        /// <summary>友伤倍率</summary>
        private const float SensibilityToSelfdamage = 0.5f;
        /// <summary>爆炸抗性达到此值视为完全免疫（跳过爆炸遮挡判定）</summary>
        private const float ExplosionImmunityThreshold = 0.95f;
        /// <summary>穿甲等级不足时，每差 1 级减伤比例</summary>
        private const float ArmorPenaltyPerLevel = 0.33f;
        /// <summary>穿甲不足时伤害最低比例</summary>
        private const float ArmorMinFactor = 0.1f;


        [SerializeField]
        [InspectorName("弱点")]
        private bool isWeakness;

        /// <summary>爆炸抗性（0~1，1=完全免疫）</summary>
        [SerializeField]
        [InspectorName("爆炸抗性")]
        private float explosionResistance;

        /// <summary>护甲等级（绝地潜兵2式，由穿甲等级AP判定减伤）</summary>
        [SerializeField]
        [InspectorName("护甲等级")]
        private int armorLevel;

        
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
        public event UnityAction<Damageable> OnDestroyPart;
        public Health Health { get; private set; }
        private Actor Actor { get; set; }



        /// <summary>护甲破坏(计算流血)</summary>
        private GameObject ArmorBreaker;

        bool I_Damagable.IsWeakness => isWeakness;

        GameObject I_Damagable.ActorGo => Actor.gameObject;

        public I_Damagable Source => this;

        public int ArmorLevel => armorLevel;

        public float ExplosionResistance => explosionResistance;

        /// <summary>
        /// 主体一定过爆炸判定
        /// </summary>
        [HideInInspector]
        public bool isMain;


        private Collider[] m_colliders;//包括关联护甲

       
        public PEInt remainArmor;

        /// <summary>护甲满值（含难度缩放），用于护甲/护盾恢复</summary>
        private PEInt maxArmor;

        [DisplayField]
        [SerializeField]
        private float showArmor;

        public float GetArmorRatio() => remainArmor.RawFloat / armorValue;

        public void SetIsMain(Health health)
        {
            isMain = true;
            health.OnDie+= DestroyPart;
        }


        void Awake()
        {

            PEInt scale = 1 + (TaskManager.Instance.nowTask.ExtraDifficulty[3] * (PEInt)0.1f);
            switch (TaskManager.Instance.nowTask.difficulty)
            {
                case DifficultyEnum.Normal:
                    scale *= (PEInt)0.5f;
                    break;
                case DifficultyEnum.Hard:
                    scale *= (PEInt)0.6f;
                    break;
                case DifficultyEnum.VeryHard:
                    scale *= (PEInt)0.7f;
                    break;
                case DifficultyEnum.HardCode:
                    scale *= (PEInt)0.85f;
                    break;
                case DifficultyEnum.Extreme:
                    scale *= (PEInt)1f;
                    break;
                case DifficultyEnum.Insane:
                    scale *= (PEInt)1.15f;
                    break;
                case DifficultyEnum.Torment:
                    scale *= (PEInt)1.2f;
                    break;
                case DifficultyEnum.Lunatic:
                    scale *= (PEInt)1.35f;
                    break;
            }

            remainArmor = armorValue * scale;
            maxArmor = remainArmor;
            showArmor = remainArmor.RawFloat;
            List <Collider> list = GetComponents<Collider>().ToList();
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
            // 记录护甲破坏效果的初始状态，用于护盾恢复
            foreach (var item in armorBreakEffect)
            {
                if (item.go != null)
                {
                    item.originActive = item.go.activeSelf;
                    item.originLocalScale = item.go.transform.localScale;
                }
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
            //爆炸抗性接近完全免疫的直接跳过遮挡判定
            if (explosionResistance >= ExplosionImmunityThreshold) return false;

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




        public void InflictDamage(DamagePacket packet) {
            if (!Health) return;
            if (!packet.DamageGroups.IsValid()|| packet.DamageGroups.Count==0) return;

            PEInt damage = packet.Damage;
            GameObject damageSource = packet.DamageSource;
            //I_Damagable source = packet.Source;

            Actor SourceActor=null;
            if (damageSource)
            {
                SourceActor = damageSource.GetComponent<Actor>();
            }

            // 友军减伤
            if (Actor && SourceActor && Actor.Team == SourceActor.Team)
            {
                
                damage *= (PEInt)SensibilityToSelfdamage;
            }
            // 全队强化"友情护盾"：友军伤害再降低 50%（总友伤 25%，仅玩家队伍生效）
            if (Actor && SourceActor && Actor.Team == SourceActor.Team
                && IsFriendlyUnit(Actor)
                && BattleManager.Instance.HaveBooster(BoosterType.FriendShield))
            {
                damage *= (PEInt)0.5f;
            }
            // 全队强化"生命力强化"：玩家受到的伤害降低 10%
            if (BattleManager.Instance.HaveBooster(BoosterType.Vitality) && Actor && (Actor.Type== UnitTypeEnum.Player|| Actor.Type == UnitTypeEnum.Friend))
            {
                damage *= (PEInt)0.9f;
            }
            if (isWeakness)
            {
                damage *= (1+ packet.WeaknessBonus);
            }
            //Debug.LogWarning("友军减伤" + damage,gameObject);


            //爆炸抗性
            PEInt hitExplosionResistance = packet.isDirect ? 1 : (1 - (PEInt)explosionResistance);
            //Debug.LogWarning("爆炸抗性" + hitExplosionResistance, gameObject);

            //穿甲等级
            PEInt armorFactor = PEMath.Clamp(1 - (armorLevel - packet.AP) * (PEInt)ArmorPenaltyPerLevel, new(0.1f), 1);
            //Debug.LogWarning("穿甲系数" + armorFactor+"护甲等级"+armorLevel+"穿甲等级"+packet.AP, gameObject);
            damage *= hitExplosionResistance* armorFactor;

            //Debug.LogWarning("AP减伤" + damage, gameObject);

            List<SKVP<DamageTypeEnum, PEInt>> finalDamageGroups = new();
            if (packet.DamageGroups.Count == 0) {
                // 无成分时按动能(Gun)结算
                finalDamageGroups.Add(new(DamageTypeEnum.Gun, (damage)));
            }
            else {
                foreach (var item in packet.DamageGroups) {
                    PEInt value = (damage * (PEInt)item.Value);

                    if (item.Key == DamageTypeEnum.Destruction) {
                        ArmorDestruction(value, damageSource);
                    }
                    else
                    {
                        finalDamageGroups.Add(new(item.Key, value));
                    }
                }
            }
            
            Health.TakeDamage(finalDamageGroups, packet.NoSource, damageSource, ClosestCollider(packet.Pos), packet.Pos,true,isWeakness, packet.DemolishValue);
            
        }

        /// <summary>是否为玩家阵营单位（玩家/盟友）</summary>
        private static bool IsFriendlyUnit(Actor actor)
            => actor.Type == UnitTypeEnum.Player || actor.Type == UnitTypeEnum.Friend;

        //计算护甲破坏
        private void ArmorDestruction(PEInt value, GameObject source)
        {
            if (remainArmor > 0)
            {
                remainArmor -= value;
                showArmor = remainArmor.RawFloat;
                if (remainArmor <= 0)
                {
                    BreakArmor(source);
                }
                else
                {
                    OnDamage?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// 恢复护甲（护盾类肢体）：护甲回满并还原护甲破坏效果
        /// </summary>
        public void RestoreArmor()
        {
            if (remainArmor > 0) return;

            remainArmor = maxArmor;
            showArmor = remainArmor.RawFloat;
            ArmorBreaker = null;
            foreach (var item in armorBreakEffect)
            {
                if (item.go != null)
                {
                    item.go.SetActive(item.originActive);
                    item.go.transform.localScale = item.originLocalScale;
                }
            }
        }

        /// <summary>
        /// 摧毁护甲
        /// </summary>
        public void BreakArmor(GameObject source)
        {
            foreach (var item in armorBreakEffect)
            {
                if (item.go != null)
                {
                    item.go.SetActive(item.state);
                    item.go.transform.localScale *= item.scale;
                }
            }
            ArmorBreaker = source;
            OnDamage?.Invoke(this);
            DestroyPart(null);
        }

        private void DestroyPart(GameObject _)
        {
            OnDestroyPart?.Invoke(this);
        }




        public override bool Tick()
        {
            if (armorValue>0&&remainArmor <= 0)
            {
                Health.TakeDamage(new() {new( DamageTypeEnum.Real,BleedValue)}, true, ArmorBreaker, m_colliders[0], m_colliders[0].bounds.center,true);
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

        /// <summary>运行时记录破坏前的激活状态，用于护盾恢复</summary>
        [HideInInspector] public bool originActive;
        /// <summary>运行时记录破坏前的局部缩放，用于护盾恢复</summary>
        [HideInInspector] public Vector3 originLocalScale;
    }

}