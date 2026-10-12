using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Game;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 玩家倒地组件（本机玩家与联机盟友实例共用）。
    /// <para>▍两侧口径不同：本机玩家靠 <c>HealthPlayer.OnDie</c> 置"可救"；盟友实例的血量是**镜像**
    /// （<c>FriendController.ApplyVital</c> 直写字段、不触发 OnDie）⇒ 必须由 <see cref="ApplyRemoteDown"/> 显式驱动，
    /// 否则盟友永远 <c>canOperate == false</c>、救不起来。</para>
    /// </summary>
    [AddComponentMenu("交互/倒地救援")]
    public class Furniture_PlayerDown : Furniture_Attached
    {

        HealthPlayer Health { get; set; }

        protected override void Awake()
        {
            base.Awake();
            Health = GetComponent<HealthPlayer>();
            canOperate = false;
            if (Health != null) Health.OnDie += OnDown;
        }

        private void OnDestroy()
        {
            if (Health != null) Health.OnDie -= OnDown;
            // 倒地中离场（盟友实例被销毁 / 换场景）：把本机的"待救"授权还回去，否则增援授权计数会漏一个
            if (canOperate) BattleEventBus.RequestAuthorize(Constants.HealBag, false);
        }

        public override void Operate()
        {
            base.Operate();
            ReviveSelf();
        }

        /// <summary>本机执行复活：本地玩家被救、以及收到 <c>PlayerReviveNtf</c>(4050) 时都走这一条。</summary>
        public void ReviveSelf()
        {
            // ⚠ 幂等（很重要）：救人路径不止一条 —— `PlayerOperationController` 手动扶、`HealBag`（增援）、
            //   `SpecUnitKei`（凯伊）都会调 `Handle → Operate`，而它们在各机器上**各跑一遍** ⇒ 同一个人
            //   可能连着收到两次 4050。而 `HealthPlayer.Revive` 会把血量压回 `MaxHealth/10`
            //   ⇒ 刚被加满的血会被第二次"复活"拉掉（10%）。
            if (Health != null && Health.GetHpCurrent() > 0f)
            {
                canOperate = false;
                return;
            }

            if (Health != null) Health.Revive();
            canOperate = false;
            BattleEventBus.RequestAuthorize(Constants.HealBag, false);
        }

        /// <summary>【联机】按同步来的"倒地"标志置位——盟友实例的血量是镜像值，改血量不会走 <c>OnDie</c>。
        /// <para>⚠ 幂等：救人的那一下已经把 <c>canOperate</c> 置回 false，重复的 down=false 消息不能重复归还授权。</para>
        /// <para>⚠ 这条路径**拿不到伤害来源**（盟友的死亡结算发生在对方机器上）⇒ 播报会省掉"被谁"。</para></summary>
        public void ApplyRemoteDown(bool down) => ApplyDown(down, null);

        /// <summary>倒地置位（本机 <c>Health.OnDie</c> 与联机 down 标志共用同一条链）。</summary>
        private void ApplyDown(bool down, GameObject source)
        {
            if (down == canOperate) return;
            canOperate = down;
            BattleEventBus.RequestAuthorize(Constants.HealBag, down);
            if (down)
            {
                PlaySound(audioClose);
                // 消息窗：谁被谁击倒了。名字/颜色都走 GameMsg.Of（玩家单位取玩家名 + 角色色），
                // 来源同一个漏斗（无源伤害 / 认不出 ⇒ 空串 ⇒ 播报省掉"被谁"）。
                GameMsg.PlayerDown(GameMsg.Of(Identity), GameMsg.NameOf(source));
            }
        }

        void OnDown(GameObject source)
        {
            ApplyDown(true, source);
        }
    }
}
