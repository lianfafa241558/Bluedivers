using System;
using System.Collections.Generic;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 榴弹背包：吃掉一格 ⇒ 给玩家的投掷物槽补一份弹药（走 <see cref="PlayerController.UseThrowBag"/>）。
    /// <para>▍回充：玩家吃补给时（<see cref="PlayerController.UseSupply"/> 是唯一入口）由事件钩子补回一格。</para>
    /// </summary>
    [AddComponentMenu("装备/榴弹背包")]
    public class GrenadeBag : AmmoBagBase
    {
        protected override bool OnUse()
        {
            // 远端盟友背上那份是重放实例（BagBase.OnInstall 取不到 PlayerController）⇒ 不成立，别扣格
            if (m_PlayerCharacterController == null) return false;

            m_PlayerCharacterController.UseThrowBag();
            return true;
        }

        protected override void OnRefill()
        {
            // 只还原视觉：弹药本身在玩家吃补给时已由 UseSupply → UseThrowBag 一并补满
        }

        /// <summary>补给钩子：只认同一个玩家的补给，别人的补给不该给我的背包充格。</summary>
        private void HandleSupplyUsed(PlayerController user)
        {
            if (user == null || user != m_PlayerCharacterController) return;
            TryRefill();
        }

        public override void OnInstall(IActor actor, Func<IEnumerable<IEquippable>> getEquippableList)
        {
            base.OnInstall(actor, getEquippableList);

            GlobalEventBus.OnSupplyUsed -= HandleSupplyUsed; // 幂等：反复装卸不重复挂
            GlobalEventBus.OnSupplyUsed += HandleSupplyUsed;
        }

        public override void OnUninstall()
        {
            GlobalEventBus.OnSupplyUsed -= HandleSupplyUsed;
            base.OnUninstall();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            GlobalEventBus.OnSupplyUsed -= HandleSupplyUsed;
        }
    }
}
