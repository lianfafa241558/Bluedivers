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
    /// - 队伍(Team)在 <see cref="SetOrigin"/> 时从投放者解析，用于智能触发判定敌我（不必挂 Actor）
    /// - Health 提供被破坏时引爆(OnDie)以及触发引爆(Kill)
    /// - LimitedLife 提供超时回收
    /// 引爆链路：触发(自动命中 / 被摧毁 / 外部调 <see cref="TriggerExplosion"/>) → 派发"触发爆炸时"
    /// → 等待 <see cref="ExplosionDelay"/> 秒 → 结算范围伤害 + 派发"爆炸时" → 回池。
    /// 爆炸后是否回池由 <see cref="ConsumeOnExplosion"/> 决定，留在场上时用 <see cref="Rearm"/> 重装
    /// （重复引爆的实现在 <see cref="RepeatableMine"/>）。
    /// </summary>
    [AddComponentMenu("单位/地雷", 30)]
    public class DeployableMine : MonoBehaviour,IVfxEffect
    {
        [Header("通用")]
        [InspectorName("根部变换")]
        [SerializeField]
        protected Transform Root;
        [InspectorName("部署后启动延迟(秒)")]
        [SerializeField]
        protected float DeployDelay = 2f;
        [InspectorName("触发范围")]
        [SerializeField]
        protected float TriggerRange = 0.7f;
        [InspectorName("触发距离(0=同触发范围)")]
        [Tooltip("实际3D距离判定使用的容差；为 0 时复用触发范围")]
        [SerializeField]
        protected float VerticalRange = 0f;
        [InspectorName("智能(只炸敌方队伍)")]
        [SerializeField]
        protected bool intelligent = false;
        [InspectorName("己方队伍(-1=跟随投放者)")]
        [Tooltip("智能过滤用：只炸与此队伍不同的单位。-1 = 部署时取投放者的队伍（拿不到按 0，玩家/盟友队）")]
        [SerializeField]
        protected int TeamOverride = -1;

        [Header("爆炸")]
        [InspectorName("爆炸延迟(秒)")]
        [Tooltip("触发后等待多久才真正结算伤害与回收(0=触发即爆)；延迟期间不会再被触发")]
        [SerializeField]
        protected float ExplosionDelay = 0f;
        [InspectorName("触发爆炸时")]
        [Tooltip("进入爆炸延迟那一刻派发(预警音效/闪烁等)；延迟为 0 时与\"爆炸时\"同帧")]
        [SerializeField]
        protected UnityEvent OnTriggered;
        [InspectorName("爆炸时")]
        [SerializeField]
        protected UnityEvent OnExploded;

        [Header("伤害数据")]
        [InspectorName("地雷自带伤害")]
        [SerializeField]
        protected SustainedDamageData DamageData;




        /// <summary>伤害来源</summary>
        public GameObject Owner { get; private set; }
        /// <summary>本次部署的队伍（智能过滤用，<see cref="SetOrigin"/> 里从投放者解析一次）</summary>
        public int Team { get; private set; }

        IHealth m_health;
        LimitedLife m_limitedLife;
        /// <summary>可触发时刻（部署延迟与重复引爆的重装间隔都折算到它）</summary>
        float m_ReadyTime;
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

        private void OnEnable()
        {
            m_health = GetComponent<IHealth>();
            m_limitedLife = GetComponent<LimitedLife>();

            if (m_health.IsValidMono()) m_health.OnDie += OnHealthDie;
            if (m_limitedLife) m_limitedLife.OnEnd.AddListener(OnLifeEnd);

            m_ReadyTime = Time.time + DeployDelay;
            // 相位错开：同一批部署的雷如果都从 0 起算，就会每 0.1s 在**同一帧**一起查一遍空间网格
            // （88 颗雷同帧 = 一次 40KB+ 的尖峰）。按实例 ID 打散到 0~0.1s 之间。
            m_NextHitCheck = Time.time + (Mathf.Abs(GetInstanceID()) % 10) * (HitCheckInterval * 0.1f);
            // 池化复用：上一次的触发状态与未完成的延迟协程都要清掉
            m_Exploded = false;
            _triggered = false;
            _explodeRoutine = null;
            OnMineReset();
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
            // 部署延迟 / 重装间隔：等待到点后再启用触发
            if (Time.time < m_ReadyTime) return;

            // 触发判定降频到 10Hz（见 HitCheckInterval）
            if (Time.time < m_NextHitCheck) return;
            m_NextHitCheck = Time.time + HitCheckInterval;

            TryHit();
        }


        void TryHit()
        {
            if (!DamageData.IsValid()) return;
            // 队伍见 SetOrigin/ResolveTeam：不再需要地雷自己挂 Actor
            if (m_Filter == null) m_Filter = FilterActor;


            //走零分配重载 + 复用缓冲
            UnitQuery.FindUnits(
                new PECircle((PEVector2)Root.position, (PEInt)TriggerRange),
                TargetCfg.EnemyAI,
                m_Filter,
                HitBuffer);

            // 非空即引爆：进 HitBuffer 的都已过 FilterActor（队伍/存活/3D 距离），不需要挑目标。
            if (HitBuffer.Count == 0) return;
            TriggerByHit();
        }

        /// <summary>命中判定通过后的触发入口。
        /// <para>▍基类经 Health 自杀引爆（OnDie → TriggerExplosion），让"被摧毁"与"触发"走同一条链路。
        /// ⚠ 重复引爆的子类必须重写：非最后一次**不能** Kill —— Health 一死就再也不派发 OnDie，
        /// 后面几次就失去"被击毁提前结算"这条链路。</para></summary>
        protected virtual void TriggerByHit()
        {
            // Health 缺失或已死时 Kill() 不派发 OnDie，故后面再兜底直接触发（TriggerExplosion 幂等）
            if (m_health.IsValidMono()) m_health.Kill();
            TriggerExplosion();
        }

        /// <summary>触发过滤：智能地雷只炸与己方不同队伍的存活目标；按实际3D距离判定(避免高空掠过的单位误触发)。
        /// <para>⚠ 必须缓存成委托（见 m_Filter），别在调用点写 lambda —— lambda 每拍都会新建闭包类。</para></summary>
        private bool FilterActor(IActor item)
            => FpsHelper.VaildTarget(item) && (!intelligent || item.Team != Team) && InTriggerRange(item);

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
        /// 生命周期结束(超时)：已触发的不留哑火，立即引爆；未触发的走 <see cref="OnLifeEndNotTriggered"/>。
        /// </summary>
        void OnLifeEnd()
        {
            if (m_Exploded) return;
            if (!_triggered)
            {
                OnLifeEndNotTriggered();
                return;
            }
            ExplodeNow();
        }

        /// <summary>寿命到点但从未触发过：默认静默回收。
        /// <para>要到点自爆的子类重写为 <see cref="ExplodeNow"/>（重复引爆的地雷即走这条）。</para></summary>
        protected virtual void OnLifeEndNotTriggered() => Tool.Destroy(gameObject);

        /// <summary>跳过 <see cref="ExplosionDelay"/> 立即引爆（超时兜底 / 被摧毁用）：
        /// 先让子类作废剩余次数，再结算并回收。</summary>
        protected void ExplodeNow()
        {
            if (m_Exploded) return;
            StopExplodeRoutine();
            PrepareFinalExplosion();
            DoExplosion();
        }

        /// <summary>Health 死亡(被摧毁) → 引爆</summary>
        private void OnHealthDie(GameObject caused, GameObject origin)
        {
            PrepareFinalExplosion();
            TriggerExplosion();
        }

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
            if (ConsumeOnExplosion())
            {
                // 地雷爆炸后自毁
                FPSGame.Core.VfxPool.Release(gameObject);
                return;
            }
            // 未消耗 ⇒ 留在场上，交给子类决定多久后重新武装
            RearmAfterExplosion();
        }

        /// <summary>本次爆炸是否消耗掉地雷（=回池）。重复引爆的子类重写为按剩余次数判断。</summary>
        protected virtual bool ConsumeOnExplosion() => true;

        /// <summary>不消耗时（<see cref="ConsumeOnExplosion"/> 返回 false）的重装动作。</summary>
        protected virtual void RearmAfterExplosion() => Rearm(0f);

        /// <summary>重新武装：清掉"已触发/已爆炸"，<paramref name="delay"/> 秒后才重新可触发（0=立刻）。
        /// <para>⚠ 只给爆炸后仍留在场上的子类用；回池时机由 <see cref="ConsumeOnExplosion"/> 决定。</para></summary>
        protected void Rearm(float delay)
        {
            StopExplodeRoutine();
            m_Exploded = false;
            _triggered = false;
            m_ReadyTime = Time.time + delay;
            // 相位错开，理由见 OnEnable
            m_NextHitCheck = Time.time + (Mathf.Abs(GetInstanceID()) % 10) * (HitCheckInterval * 0.1f);
        }

        /// <summary>池化复用 / 重新部署时的状态复位（子类扩展点：重置自己的计数等）。</summary>
        protected virtual void OnMineReset() { }

        /// <summary>被击毁 / 超时兜底：不论子类还剩几次，本次一律按"最后一次"结算（会被消耗）。</summary>
        protected virtual void PrepareFinalExplosion() { }

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
            Team = ResolveTeam(owner);
            m_ReadyTime = Time.time + DeployDelay;
            m_NextHitCheck = Time.time + (Mathf.Abs(GetInstanceID()) % 10) * (HitCheckInterval * 0.1f);
            m_Exploded = false;
            _triggered = false;
            StopExplodeRoutine();
            OnMineReset();
        }

        /// <summary>己方队伍：显式配置优先，否则取投放者的队伍（投放者没有单位身份 ⇒ 按 0，即玩家/盟友队）。
        /// <para>⚠ 这样地雷 prefab 不需要挂 <c>Actor</c>：它只要一个"阵营"值，不该顺带进单位网格/号段/逐帧驱动。</para></summary>
        private int ResolveTeam(GameObject owner)
        {
            if (TeamOverride >= 0) return TeamOverride;
            return owner != null && owner.TryGetComponent(out IActor ownerActor) ? ownerActor.Team : 0;
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
