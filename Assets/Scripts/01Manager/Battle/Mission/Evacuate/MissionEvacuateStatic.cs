using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

//第一步:空投一个信标下来√播放语音√
//第二步:记录这个信标，等待玩家操作完成√
//第三步:等待倒计时√播放语音√
//第四步:创建运输机并检查下面有没有人，没有就滞空
//第五步:落地等待登机
//第六步:起飞



public class MissionEvacuateStatic : MissionBase
{
    enum EvacuateState
    {
        Disable,
        Activation,
        Wait,
        CompleWait,
        Land,
        Hover,
        Suspend,
        Evacuate,
        End
    }
    public bool IsFast;

    private EvacuateState stage;
    private int countDown;
    private int suspendCountDown;

    private int m_EvacuateTime = 120;//撤离时间
    private int m_EvacuateRange = 30;//撤离范围
    private int suspendTime = 10;
    private Vector3 areaPoint;

    Transform area,beacon;
    KeyScreen keyScreen;

    MedivacController medivac;

 
    protected override void CreatMission()
    {
        base.CreatMission();
        if (IsFast)
        {
            m_EvacuateTime = 10;
            m_EvacuateRange = 999;
        }
        area = entity.transform;
        areaPoint = area.transform.position;

    }

    public override void Link(MissionBase mission)
    {
        mission.OnMissionCompleted += Activation;

    }

    public void Activation(MissionBase mission)
    {
        mission.OnMissionCompleted -= Activation;
        stage = EvacuateState.Activation;
        hide = false;
        GlobalEventManager.MissionStateChange(this, true);
        GlobalEventManager.MissionShow(this);
        UpdateText("激活撤离终端", "");
    }

    public override bool Tick()
    {
        switch (stage)
        {
            case EvacuateState.Activation:
                if (--countDown == -3)
                {
                    WndManager.Instance.CreatNotice("Yuuka2", "Evacuate");
                    BattleManager.Instance.ReleaseAirdrop(areaPoint, 0, InitBeacon);
                }
                if (IsFast&& keyScreen)
                {
                    keyScreen.SetStage(keyScreen.procedure.Count - 1);
                }

                break;
            case EvacuateState.Wait:
                if (AreaHavePlayer())
                {
                    --countDown;
                    suspendCountDown = suspendTime;
                    UpdateTip("请在撤离区坚守  [" + Tool.FloatToTime(countDown) + "]");
                    if (countDown <= 0)
                    {
                        EndWait();
                        return true;
                    }
                }
                else
                {
                    keyScreen.AddTime(1);
                    if (suspendCountDown == suspendTime)
                    {
                        WndManager.Instance.CreatNotice("Ayane2", "WarnArea");
                    }
                    UpdateTip("<color=#FF4040>请返回撤离区范围  [" + Tool.FloatToTime(--suspendCountDown) + "]</color>");
                    if (suspendCountDown <= 0)
                    {
                        Suspend();
                        return true;
                    }
                }


                break;
            case EvacuateState.CompleWait:
                if (--countDown <= 0)
                {
                    CheckHover();
                }
                break;
            case EvacuateState.Land:
                if (--countDown <= 0)
                {
                    Evacuate();
                }

                break;

            case EvacuateState.Hover:
                if (AreaHavePlayer())
                {
                    Landing();
                }
                break;
            case EvacuateState.Evacuate:

                break;
            case EvacuateState.End:
                if (--countDown == 0)
                {
                    WndManager.Instance.CreatNotice("Yuuka2", "End");
                }
                break;
        }

        return true;
    }




    void InitBeacon(GameObject beacon)
    {
        this.beacon = beacon.transform;
        keyScreen = beacon.GetComponentInChildren<KeyScreen>();
        //var tower = area.Find("SignaTower").GetComponent<Furniture_Base>();
        //var bolts = area.FindAll(item=>item.name.Contains("Bolt")).Select(item=>item.GetComponent<Furniture_Base>()).ToList();
        if (IsFast)
        {

        }
        foreach (var item in keyScreen.procedure)
        {
            /*
            if(item.type == KeyScreen.ProcedureType.ActionItem)
            {
                item.furns = bolts;
            }
            else if (item.type == KeyScreen.ProcedureType.Direction|| item.type == KeyScreen.ProcedureType.Load)
            {
                item.furns.Add(tower);
            }
            else */if (item.type == KeyScreen.ProcedureType.Wait)
            {
                item.time = m_EvacuateTime;
            }
        }

        keyScreen.OnUpdateStage += OnKeyScreenStage;
    }

    private void OnKeyScreenStage(int stage)
    {
        if(stage==keyScreen.procedure.Count - 1) StartWait();
    }

    private void StartWait()
    {
        if (keyScreen.owner)
        {
            keyScreen.owner.GetComponent<PlayerSpeechManager>().Speech(SpeechTypeEnum.Evacuate);
        }
        stage = EvacuateState.Wait;
        UpdateText("运输船接近中", "");
        countDown = m_EvacuateTime;//如果有撤离效果就变短
        AudioManager.PlayMusic(AudioManager.MusicGroup.Evacuate,0.6f);
        WndManager.Instance.CreatNotice("Ayane2", "CountDownBegins",delay:1);

    }

    private void EndWait()
    {
        stage = EvacuateState.CompleWait;
        WndManager.Instance.CreatNotice("Ayane2", "CountDownEnd");
        countDown = 5;
        UpdateText("运输船即将着陆", "请肃清着陆点");
        medivac= ResManager.Instance.CreatPrefab("Prefabs/BattleBase/NeoNimbus", true, areaPoint + Vector3.up*500).GetComponent<MedivacController>();
        medivac.transform.LookAt(areaPoint);
        medivac.Init();
        medivac.Complete += End;
        medivac.SetType(MedivacController.MedivacState.Evacuate);
        medivac.Play("Idle");
        GameRoot.CreatePerTimer(() => {
            medivac.transform.position = Vector3.Lerp(medivac.transform.position, (Vector3.up * 50) * medivac.transform.lossyScale.x, 30 * Time.deltaTime);
            medivac.transform.eulerAngles = Vector3.Lerp(medivac.transform.eulerAngles, area.eulerAngles, 30 * Time.deltaTime);
        }, 3, null);
    }
    private void CheckHover()
    {
        if (AreaHavePlayer())
        {
            Landing();
        }
        else
        {
            stage = EvacuateState.Hover;
            WndManager.Instance.CreatNotice("Ayane2", "Hover");
            UpdateText("运输船无法着陆", "请靠近撤离区域");
        }
        
    }
    void Landing()
    {
        stage = EvacuateState.Land;
        WndManager.Instance.CreatNotice("Ayane2", "Landing");
        countDown = 5;
        UpdateText("运输船即将着陆", "");
        beacon.GetComponent<Animator>().Play("Hide");
        medivac.transform.position = areaPoint +( Vector3.up * 5.5f + area.forward * +10)*medivac.transform.lossyScale.x;
        GameRoot.CreatePerTimer(() => { 
            medivac.transform.position = Vector3.Lerp(medivac.transform.position, areaPoint + (Vector3.up * 5.5f + area.forward * -5.5f) * medivac.transform.lossyScale.x, 15 * Time.deltaTime);
        }, 3, null);

        medivac.Play("Land");
    } 


    void Suspend()
    {
        AudioManager.StopMusic();
        stage = EvacuateState.Activation;
        WndManager.Instance.CreatNotice("Ayane2", "Suspend");
        UpdateText("激活撤离终端", "");
        keyScreen.SetStage(0);

    }

    void Evacuate()
    {
        stage = EvacuateState.Evacuate;
        //WndManager.Instance.CreatNotice("Ayane2", "Suspend");
        UpdateText("进入雨云号", "");
    }

    void End()
    {
        stage = EvacuateState.End;
        countDown = 6;
        WndManager.Instance.CreatNotice("Ayane2", "TakeOff");
        WndManager.Instance.movieWnd.SetWndState(true);

    }


    private bool AreaHavePlayer()
    {
        return ActorsManager.Players.Any(item=>Vector3.Distance(item.transform.position, areaPoint) < m_EvacuateRange);
    }


}

