using UnityEngine;
using UnityEngine.AI;

namespace FPSGame.AI
{
    /// <summary>
    /// 联机 · "房主权威目标点 → 本端各自寻路"的**通用落点组件**。
    ///
    /// <para>▍职责（与原先埋在 <c>EnemyController</c> 里的那套**语义完全一致**，只是搬出来复用）：
    /// ① 缓存"网络下发但**当帧应用不了**"的目标点；② agent 可用才落地，否则每帧重试到成功；
    /// ③ 远端点**绕开**本端去重/节流 —— 那套判据基于本地状态，两端不一定同步命中，会静默吞掉远端点。</para>
    ///
    /// <para>▍为什么要抽：这套逻辑**每个"房主权威移动"的单位都要一份**（敌人 / 场景 NPC / 凯伊 …），
    /// 各写一份必然各漏一处 —— 例：<c>NPCWalk</c> 自己复刻了一版（且它**不能**走
    /// <c>PathRequestManager</c>：大厅里没人订阅、请求会被静默丢弃）；<c>SpecUnitController</c> 干脆没有这层缓存。</para>
    ///
    /// <para>▍"怎么落地"是**注入的**（<see cref="applyHandler"/>），因为各单位的通道不同：
    /// 敌人要过 <c>UnitEventBus.PathRequest</c>（09 的 <c>PathRequestManager</c> 带投影兜底），
    /// 而大厅/场景单位只能直连 <c>NavMeshAgent.SetDestination</c>。</para>
    ///
    /// <para>▍本次是**纯结构收敛，不改任何消息**：<c>EnemyController</c> 的对外行为与消息完全不变，
    /// 只是把内部那三个成员挪进了本组件。</para>
    /// </summary>
    [AddComponentMenu("联机/同步寻路落点")]
    public class SyncedNavMover : MonoBehaviour
    {
        /// <summary>
        /// 落地方式：<c>true</c> = 本次已处理完（无论 agent 存不存在）；<c>false</c> = agent 暂不可用、需要重试。
        /// <para>为 <c>null</c> 时用默认实现：<c>NavMeshAgent.SetDestination</c>（agent 被禁用则返回 false 等重试）。</para>
        /// </summary>
        public System.Func<Vector3, bool> applyHandler;

        /// <summary>成员端 = <c>true</c>：本端决策一律作废（移动意图由房主下发）。
        /// <para>⚠ 只是个可读开关；实际闸门是否生效由调用方决定（<c>EnemyController</c> 自己仍先判它的静态开关）。</para></summary>
        public bool RemoteDriven { get; set; }

        /// <summary>最近一次的目标点（本地或远端），供 AI 侧判断"目标没变"。</summary>
        public Vector3 LastDestination { get; private set; }

        /// <summary>是否还有没落地的目标点。</summary>
        public bool HasPending => _hasPending;

        Vector3 _pending;
        bool _hasPending;
        NavMeshAgent _agent;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        void Update()
        {
            RetryPending();
        }

        /// <summary>
        /// 【网络下发】应用房主给的移动目标：**绕开**本端去重/节流。
        /// <para>▍为什么必须缓存重试：成员的移动**全靠这条**，而房主只在"目标变化"时发（&lt;1m 去重）
        /// ⇒ 丢掉一条就再也不会来第二条，那只单位会一直原地不动。收到时 agent 正好被禁用
        /// （空投单位在落地前整套 Behaviour 都是关的）就会丢 ⇒ 先存下来，等 agent 启用后补发。</para>
        /// </summary>
        public void ApplyRemoteDestination(Vector3 destination)
        {
            LastDestination = destination;
            _pending = destination;
            _hasPending = true;
            RetryPending();
        }

        /// <summary>【本端决策】设置目标点（<see cref="RemoteDriven"/> 时直接作废）。</summary>
        public void SetDestination(Vector3 destination)
        {
            if (RemoteDriven) return;
            LastDestination = destination;
            _pending = destination;
            _hasPending = true;
            RetryPending();
        }

        /// <summary>只清掉"待落地"标记，**不动** <see cref="LastDestination"/>
        /// （用于"单位已死、agent 会被禁用，别再每帧去补发"）。</summary>
        public void CancelPending()
        {
            _hasPending = false;
        }

        /// <summary>停下：清待落地点 + 清目标 + <c>ResetPath</c>。</summary>
        public void Stop()
        {
            _hasPending = false;
            LastDestination = default;
            if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh) _agent.ResetPath();
        }

        void RetryPending()
        {
            if (!_hasPending) return;
            if (TryApply(_pending)) _hasPending = false;
        }

        /// <summary>
        /// agent 真正可用时才落地。
        /// <para>⚠ 默认实现只拦"agent 被禁用"这一种：<c>SetDestination</c> 对它必然失败、还会刷错误日志。
        /// 其余情况（含离网格 / 飞行单位）保持调用方注入的 <see cref="applyHandler"/> 的原行为。</para>
        /// </summary>
        bool TryApply(Vector3 destination)
        {
            if (applyHandler != null) return applyHandler(destination);

            if (_agent == null) return true;                  // 没有 agent（飞行/纯逻辑单位）：算处理完
            if (!_agent.isActiveAndEnabled) return false;     // 禁用中：等启用后重试
            if (!_agent.isOnNavMesh) return false;            // 不在网格上 SetDestination 会报错 ⇒ 等就位
            _agent.SetDestination(destination);
            return true;
        }
    }
}
