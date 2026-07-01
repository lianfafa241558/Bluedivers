using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PEMaths;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FpsGame.Mission
{
    /// <summary>
    /// 升旗
    /// </summary>
    [AddComponentMenu("任务/子任务/升旗", 30)]
    public class MissionSubFlagRaising : MissionBase
    {
        [SerializeField]
        private float defenseRange;
        bool startDefense;
        bool lastHavePlayer = true;

        Animator flag;

        private float nowTime;
        int adID;
        protected override void StartMission()
        {
            BattleEventSub.OnSelectAirdrop += OnSelectAirdrop;
            adID = data.cfg.RequiredAD[0].ID;
        }



        public override bool Tick()
        {
            base.Tick();
            var dis = Vector3.Distance(ActorsManager.Player.Pos, pos);
            if (!startDefense)
            {
                if (dis <= AirdropRange)
                {
                    UpdateTip("使用战略配备:<color=#FFF080>超级夏莱旗帜</color> ");
                }
                else if (dis < AirdropRange + 30)
                {
                    UpdateTip("移动至离目标更近的位置");
                }
                else if(!string.IsNullOrEmpty(tip))
                {
                     UpdateTip("");
                }
            }
            if (!startDefense || data.complete) return true;
            int count = AreaHavePlayer();
            nowTime += TickTime*count;

            if (nowTime < 0) return true;
            if (nowTime >= 100)
            {
                int enemyCount = BattleManager.Instance.FindUnits(new PECircle(entity.LogicPos, (int)defenseRange-5),TargetCfg.Enemy).Count;
                if (count>0)
                {
                    UpdateTip("肃清区域敌人  [剩余" + count + "]");
                }
                else
                {
                    CompleteMission();
                }
                return true;
            }
            if (nowTime==5|| nowTime==65)
            {
                BattleManager.Instance.CreatWave(WaveCreateParams.Extra.Set(pos));
            }
            

            if (lastHavePlayer != count > 0)
            {
                lastHavePlayer = !lastHavePlayer;
                if (!lastHavePlayer)
                {
                    //TODO:临时
                    CreatNotice("Kotama", "TaskPodUnvaildAble", () => !lastHavePlayer);
                }
            }

            var remainTime = 100 - nowTime;
            percentage = nowTime / 100f;
            if (count == 0)
            {
                if (nowTime > 0) nowTime -= TickTime;

                UpdateTip("<color=#FF4040>请返回升旗范围</color> [" + Tool.FloatToTime(remainTime) + "]");
                flag.speed = -1;
            }
            else
            {
                UpdateTip("升起旗帜  [" + Tool.FloatToTime(remainTime) + "]");
                flag.speed = count;
            }
            return true;
        }

        private int AreaHavePlayer()
        {
            return ActorsManager.Players.Count(item => Vector3.Distance(item.Pos, entity.Pos) < defenseRange);
        }


        private void OnSelectAirdrop(GameObject go,AirdropController.AirdropData data)
        {
            //这里多人情况下也得同步（现在没办法获取其他玩家按
            if (adID == data.cfg.ID&& Vector3.Distance(go.transform.position,pos)<=AirdropRange)
            {
                BattleEventSub.OnSelectAirdrop -= OnSelectAirdrop;
 
                BattleManager.Instance.ReleaseAirdrop(entity.Pos, adID, InitFlag);
            }
        }

        void InitFlag(GameObject flag)
        {
            this.flag = flag.GetComponent<Animator>();
            nowTime = -4;
            startDefense = true;
        }


    }
}