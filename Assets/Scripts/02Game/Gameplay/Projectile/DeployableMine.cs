using Core;
using GameContract;
using PEMaths;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.VFX;
using Utils;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 地雷（自部署专用，替代 ProjectileMine）
    /// 纯 MonoBehaviour 自部署单位：不继承 ProjectileBase、不依赖武器体系，
    /// 由外部脚本 Instantiate 后调用 <see cref="Deploy(Vector3, GameObject)"/> 部署。
    /// 复用预制体上的 Actor/Health/LimitedLife 组件：
    /// - Actor 提供队伍(Team)，用于智能触发判定敌我
    /// - Health 提供被破坏时引爆(OnDie)以及触发引爆(Kill)
    /// - LimitedLife 提供超时回收
    /// </summary>
    [AddComponentMenu("单位/地雷", 30)]
    public class DeployableMine : MonoBehaviour
    {
        [Header("通用")]
        [InspectorName("根部变换")]
        [SerializeField]
        private Transform Root;
        [InspectorName("部署后启动延迟(秒)")]
        [SerializeField]
        private float DeployDelay = 2f;
        [InspectorName("触发范围")]
        [SerializeField]
        private float TriggerRange = 0.7f;
        [InspectorName("智能(只炸敌方队伍)")]
        [SerializeField]
        private bool intelligent = false;

        [Header("伤害数据")]
        [InspectorName("地雷自带伤害")]
        [SerializeField]
        private SustainedDamageData DamageData;

        /// <summary>伤害来源</summary>
        public GameObject Owner { get; private set; }

        Actor m_Actor;
        Health m_health;
        LimitedLife m_limitedLife;
        float m_DeployTime;
        bool m_Exploded;

        private void OnEnable()
        {
            m_Actor = GetComponent<Actor>();
            m_health = GetComponent<Health>();
            m_limitedLife = GetComponent<LimitedLife>();

            if (m_health) m_health.OnDie += Explosion;
            if (m_limitedLife) m_limitedLife.OnEnd.AddListener(OnLifeEnd);

            m_DeployTime = Time.time;
            m_Exploded = false;
        }

        private void OnDisable()
        {
            if (m_health) m_health.OnDie -= Explosion;
            if (m_limitedLife) m_limitedLife.OnEnd.RemoveListener(OnLifeEnd);
        }

        private void Update()
        {
            // 部署延迟：等待稳定落定后再启用触发
            if (Time.time < m_DeployTime + DeployDelay) return;
            if (m_Exploded) return;
            TryHit();
        }

        /// <summary>
        /// 部署地雷：由外部脚本调用，实例化后激活即生效。
        /// </summary>
        /// <param name="pos">部署位置</param>
        /// <param name="owner">伤害来源（可为 null，走无源伤害）</param>
        public void Deploy(Vector3 pos, GameObject owner = null)
        {
            transform.position = pos;
            Owner = owner;
            m_DeployTime = Time.time;
            m_Exploded = false;
        }

        void TryHit()
        {
            if (!DamageData.IsValid()) return;
            // 队伍从 Actor 组件读取；未挂 Actor 时退化为非智能(仅 VaildTarget 过滤)
            int team = m_Actor ? m_Actor.Team : 0;

            // 通过空间网格查询触发范围内的单位（返回 I_Actor，可直接获得队伍信息做智能判定）
            // 注意：TargetCfg.Enemy 只匹配 UnitTypeEnum.Enemy，查不到玩家(Player)，必须用匹配所有类型的目标配置
            var units = BattleManager.Instance.FindUnits(
                new PECircle((PEVector2)Root.position, (PEInt)TriggerRange),
                TargetCfg.EnemyAI,
                // 智能地雷：只炸与地雷不同队伍的存活目标
                item => FpsHelper.VaildTarget(item) && (!intelligent || item.Team != team));

            foreach (var actor in units)
            {
                if (Vector3.Distance(actor.Pos, Root.position) < TriggerRange)
                {
                    // 通过 Health 自杀引爆，与 ProjectileMine 行为一致，避免重复爆炸造成两次伤害
                    if (m_health) m_health.Kill();
                    else Explosion(actor.gameObject);
                    break;
                }
            }
        }

        /// <summary>生命周期结束(超时)：未爆炸则直接回收</summary>
        void OnLifeEnd()
        {
            if (m_Exploded) return;
            Tool.Destroy(gameObject);
        }

        void Explosion(GameObject source)
        {
            if (m_Exploded) return;
            m_Exploded = true;

            // 走通用伤害链路：仅范围伤害，不产生直击伤害（collider=null）
            FpsHelper.Hit(new ProjectileHitData
            {
                pos = Root.position,
                normal = Root.forward,
                collider = null,//不产生直击伤害
                data = DamageData,
                chargeScale = 1,
                owner = Owner??gameObject,
                sfxRange = DamageData.SoundRadius,
                weapon = null,//纯自部署，无武器来源
                useDiffScale = false,
                IgnoreSelf = true,
            });

            // 地雷爆炸后自毁
            VFXManager.Release(gameObject);
        }

        protected virtual void OnDrawGizmos()
        {
            if (!DamageData.IsValid() || !Root) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Root.position, DamageData.GetDamageOuterRadius(1).RawFloat);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Root.position, TriggerRange);
        }
    }
}
