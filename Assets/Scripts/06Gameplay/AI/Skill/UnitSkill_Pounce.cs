using System.Collections;
using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace FPSGame.AI
{
    /// <summary>
    /// 飞扑：朝目标方向跃出，落到"距目标点 _stopDistance"处（越过途中障碍）。
    /// ⚠ 空中必须停用 NavMeshAgent —— 它每帧把 transform 拽回导航网格，不停用既写不进 Y、也走不出弧线。
    /// </summary>
    [AddComponentMenu("单位/_技能/飞扑", 30)]
    public class UnitSkill_Pounce : UnitSkill_Base
    {
        /// <summary>每个窗口允许起跳的"单位量"总量：固定 1，各单位按自己的占用扣，凑满本窗口就不再放行</summary>
        private const float WindowQuota = 1f;

        [InspectorName("飞扑高度")]
        [Tooltip("弧线拱高(米)：0 表示贴地平移")]
        [SerializeField]
        private float _flyHeight = 3f;

        [InspectorName("飞扑到距离目标点的距离")]
        [Tooltip("落点与目标点保持的距离(米)：已在该距离内时不扑，避免扑过头")]
        [SerializeField]
        private float _stopDistance = 2f;

        /// <summary>限流窗口长度(秒)：任意 X 秒内全场最多扑掉 1 个单位量（0 = 不限流）。
        /// ⚠ static ⇒ 不进序列化、Inspector 里没有这一项，改值只能改这里，改完全场同时生效。</summary>
        private static float _windowSeconds = 3f;

        [InspectorName("占用单位量")]
        [Tooltip("本单位一次起跳占掉多少名额：每 X 秒总共 1 单位量，0.3 ⇒ 大约放行 3 个；0 或负数 = 不占名额")]
        [SerializeField]
        private float _quotaCost = 1f;

        [DisplayField]
        [InspectorName("已跳个数(展示)")]
        [SerializeField]
        private int _ShowJumped;

        [DisplayField]
        [InspectorName("已用单位量(展示)")]
        [Tooltip("窗口内已被占用的单位量(0~1)，凑满 1 后本窗口不再放行")]
        [SerializeField]
        private float _ShowUsedQuota;

        /// <summary>最近几次成功释放的记录(时刻 + 占用单位量)：static ⇒ 全场共享，防止一波单位同时飞扑。
        /// 用"滑动窗口"累加单位量，而不是"到期时刻/计个数"：大小单位混编时名额才分配得合理。
        /// ⚠ 联机两端各有一份，可能错开一拍（位置本身也是各端本地推进的）。</summary>
        private static readonly Queue<(float time, float cost)> _recentJumps = new();

        /// <summary>进入播放时清空静态配额：编辑器关掉 Domain Reload 时上一局的残留会挡住新一局</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedLimit() => _recentJumps.Clear();

        /// <summary>落点：飞行中被打断(失活/阵亡)时靠它收尾，不能只存在协程的局部变量里</summary>
        private Vector3 _landPos;
        private NavMeshAgent _agent;
        private Coroutine _flyRoutine;
        private bool _flying;

        protected override void Init()
        {
            // 基类 Start 里已取过 EnemyController，这里只在初始化阶段取一次 agent（飞行阶段要反复开关它）
            _agent = GetComponent<NavMeshAgent>();

            // 单只就超过整窗配额 ⇒ 它永远等不到名额，静默不跳最难查，这里显式报一次
            if (_quotaCost > WindowQuota)
            {
                Debug.LogWarning($"技能/飞扑: 占用单位量 {_quotaCost} > 每窗口配额 {WindowQuota}，该单位永远不会起跳", gameObject);
            }
        }

        /// <summary>检视器的展示值要跟着全场配额走：静态量没法直接序列化显示，借每拍 Tick 刷一次</summary>
        public override bool Tick()
        {
            RefreshShowFields();
            return base.Tick();
        }

        protected override void SkillStart()
        {
            // 空中不重复触发：Cool=0 时每个 Tick 都会进这里
            if (_flying) return;

            // 飞扑时长就取基类的"技能持续"(秒)：为 0 时扑不出去（弧线时长为 0 等同于瞬移）
            if (Duration <= 0)
            {
                Debug.LogWarning("技能/飞扑: 基类\"技能持续\"未配置(<=0)，放弃", gameObject);
                return;
            }

            // ★ 限流：Tick 是逐个单位调用的，同帧先扑的已占掉名额 ⇒ 窗口单位量凑满后的必被挡下
            if (!HasJumpSlot()) return;

            if (!HaveTarget(out _))
            {
                Debug.LogWarning("技能/飞扑: 没有有效目标，放弃", gameObject);
                return;
            }

            Vector3 toTarget = m_Controller.Target.Pos - transform.position;
            toTarget.y = 0f;
            float leapDistance = toTarget.magnitude - _stopDistance;
            // 已经在落点距离内：再往前扑就越过目标了
            if (leapDistance <= 0.01f) return;

            Vector3 dir = toTarget.normalized;
            // 落点必须贴回导航网格：空中位移脱离网格，落点不在网格上 agent 之后没法继续寻路
            if (!NavMesh.SamplePosition(transform.position + dir * leapDistance, out var hit, 5f, NavMesh.AllAreas))
            {
                Debug.LogWarning("技能/飞扑: 落点不在导航网格上，放弃本次飞扑", gameObject);
                return;
            }

            _landPos = hit.position;
            transform.rotation = Quaternion.LookRotation(dir);
            _flying = true;
            // 占配额放在"确定要飞"之后：前面那几条放弃分支(没目标/太近/落点不在网格)不该占掉名额
            TakeJumpSlot();

            if (_agent)
            {
                if (FpsHelper.HaveNavMeshAgent(_agent)) _agent.ResetPath();
                _agent.enabled = false;
            }

            _flyRoutine = StartCoroutine(FlyRoutine(transform.position, _landPos, Duration));
        }

        /// <summary>窗口内还塞得下本单位这一份？</summary>
        private bool HasJumpSlot()
        {
            RefreshShowFields();
            return _ShowUsedQuota + _quotaCost <= WindowQuota;
        }

        /// <summary>占掉本单位这一份（本次已经决定要飞了）</summary>
        private void TakeJumpSlot()
        {
            TrimExpiredJumps();
            _recentJumps.Enqueue((Time.time, _quotaCost));
            RefreshShowFields();
        }

        /// <summary>刷检视器展示：已跳个数 + 窗口内已用单位量（都是 static 队列的镜像）</summary>
        private void RefreshShowFields()
        {
            TrimExpiredJumps();
            _ShowJumped = _recentJumps.Count;

            float used = 0f;
            foreach (var jump in _recentJumps) used += jump.cost;
            _ShowUsedQuota = used;
        }

        /// <summary>丢掉滑出窗口(X 秒)的旧记录
        /// <para>⚠ X ≤ 0 时会把记录全清空 ⇒ 等于不限流，正好对上"0 = 不限流"的配置语义。</para></summary>
        private void TrimExpiredJumps()
        {
            while (_recentJumps.Count > 0 && Time.time - _recentJumps.Peek().time >= _windowSeconds)
            {
                _recentJumps.Dequeue();
            }
        }

        /// <summary>抛物线飞行：水平匀速插值 + 4h·t(1-t) 的拱高（t=0.5 处最高）</summary>
        private IEnumerator FlyRoutine(Vector3 start, Vector3 land, float flyTime)
        {
            float elapsed = 0f;
            while (elapsed < flyTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flyTime);
                transform.position = Vector3.Lerp(start, land, t) + Vector3.up * (_flyHeight * 4f * t * (1f - t));
                yield return null;
            }

            _flyRoutine = null;
            LandAndRestoreAgent();
        }

        /// <summary>正常落地：摆到落点、恢复 agent 并丢掉飞扑前的旧路径（等下一次决策重新寻路）</summary>
        private void LandAndRestoreAgent()
        {
            if (!_flying) return;
            _flying = false;

            transform.position = _landPos;
            if (!_agent) return;
            _agent.enabled = true;      // ⚠ Warp 只对已启用的 agent 生效：先启用再落位
            _agent.Warp(_landPos);
            m_Controller.StopNav();
        }

        protected override void SkillEnd()
        {
            // 正常时协程已按同一时长落地；基类 Tick 是 1 秒粒度，可能早一拍判到点 ⇒ 还在空中就当场收尾
            if (!_flying) return;
            StopFlyRoutine();
            LandAndRestoreAgent();
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            // 空中阵亡：停掉协程，agent 交给 EnemyController._OnDie 关，这里不能再打开
            StopFlyRoutine();
            _flying = false;
        }

        private void OnDisable()
        {
            // 池化中途失活：协程随对象一起停 ⇒ 必须自己收尾，否则复用的单位带着停用的 agent 不会动
            if (!_flying) return;
            StopFlyRoutine();
            _flying = false;
            transform.position = _landPos;
            if (_agent) _agent.enabled = true;
        }

        private void StopFlyRoutine()
        {
            if (_flyRoutine == null) return;
            StopCoroutine(_flyRoutine);
            _flyRoutine = null;
        }
    }
}
