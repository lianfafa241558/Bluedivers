using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.Managers
{

/// <summary>
/// **逻辑帧宿主**：按 <see cref="I_Login"/> 调度所有接入逻辑帧的组件（<c>LogicFrame.Sink</c> 的实现方）。
///
/// <para>▍名字：2026-10-07 由 <c>NetManager</c> 改名而来 —— 原名是历史包袱（最初为联机准备，
/// 设想"帧由服务器 / 房主下发"），但它的实际职责**与网络无关**：`NetTmp` 对它零引用，
/// 它是全局 **50Hz 定步时钟**，撑着 `LogicBehaviour` 的整条武器继承链（射速 / 换弹 / 热量 / 地雷 / 临时武器）。
/// 基类也从 `SingletonNet`（Photon 时代残留）换回 <see cref="FPSGame.Core.Singleton{T}"/>。</para>
///
/// <para>▍⚠ 关掉它的后果是**静默**的：<c>LogicFrame.Sink == null</c> 时 <c>LogicFrame.Register</c> 是**空操作且不会补注册**
/// ⇒ 所有 <c>LogicTick</c> 从此不跑，而且不报错。所以它必须常驻在 GameRoot 上。</para>
///
/// <para>▍将来真要做**逻辑帧同步**：只需把"帧来源"从本地时钟换成网络帧（<c>Update</c> 里那个累加器），
/// 接入方（<c>LogicBehaviour</c> 子类）与本类结构都不受影响。⚠ 但瓶颈不在"帧从哪来"，而在**确定性**
/// （`UnityEngine.Random` / 物理弹道 / NavMesh）——详见 <c>topics/net.md</c>。</para>
/// </summary>

public class LogicFrameHost : FPSGame.Core.Singleton<LogicFrameHost>, I_GlobaManager, ILogicFrameSink
{
    /// <summary>逻辑帧步长(秒)，与 Constants.LoginFrame 同源：每跑一帧，各 I_Login 的计时器就 += 这个值。</summary>
    private static readonly float StepSeconds = Constants.LoginFrame.RawFloat;

    /// <summary>单个渲染帧最多补跑的逻辑帧数(0.02 × 10 = 0.2s)。
    /// ▍为什么要"累加 + 封顶"：逻辑帧率必须与渲染帧率解耦，否则 FPS 低于 50 时按 TickTime 累加的
    /// 冷却/射速会系统性变慢（"2.68s 首发"会漂到 3s+）。但欠下的帧不能在一次渲染帧里全补完——
    /// 长卡顿 / 断点 / 重编译后 Time.deltaTime 可能很大，不封顶就会在同一帧里连跑几十次 LogicTick
    /// （武器连发、伤害结算雪崩）。⇒ 债务保留（与旧实现语义一致，不丢弃），但每帧最多补这么多，
    /// 其余在后续帧里逐步还清。</summary>
    private const int MaxCatchUpTicks = 10;

    private List<I_Login> list;

    /// <summary>已累加但尚未兑现的真实时间(秒)，够 StepSeconds 就跑一帧</summary>
    private float _accumulator;

    /// <summary>是否正在跑逻辑帧：期间对 list 的增删改为入队，帧末统一应用。
    /// ▍为什么要这样：LogicTick 内销毁对象会触发 OnDestroy ⇒ LogicFrame.Unregister ⇒ Remove，
    /// 正序 for 遍历中 list 被改动会漏跳被顶上来的元素（不会崩，IsActive 有判空，但会静默少跑一帧）。</summary>
    private bool _ticking;
    private readonly List<I_Login> _pendingAdd = new();
    private readonly List<I_Login> _pendingRemove = new();

    public void Init()
    {
        //接管逻辑帧宿主：供玩法层（LogicBehaviour 的 26 个子类）注册（见 00Core/LogicFrame.cs）
        LogicFrame.Sink = this;
        list = new();
        _accumulator = 0f;
        _ticking = false;
        _pendingAdd.Clear();
        _pendingRemove.Clear();
    }
    public void UnInit()
    {
        //服务下线：把宿主还回去（未接管语义），避免静态字段指向已销毁实例
        if (ReferenceEquals(LogicFrame.Sink, this)) LogicFrame.Sink = null;
        list = null;
        _ticking = false;
        _pendingAdd.Clear();
        _pendingRemove.Clear();
    }

    void Update()
    {
        if (list == null) return;

        //真实时间累加：逻辑帧率恒为 1/StepSeconds(50Hz)，与渲染帧率解耦 ⇒ 冷却/射速回到"真实秒"。
        //用 Time.deltaTime 而非 Time.unscaledDeltaTime：与旧实现(Time.time)同一时钟域，
        //暂停(timeScale=0)时逻辑一起停，语义不变。
        _accumulator += Time.deltaTime;

        int ticks = 0;
        while (_accumulator >= StepSeconds && ticks < MaxCatchUpTicks)
        {
            _accumulator -= StepSeconds;
            TickOnce();
            ++ticks;
        }
    }

    /// <summary>跑一帧逻辑。帧内 list 只读（引擎侧销毁触发的注销会入队到帧末应用）。</summary>
    private void TickOnce()
    {
        _ticking = true;
        for (int i = 0; i < list.Count; ++i)
        {
            if (list[i].IsActive()) list[i].LogicTick();
        }
        _ticking = false;

        FlushPending();
    }

    /// <summary>应用本帧积累的增删。Add/Remove 在帧内会互相抵消(先 Remove 后 Add = 保留)，保持调用顺序语义。</summary>
    private void FlushPending()
    {
        for (int i = 0; i < _pendingAdd.Count; ++i)
        {
            var obj = _pendingAdd[i];
            if (obj != null && !list.Contains(obj)) list.Add(obj);
        }
        _pendingAdd.Clear();

        for (int i = 0; i < _pendingRemove.Count; ++i)
        {
            var obj = _pendingRemove[i];
            if (obj != null) list.Remove(obj);
        }
        _pendingRemove.Clear();
    }

    public void Add(I_Login obj)
    {
        if (obj == null || list == null) return;

        if (!_ticking)
        {
            if (!list.Contains(obj)) list.Add(obj);
            return;
        }

        //帧内注册：撤销同帧的注销，避免"先 Remove 后 Add"被吞掉
        _pendingRemove.Remove(obj);
        if (!_pendingAdd.Contains(obj)) _pendingAdd.Add(obj);
    }
    public void Remove(I_Login obj)
    {
        if (obj == null || list == null) return;

        if (!_ticking)
        {
            list.Remove(obj);
            return;
        }

        //帧内注销：撤销同帧的注册，避免"先 Add 后 Remove"被复活
        _pendingAdd.Remove(obj);
        if (!_pendingRemove.Contains(obj)) _pendingRemove.Add(obj);
    }
}

}
