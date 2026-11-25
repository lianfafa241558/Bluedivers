using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Gameplay;
using FPSGame.GameContract;
using FPSGame.Game;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Effect
{

/// <summary>
/// 治疗包的表现与治疗效果。
/// <para>▍救援对象在**出现那一刻**就锁定（见 <see cref="CaptureTargets"/>），落地后**不看距离**直接拉起：
/// 从呼叫增援到包落地有 7s+，这段时间尸体可能被爆炸推走、包本身也可能落偏 ⇒ 老实现那套"落地时在 5m 内找倒地者"
/// 经常一个都匹配不上，一整发增援白扔（2026-11-25 用户实测）。</para>
/// </summary>
[AddComponentMenu("特效/治疗包")]
public class HealBag : MonoBehaviour, IRecyclable
{
    /// <summary>出现时锁定的候选（按离本包的**水平**距离排序，最近的在最前）。一包只负责拉起一个。</summary>
    private readonly List<Furniture_PlayerDown> _targets = new();

    /// <summary>本次出现是否已锁过目标（包是池化复用的 ⇒ 每次出现都要重锁，见 <see cref="OnShow"/>）。</summary>
    private bool _captured;

    /// <summary>池化出现：<c>VFXManager.Creat</c> 摆好位置/父级之后才回调 ⇒ 这里取到的才是本次真实落点。</summary>
    public void OnShow() => CaptureTargets();

    public void OnHide() => _targets.Clear();

    /// <summary>非池化路径（直接摆场景 / 别的 Instantiate）：Start 时位置也摆好了。</summary>
    private void Start()
    {
        if (!_captured) CaptureTargets();
    }

    /// <summary>
    /// 锁定这次要救的人：当前**倒地**的玩家，按离本包的水平距离排序。
    /// <para>▍为什么按距离：<c>AirdropController</c> 是"每个死人各放一个包"（见它的 Wait 分支），
    /// 所以离包最近的那个就是这一包该救的人；多具尸体挨在一起时靠后面的候选兜底。</para>
    /// <para>▍为什么不用 <c>canOperate</c> 当判据：那是"此刻能不能被交互"，会受 <c>inOperate</c> 等临时状态影响；
    /// 本轮要的是"谁倒着"这个**事实**，统一取 <see cref="ActorState.Dead"/>（与放包处同一口径）。</para>
    /// </summary>
    private void CaptureTargets()
    {
        _captured = true;
        _targets.Clear();

        Vector3 self = transform.position;
        var players = ActorsManager.Players;
        for (int i = 0; i < players.Count; ++i)
        {
            var p = players[i];
            if (!p.IsValidMono() || p.ActorState != ActorState.Dead) continue;
            if (!p.transform.TryGetComponent(out Furniture_PlayerDown furn)) continue;
            _targets.Add(furn);
        }
        _targets.Sort((a, b) => SqrXz(a.transform.position, self).CompareTo(SqrXz(b.transform.position, self)));

        NetSyncLog.SyncLog("增援/锁定",
            $"{name} 出现于 {self}：倒地 {_targets.Count} 人，本次目标={(_targets.Count > 0 ? _targets[0].name : "<无>")}");
    }

    /// <summary>
    /// 检视器里面调用的（<c>LimitedLife.OnEnd</c> ⇒ 落地寿命结束时）。
    /// </summary>
    public void HelpPlayer()
    {
        // 出现时一个都没锁到（联机下"倒地"状态可能比战备复现晚到）⇒ 落地再补锁一次，总比这发白扔强
        if (_targets.Count == 0) CaptureTargets();

        var target = FirstDowned();
        if (target == null)
        {
            NetSyncLog.Warn("增援/救援", $"{name} 落地时创建期锁定的倒地者都已不倒地（被别的增援/队友救起）⇒ 本次不生效");
            return;
        }

        // ★ 强制：不再按距离找人（这个包在出现时就认定了救谁），也不再让 canOperate/inOperate 把这一发挡掉。
        //   ⚠ 必须走 Operate 而不是直接 ReviveSelf：盟友实例要靠它发出的 OnFurnitureOperate 上报 4028，
        //     否则只改了本端那份镜像，队友自己那边不会复活。
        if (!target.Handle(gameObject)) target.Operate();
        if (target.transform.TryGetComponent(out IHealth health)) health.Heal(100);
    }

    /// <summary>锁定列表里第一个**还倒着**的：优先"可交互"的（正常路径），其次仍处于 <see cref="ActorState.Dead"/> 的
    /// （倒地标志一时没置位也要救成，见 <see cref="HelpPlayer"/>）。已被别人救起的两个条件都不成立 ⇒ 跳过。</summary>
    private Furniture_PlayerDown FirstDowned()
    {
        Furniture_PlayerDown stillDead = null;
        for (int i = 0; i < _targets.Count; ++i)
        {
            var t = _targets[i];
            if (t == null) continue;
            if (t.canOperate) return t;
            if (stillDead == null && t.TryGetComponent(out IActor actor) && actor.ActorState == ActorState.Dead) stillDead = t;
        }
        return stillDead;
    }

    private static float SqrXz(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
}
