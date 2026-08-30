using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;
using PEMaths;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

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
            var list = BattleManager.Instance.FindUnits(
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
