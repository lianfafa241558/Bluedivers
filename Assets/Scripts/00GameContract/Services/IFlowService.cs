using System;
using System.Collections;
using FPSGame.Core;
using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 游戏流程状态 服务契约（2026-09-30 为拆出 <c>05_UnitCore</c> 而建）。
    ///
    /// <para>▍为什么需要它：<c>Actor</c> 有一段"等进入主流程"的等待逻辑（`while (!IsMainStage())`），
    /// 原先调 `FpsHelper.IsMainStage()`，而后者读 <c>GameRoot.GameState</c> ⇒ 单位内核反向依赖 01Manager。</para>
    ///
    /// <para>▍它后来承担了三件事（P5-0 实测补充）：查游戏状态（<c>GameState</c>/<c>IsMainStage</c>）、
    /// 查是否本地实例（<c>IsLocal</c>，给 <c>LogicBehaviour</c>）、以及**代下层跑协程/建定时器**
    /// （<c>RunCoroutine</c>/<c>CreateTimer</c>/<c>CreatePerTimer</c>）——因为下层拿不到 <c>GameRoot.Instance</c>
    /// 这个静态门面（老坑）。</para>
    ///
    /// <para>⚠ <b>不能把它并进 <see cref="IBattleService"/></b>：Bridge/Ready 场景里并没有 <c>BattleManager</c>，
    /// 而这段等待逻辑恰恰要在这些状态下判定为"已进入主流程"。所以由**状态的所有者** <c>GameRoot</c> 实现并注册。</para>
    ///
    /// <para>⚠ 实现方注意：<c>GameRoot</c> 的定时器是 **静态** 方法（在 <c>GameRootBase</c>）⇒ 接口成员需显式转发。</para>
    /// </summary>
    public interface IFlowService
    {
        /// <summary>当前游戏状态。</summary>
        GameStateEnum GameState { get; }

        /// <summary>是否已进入主流程（Game / Ready / Bridge；与 <c>FpsHelper.IsMainStage()</c> 同义）。</summary>
        bool IsMainStage { get; }

        /// <summary>是否本地实例（等价 <c>GameRoot.IsLocal</c>）。</summary>
        bool IsLocal { get; }

        /// <summary>在游戏根节点上跑协程（下层拿不到 <c>GameRoot.Instance</c>，故由此代为启动）。</summary>
        Coroutine RunCoroutine(IEnumerator routine);

        /// <summary>设置游戏状态（等价 <c>GameRoot.GameState</c> 的 setter，会触发 <c>GlobalEventSub.SceneChange</c>；玩法层 2 处使用）。</summary>
        void SetGameState(GameStateEnum state);

        /// <summary>创建一次性定时器（等价 <c>GameRoot.CreateTimer</c>）。</summary>
        LogicTimer CreateTimer(Action cb, float waitTime, int counter = 1, Action endcb = null);

        /// <summary>创建一次性定时器（带计数回调版本）。</summary>
        LogicTimer CreateTimer(Action<int> cb, float waitTime, int counter = 1, Action endcb = null);

        /// <summary>创建循环定时器（等价 <c>GameRoot.CreatePerTimer</c>）。</summary>
        LogicTimer CreatePerTimer(Action percb, float waitTime, Action endcb = null);
    }
}
