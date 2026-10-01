using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using PEMaths;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.AI
{
    /// <summary>
    /// 嘲讽：搜索范围内的敌人，强制将其目标设置为自己
    /// </summary>
    [AddComponentMenu("技能/嘲讽", 30)]
    public class UnitSkill_Taunt : UnitSkill_Base
    {
        [SerializeField]
        private int effectRadius;

        protected override void SkillStart()
        {
            var self = m_Controller.Actor;
            var list = FPSGame.Game.UnitQuery.FindUnits(
                new PECircle(self.LogicPos, effectRadius),
                TargetCfg.EnemyAI,
                item => item != self
                    && item.Team != self.Team
                    && FpsHelper.VaildTarget(item));

            foreach (var unit in list)
            {
                var detect = unit.transform.GetComponentInChildren<DetectionModule>();
                if (detect != null)
                {
                    //强制目标设置为嘲讽者自身
                    detect.SetTargetActor(self);
                }
            }
        }
    }
}
