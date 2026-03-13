using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
using Utils;
/// <summary>
/// 黑盒子
/// </summary>
public class MissionBlackBox : MissionBase
{
    [SerializeField]
    private float defenseRange;

    KeyScreen keyScreen;
    bool startDefense;
    bool lastHavePlayer = true;

    protected override void Start()
    {
        keyScreen = entity.transform.GetComponentInChildren<KeyScreen>(true);
        keyScreen.OnUpdateStage += OnUpdateStage;
        base.Start();
        TickTime = 0.5f;
    }

    void OnUpdateStage(int stage)
    {
        switch (stage)
        {
            case 1:
                startDefense = true;
                GameRoot.CreateTimer(()=>BattleManager.Instance.CreatWave(entity.Pos,true),5);
                break;
            case 2:
                CompleteMission();
                break;
        }
    }
    public override bool Tick()
    {
        base.Tick();
        if (!startDefense|| data.complete) return true;

        if (lastHavePlayer!=AreaHavePlayer())
        {
            lastHavePlayer = !lastHavePlayer;
            if (!lastHavePlayer)
            {
                //TODO:临时的
                CreatNotice("Kotama", "TaskPodUnvaildAble", () => !lastHavePlayer);
            }
        }
       
        var nowTime = keyScreen.GetTime();
        var remainTime = keyScreen.nowProcedure.time- nowTime;
        percentage = nowTime / keyScreen.nowProcedure.time;
        if (!lastHavePlayer)
        {
            if(nowTime >0) keyScreen.AddTime(2*TickTime);
            
            UpdateTip("<color=#FF4040>请返回信号发射范围</color> [" + Tool.FloatToTime(remainTime) + "]");
        }
        else
        {
            UpdateTip("请在信号发射区坚守  [" + Tool.FloatToTime(remainTime) + "]");
        }
        return true;
    }

    private bool AreaHavePlayer()
    {
        return ActorsManager.Players.Any(item => Vector3.Distance(item.Pos, entity.Pos) < defenseRange);
    }
}
