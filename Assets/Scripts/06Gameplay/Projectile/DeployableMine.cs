using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using PEMaths;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.VFX;
using FPSGame.Utils;
using FPSGame.Audio;
using FPSGame.Weapon;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 地雷
    /// 由外部脚本 Instantiate 后调用 <see cref=" SetOrigin(GameObject, GameObject, Collider, Vector3)"/> 部署。
    /// - Actor 提供队伍(Team)，用于智能触发判定敌我
    /// - Health 提供被破坏时引爆(OnDie)以及触发引爆(Kill)
    /// - LimitedLife 提供超时回收
    /// 引爆链路：触发(自动命中 / 被摧毁 / 外部调 <see cref="TriggerExplosion"/>) → 派发"触发爆炸时"
    /// → 等待 <see cref="ExplosionDelay"/> 秒 → 结算范围伤害 + 派发"爆炸时" → 回池。
    /// </summary>
    [AddComponentMenu("单位/地雷", 30)]
    public class DeployableMine : MonoBehaviour,IVfxEffect
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
        [InspectorName("触发距离(0=同触发范围)")]
        [Tooltip("实际3D距离判定使用的容差；为 0 时复用触发范围")]
        [SerializeField]
        private float VerticalRange = 0f;
        [InspectorName("智能(只炸敌方队伍)")]
        [SerializeField]
        private bool intelligent = false;

        [Header("爆炸")]
        [InspectorName("爆炸延迟(秒)")]
        [Tooltip("触发后等待多久才真正结算伤害与回收(0=触发即爆)；延迟期间不会再被触发")]
        [SerializeField]
        private float ExplosionDelay = 0f;
        [InspectorName("触发爆炸时")]
        [Tooltip("进入爆炸延迟那一刻派发(预警音效/闪烁等)；延迟为 0 时与\"爆炸时\"同帧")]
        [SerializeField]
        private UnityEvent OnTriggered;
        [InspectorName("爆炸时")]
        [SerializeField]
        private UnityEvent OnExploded;

        [Header("伤害数据")]
        [InspectorName("地雷自带伤害")]
        [SerializeField]
        private SustainedDamageData DamageData;




        /// <summary>伤害来源</summary>
        public GameObject Owner { get; private set; }

        Actor m_Actor;
        IHealth m_health;
        LimitedLife m_limitedLife;
        float m_DeployTime;
        bool m_Exploded;
        /// <summary>本次已触发(进入爆炸延迟)，用于防止重复触发</summary>
        bool _triggered;
        /// <summary>爆炸延迟协程</summary>
        Coroutine _explodeRoutine;

        /// <summary>触发判定间隔(秒)：触发范围只有米级，不需要逐帧查。
        /// <para>⚠ 原来是每帧一次空间查询 —— 88 颗雷 = 每帧 100KB+ GC（每次查询 new List + new HashSet + 每节点 new List），
        /// 而且只为了判"附近有没有敌人"。</para></summary>
        private const float HitCheckInterval = 0.1f;
        /// <summary>查询结果缓冲：Update 串行调用，静态共用一份（别每次 new List）。</summary>
        private static readonly List<IActor> HitBuffer = new();
        /// <summary>缓存的过滤委托：写成 lambda 会每帧新建闭包类，这里建一次复用。</summary>
        private Func<IActor, bool> m_Filter;
        private float m_NextHitCheck;
        /// <summary>本次判定的队伍（TryHit 每拍刷新，filter 从字段读，避免闭包捕获）</summary>
        private int m_Team;

        private void OnEnable()
        {
            m_Actor = GetComponent<Actor>();
            m_health = GetComponent<IHealth>();
            m_limitedLife = GetComponent<LimitedLife>();

            if (m_health.IsValidMono()) m_health.OnDie += OnHealthDie;
            if (m_limitedLife) m_limitedLife.OnEnd.AddListener(OnLifeEnd);

            m_DeployTime = Time.time;
            // 相位错开：同一批部署的雷如果都从 0 起算，就会每 0.1s 在**同一帧**一起查一遍空间网格
            // （88 颗雷同帧 = 一次 40KB+ 的尖峰）。按实例 ID 打散到 0~0.1s 之间。
            m_NextHitCheck = Time.time + (Mathf.Abs(GetInstanceID()) % 10) * (HitCheckInterval * 0.1f);
            // 池化复用：上一次的触发状态与未完成的延迟协程都要清掉
            m_Exploded = false;
            _triggered = false;
            _explodeRoutine = null;
        }

        private void OnDisable()
        {
            if (m_health.IsValidMono()) m_health.OnDie -= OnHealthDie;
            if (m_limitedLife) m_limitedLife.OnEnd.RemoveListener(OnLifeEnd);
            StopExplodeRoutine();
        }

        private void Update()
        {
            if (m_Exploded) return;
            // 已触发：交给爆炸延迟协程(延迟为 0 时触发那一刻已直接引爆，走不到这里)
            if (_triggered) return;
            // 部署延迟：等待稳定落定后再启用触发
            if (Time.time < m_DeployTime + DeployDelay) return;

            // 触发判定降频到 10Hz（见 HitCheckInterval）
            if (Time.time < m_NextHitCheck) return;
            m_NextHitCheck = Time.time + HitCheckInterval;

            TryHit();
        }


        void TryHit()
        {
            if (!DamageData.IsValid()) return;
            // 队伍从 Actor 组件读取；未挂 Actor 时退化为非智能(仅 VaildTarget 过滤)
            m_Team = m_Actor ? m_Actor.Team : 0;
            if (m_Filter == null) m_Filter = FilterActor;


            //走零分配重载 + 复用缓冲
            UnitQuery.FindUnits(
                new PECircle((PEVector2)Root.position, (PEInt)TriggerRange),
                TargetCfg.EnemyAI,
                m_Filter,
                HitBuffer);

            // 非空即引爆：进 HitBuffer 的都已过 FilterActor（队伍/存活/3D 距离），不需要挑目标。
            if (HitBuffer.Count == 0) return;
            // 经 Health 自杀引爆（OnDie → TriggerExplosion），让"被摧毁"与"触发"走同一条链路；
            // Health 缺失或已死时 Kill() 不派发 OnDie，故后面再兜底直接触发（TriggerExplosion 幂等）
            if (m_health.IsValidMono()) m_health.Kill();
            TriggerExplosion();
        }

        /// <summary>触发过滤：智能地雷只炸与地雷不同队伍的存活目标；按实际3D距离判定(避免高空掠过的单位误触发)。
        /// <para>⚠ 必须缓存成委托（见 m_Filter），别在调用点写 lambda —— lambda 每拍都会新建闭包类。</para></summary>
        private bool FilterActor(IActor item)
            => FpsHelper.VaildTarget(item) && (!intelligent || item.Team != m_Team) && InTriggerRange(item);

        /// <summary>
        /// 实际距离判定：由单位中心点与单位高度构成竖直占位区间 [CenterPos.y - HalfHeight, CenterPos.y + HalfHeight]
        /// （半高度未配置(<=0)时退化为单位中心点本身），水平方向以单位占位边缘(中心距 - HalfRange)计。
        /// 实际距离 = 水平间隙与竖直间隙合成的 3D 距离，仅当不超过触发距离才可引爆，
        /// 避免高空掠过的飞行单位(如治疗无人机)误触发，同时保证地雷黏在巨型单位表面时仍能正常引爆。
        /// </summary>
        bool InTriggerRange(IActor actor)
        {
            float range = VerticalRange > 0f ? VerticalRange : TriggerRange;

            // 竖直间隙：地雷高度到单位竖直占位区间的距离(未配置半高度时按中心点计算)
            float centerY = actor.CenterPos.y;
            float halfH = actor.HalfHeight;
            float bottom = centerY - halfH;
            float top = centerY + halfH;
            float mineY = Root.position.y;
            float vertGap = mineY < bottom ? bottom - mineY : (mineY > top ? mineY - top : 0f);

            // 水平间隙：到单位水平占位边缘的距离，扣除单位半径保证黏在巨型单位表面时仍可触发
            Vector2 mineXZ = new Vector2(Root.position.x, Root.position.z);
            Vector2 unitXZ = new Vector2(actor.CenterPos.x, actor.CenterPos.z);
            float horizGap = Mathf.Max(0f, Vector2.Distance(mineXZ, unitXZ) - actor.HalfRange);

            // 实际(3D)距离
            float actualDist = Mathf.Sqrt(horizGap * horizGap + vertGap * vertGap);
            return actualDist <= range;
        }

        /// <summary>
        /// 生命周期结束(超时)：已触发的不留哑火，立即引爆；未触发的直接回收。
        /// </summary>
        void OnLifeEnd()
        {
            if (m_Exploded) return;
            if (_triggered)
            {
                StopExplodeRoutine();
                DoExplosion();
                return;
            }
            Tool.Destroy(gameObject);
        }

        /// <summary>Health 死亡(被摧毁) → 引爆</summary>
        private void OnHealthDie(GameObject caused, GameObject origin) => TriggerExplosion();

        /// <summary>
        /// 触发爆炸：外部(含其它物体的 UnityEvent)也可直接调用，幂等(只生效一次)。
        /// 先派发<see cref="OnTriggered"/>"触发爆炸时"，再按 <see cref="ExplosionDelay"/> 延迟结算伤害；
        /// 延迟为 0 时同步引爆。触发一次后 <see cref="Update"/> 不再做命中判定。
        /// </summary>
        public void TriggerExplosion()
        {
            if (m_Exploded || _triggered) return;
            _triggered = true;

            OnTriggered?.Invoke();

            if (ExplosionDelay > 0f)
            {
                StopExplodeRoutine();
                _explodeRoutine = StartCoroutine(DelayExplode());
            }
            else DoExplosion();
        }

        /// <summary>爆炸延迟到点 → 真正引爆</summary>
        private IEnumerator DelayExplode()
        {
            yield return new WaitForSeconds(ExplosionDelay);
            _explodeRoutine = null;
            DoExplosion();
        }

        /// <summary>中止未完成的爆炸延迟(回收/禁用时)</summary>
        private void StopExplodeRoutine()
        {
            if (_explodeRoutine == null) return;
            StopCoroutine(_explodeRoutine);
            _explodeRoutine = null;
        }

        /// <summary>真正引爆：结算范围伤害 → 派发<see cref="OnExploded"/>"爆炸时" → 回池</summary>
        private void DoExplosion()
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
                origin = Owner,          // 起源 = 放置者（统计）
                caused = gameObject,    // 直接来源 = 地雷自己（拍板③：无武器/无 Actor 的物件显示成它自己）
                self = gameObject,
                sfxRange = DamageData.SoundRadius,
                weapon = null,//纯自部署，无武器来源
                useDiffScale = false,
                IgnoreSelf = false,
                // ★ 地雷也是"每端各自生成、各自触发"的：非权威端只对玩家/盟友落地，否则两端各爆一次
                localEffectDamage = true,
            });
            // 先派发再回池：监听者还能拿到仍然存活的物体
            OnExploded?.Invoke();
            //Debug.LogWarning("回收"+gameObject,gameObject);
            // 地雷爆炸后自毁
            FPSGame.Core.VfxPool.Release(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
                        if (!DamageData.IsValid() || !Root) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Root.position, DamageData.GetDamageOuterRadius(1).RawFloat);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Root.position, TriggerRange);
        }

        public void SetOrigin(GameObject owner, GameObject weaponRoot, Collider target, Vector3 point)
        {
            transform.position = point;
            Owner = owner;
            m_DeployTime = Time.time;
            m_NextHitCheck = Time.time + (Mathf.Abs(GetInstanceID()) % 10) * (HitCheckInterval * 0.1f);
            m_Exploded = false;
            _triggered = false;
            StopExplodeRoutine();
        }

        /// <summary>
        /// 监视器用的
        /// </summary>
        /// <param name="sfx"></param>
        public void PlaySound(AudioClip sfx)
        {
            AudioSvc.PlaySound(new(sfx, Root.position, DamageData.SoundRadius, AudioGroups.Impact));
        }

    }
}
