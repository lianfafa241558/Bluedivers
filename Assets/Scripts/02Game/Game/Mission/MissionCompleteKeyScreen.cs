using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FpsGame.Mission
{
    /// <summary>
    /// 完成控制面板
    /// </summary>
    [AddComponentMenu("任务/完成控制面板", 30)]
    public class MissionCompleteKeySceern : MissionBase
    {
        
        protected KeyScreen keyScreen;

        protected override void CreatMission()
        {
            keyScreen = entity.GetComponentInChildren<KeyScreen>();
            keyScreen.OnComple += OnComple;
            keyScreen.OnUpdateStage += OnUpdateStage;
        }
        protected override void Uninit()
        {
            base.Uninit();
            if (!keyScreen.IsValid()) return;
            keyScreen.OnComple -= OnComple;
            keyScreen.OnUpdateStage -= OnUpdateStage;
        }

        protected virtual void OnComple()
        {
            CompleteMission();
        }
        protected virtual void OnUpdateStage(int stage)
        {
            if (!keyScreen.IsActive|| keyScreen.IsEnd) return;
            UpdateTip(keyScreen.nowProcedure.tip);
            if (keyScreen.nowProcedure.type != KeyScreen.ProcedureType.Load && keyScreen.nowProcedure.type != KeyScreen.ProcedureType.Wait)
            {
                percentage = 0f;
                TickTime = 1f;
                UpdateMission();
            }
            else
            {
                TickTime = 0.2f;
            }

        }

        public override bool Tick()
        {
            if (!HasTag(GameContract.MissionTag.IsActive)) return true;
            if (!keyScreen.IsActive)
            {
                float dis = Vector2.Distance(ActorsManager.Player.Pos.ToVector2(), pos.ToVector2());

                bool airdropRange = dis < entitySize+10;
                UpdateTip(airdropRange?"激活终端":"");

                return true;
            }
            if (keyScreen.IsEnd) return true;
            if (keyScreen.nowProcedure.type== KeyScreen.ProcedureType.Load|| keyScreen.nowProcedure.type== KeyScreen.ProcedureType.Wait)
            {
                percentage = keyScreen.GetTime() / keyScreen.nowProcedure.time;
                UpdateMission();
            }
            return true;
        }
    }

}