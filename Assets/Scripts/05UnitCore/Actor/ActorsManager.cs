using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Utils;      // Tool.Destroy（00_Utils 已在 05_UnitCore.asmdef 的 references 里）

using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 维护场上所有单位的注册与查询。
    /// </summary>
    [AddComponentMenu("管理/单位注册表")]
    public class ActorsManager : Singleton<ActorsManager>
    {
        public static Queue<KVP<UnitTypeEnum, IActor>> OnActorCreat=new();//用来创建跟随UI的

        public static List<IActor> Enemys { get; private set; } = new();
        public static List<IActor> Actors { get; private set; } = new();
        public static List<IActor> Players { get; private set; } = new();
        public static List<IActor> SpecUnits { get; private set; } = new();


        /// <summary>本局敌人 <c>NetId</c> 计数器（每局在 <see cref="Awake"/> 归零）。
        /// <para>▍为什么计数挂在这里：分配点只有 <c>Actor.Awake</c> 一处（见 <c>Actor.NetId</c>），
        /// 它比任何刷怪调用点都早、且枚举不全刷怪路径 ⇒ 需要一个"每局一份、且早于全部敌人"的对象来持计数。</para></summary>
        private static int _enemyNetIdSeq;

        public static IActor Player { get; private set; }

        /// <summary>
        /// 离 <paramref name="point"/> 最近的玩家（本地玩家与盟友幽灵都在 <see cref="Players"/> 里）。
        ///
        /// <para>▍为什么口径是"最近"而不是"本机玩家"：刷怪中心若取本机玩家位置，两端算出的点必然不同
        /// （各端只有自己那台是真身，盟友是同步过来的近似位置）⇒ 同一波怪会落到不同地方。
        /// 取"离锚点最近的玩家"则两端**用同一份已同步的位置**算出同一个答案（2026-10-07 用户口径）。</para>
        /// </summary>
        public static IActor NearestPlayer(Vector3 point)
        {
            IActor best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < Players.Count; ++i)
            {
                var p = Players[i];
                if (p == null) continue;
                // ⚠ 接口引用不能靠 Unity 的假 null（`== null` 判不出已销毁）⇒ 借组件引用判一次
                var comp = p as Component;
                if (comp == null) continue;
                float sqr = (comp.transform.position - point).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = p;
            }
            return best;
        }

        /// <summary>同 <see cref="NearestPlayer"/>，直接给位置；没有玩家时用锚点兜底。</summary>
        public static Vector3 NearestPlayerPos(Vector3 point)
        {
            var p = NearestPlayer(point);
            return p != null ? p.transform.position : point;
        }
        public void RegisterPlayer(IActor player)
        {
            Player = player;
            Players.Add(player);
            OnActorCreat.Enqueue(new(UnitTypeEnum.Player, player));
            //Debug.LogError("玩家出生" + player + "  " + player.transform.position, player.transform);
        }
        public void RegisterFriend(Actor friend)
        {
            Players.Add(friend);
            OnActorCreat.Enqueue(new(UnitTypeEnum.Friend, friend));
        }
        public void RegisterSpecUnit(Actor specUnit)
        {
            //Debug.LogError("响应特殊单位出生"+ specUnit, specUnit);
            SpecUnits.Add(specUnit);
            OnActorCreat.Enqueue(new(UnitTypeEnum.SpecUnit, specUnit));
        }
        public void RegisterEnemy(Actor specUnit)
        {
            //Debug.LogError("特殊单位出生"+ specUnit, specUnit);
            Enemys.Add(specUnit);
        }

        /// <summary>【联机】分配下一个敌人网络标识（唯一调用点 = <c>Actor.Awake</c>）。
        /// <para>按创建顺序自增 ⇒ 两端同序同号；⚠ 未经 <see cref="Instance"/> 判定不得调用（见调用点注释）。</para></summary>
        public static int NextEnemyNetId() => ++_enemyNetIdSeq;
        /// <summary>
        /// 【单位死亡事件入口】⚠ 死亡 **只是"倒地"**，单位还在场上、还可能被救起
        /// <para>▍"离场"走的是另一条路（对象被销毁）⇒ 见 <see cref="Unregister"/>。</para>
        /// </summary>
        public void UnRegisterUnit(Actor actor)
        {
            switch (actor.Type)
            {
                case UnitTypeEnum.Player:
                    //不移除（见上面注释：倒地不离开队伍，复活还是同一个 Actor）
                    break;
                case UnitTypeEnum.Friend:
                    //不移除（见上面注释：倒地待救，救援系统还要在 Players 里找它）
                    break;
                case UnitTypeEnum.Enemy:
                    Enemys.Remove(actor);
                    Actors.Remove(actor);
                    break;
                case UnitTypeEnum.SpecUnit:
                case UnitTypeEnum.Other:
                    SpecUnits.Remove(actor);
                    Actors.Remove(actor);
                    break;
            }
        }

        /// <summary>
        /// 【对象被销毁时调用（<c>Actor.OnDestroy</c>）】把单位从**所有**注册表里摘掉，
        /// <para>▍与 <see cref="UnRegisterUnit"/> 的分工：**销毁 ⇒ 不可能再复活，条目一定是垃圾 ⇒ 全部移除**；
        /// </summary>
        public static void Unregister(IActor actor)
        {
            if (actor == null) return;
            // 销毁期间读被打断的对象有风险，而"某类只会出现在某表"是假设；
            // 一次清干净可能存在的重复项。
            Actors.RemoveAll(x => ReferenceEquals(x, actor));
            Enemys.RemoveAll(x => ReferenceEquals(x, actor));
            SpecUnits.RemoveAll(x => ReferenceEquals(x, actor));
            Players.RemoveAll(x => ReferenceEquals(x, actor));
        }

        /// <summary>
        /// 【单位离场】摘注册表 + 销毁 GameObject（两个动作必须成对、顺序固定，所以封在一起）。
        /// <para>⚠ 顺序反了（先 Destroy 后摘表）就会 MissingReferenceException：注册表被一堆系统每帧遍历。</para>
        /// <para>⚠ 刻意不叫 <c>Kill</c>：<see cref="Actor.Kill"/> 是"致死"（走 <c>OnDie</c> 死亡链，单位还在场上、能被救起），
        /// 与本方法"离场销毁"不是一回事。</para>
        /// </summary>
        public static void Despawn(IActor actor)
        {
            if (actor == null) return;

            Unregister(actor);                  // ① 先摘表

            // ② 再销毁。⚠ 接口引用不能用 `?.` / `??`：绕过 Unity 的假 null 重载，会把已销毁对象判成有效
            var comp = actor as Component;
            if (comp == null) return;           // 已销毁 / 不是组件 ⇒ 表已摘干净，收工
            Tool.Destroy(comp.gameObject);
        }

        public override void Awake()
        {
            //不管这个新的单例会不会被覆盖，都刷新列表
            Actors = new();
            Players = new();
            SpecUnits = new();
            OnActorCreat = new();
            _enemyNetIdSeq = 0;   // 本局的敌人编号从头开始（分配点会先判 Instance，保证不会有人在归零之前分号）
            base.Awake();
            UnitEventBus.OnPlayerCreate += RegisterPlayer;
            UnitEventBus.OnFriendCreate += RegisterFriend;
            UnitEventBus.OnSpecUnitCreate += RegisterSpecUnit;
            UnitEventBus.OnUnitDeath += UnRegisterUnit;
            UnitEventBus.OnEnemyCreate += RegisterEnemy;
        }
        private void OnDestroy()
        {
            UnitEventBus.OnPlayerCreate -= RegisterPlayer;
            UnitEventBus.OnFriendCreate -= RegisterFriend;
            UnitEventBus.OnSpecUnitCreate -= RegisterSpecUnit;
            UnitEventBus.OnUnitDeath -= UnRegisterUnit;
            UnitEventBus.OnEnemyCreate -= RegisterEnemy;
        }
    }

}
