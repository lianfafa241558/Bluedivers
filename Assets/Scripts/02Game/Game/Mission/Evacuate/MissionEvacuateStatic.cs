using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using Utils;
namespace FpsGame.Mission
{
    //第一步 空投一个信标下来√播放语音
    //第二步 记录这个信标，等待玩家操作完成
    //第三步 等待倒计时√播放语音
    //第四步 创建运输机并检查下面有没有人，没有就滞空
    //第五步 落地等待登机
    //第六步 起飞


    [AddComponentMenu("任务/撤离/静态撤离", 30)]
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
        [SerializeField]
        private Transform _startPointEntity;

        private EvacuateState stage;
        private int countDown;
        private int suspendCountDown;

        private int m_EvacuateTime = 120;//撤离时间
        private int m_EvacuateRange = 30;//撤离范围
        private int suspendTime = 10;
        private Vector3 areaPoint;

        Transform area, beacon;
        KeyScreen keyScreen;

        MedivacController medivac;
        bool IsComplete;
        
        


        protected override void StartMission()
        {

           

            
        }

        protected override void InitMission()
        {
            if (!entity)
            {
                if (IsFast)
                {

                    var go = GameObject.FindWithTag("StartPoint");
                    if (go)
                    {
                        Debug.LogWarning("尝试设置init" + gameObject, gameObject);
                        entity = go.GetComponent<MissionView>();
                        entity.Init(this, new int[0]);
                    }
                    else
                    {
                        base.InitMission();
                    }
                }
                else
                {
                    base.InitMission(); 
                }
            }


            if (IsFast)
            {
                m_EvacuateTime = 10;
                m_EvacuateRange = 999;
            }
            pos = entity.Pos;
            area = entity.transform;
            areaPoint = area.transform.position;
            // 注意：不能用 ?? 对 Unity Object 判空（无法识别未赋值/已销毁的伪 null），改用 Unity 重载的 != null
            Transform point = _startPointEntity != null ? _startPointEntity : area;
            if (point == null)
            {
                Debug.LogError("撤离任务起始点为空：_startPointEntity 与 area 均为空，无法创建撤离单位", this);
                return;
            }

            ResSvc.Instance.CreatPrefab("Prefabs/BattleBase/Kei", false, point.TransformPoint(0, 0, 10));
            medivac = ResSvc.Instance.CreatPrefab("Prefabs/BattleBase/NeoNimbus", true, point.TransformPoint(0, 12, 0)).GetComponent<MedivacController>();
            medivac.SetType(MedivacController.MedivacState.Land);
            medivac.targetPoint = point;
        }

        public override void Link(MissionBase mission)
        {
            mission.OnMissionEnd += Activation;

        }

        public void Activation(MissionBase mission)
        {
            mission.OnMissionEnd -= Activation;
            IsComplete = mission.completed;
            stage = EvacuateState.Activation;

            RemoveTag(MissionTag.hideAll);
            AddTag(MissionTag.IsActive);
            BattleEventSub.MissionStateChange(this, true);
            BattleEventSub.MissionEnityShow(entity);
            UpdateText("激活撤离终端", "");
        }

        public override bool Tick()
        {
            switch (stage)
            {
                case EvacuateState.Activation:
                    if (--countDown == -5)
                    {
                        CreatNotice("Yuuka", IsComplete?"Evacuate": "EvacuateFail");
                        BattleManager.Instance.ReleaseAirdrop(areaPoint, 0, InitBeacon);
                    }
                    if (IsFast && keyScreen)
                    {
                        keyScreen.SetStage(keyScreen.procedure.Count - 1);
                    }

                    break;
                case EvacuateState.Wait:
                    if (AreaHavePlayer())
                    {
                        --countDown;
                        suspendCountDown = suspendTime;
                        UpdateTip("请在撤离区坚守 [" + Tool.FloatToTime(countDown) + "]");
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
                            CreatNotice("Ayane", "WarnArea");
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
                        CreatNotice("Yuuka", IsComplete? "End":"Fail");
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
                else */
                if (item.type == KeyScreen.ProcedureType.Wait)
                {
                    item.time = m_EvacuateTime;
                }
            }

            keyScreen.OnUpdateStage += OnKeyScreenStage;
        }

        private void OnKeyScreenStage(int stage)
        {
            if (stage == keyScreen.procedure.Count - 1) StartWait();
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
            if(IsComplete) AudioSvc.PlayMusic(AudioSvc.MusicGroup.Evacuate, 0.5f);
            CreatNotice("Ayane", "CountDownBegins");

        }

        private void EndWait()
        {
            stage = EvacuateState.CompleWait;
            CreatNotice("Ayane", "CountDownEnd");
            countDown = 5;
            UpdateText("运输船即将着陆", "请肃清着陆区");
            medivac = ResSvc.Instance.CreatPrefab("Prefabs/BattleBase/NeoNimbus", true, areaPoint + Vector3.up * 500).GetComponent<MedivacController>();
            medivac.transform.LookAt(areaPoint);
            //medivac.Init();
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
                CreatNotice("Ayane", "Hover");
                UpdateText("运输船无法着陆", "请靠近撤离区");
            }

        }
        void Landing()
        {
            stage = EvacuateState.Land;
            CreatNotice("Ayane", "Landing");
            countDown = 5;
            UpdateText("运输船即将着陆", "");
            beacon.GetComponent<Animator>().Play("Hide");
            medivac.transform.position = areaPoint + (Vector3.up * 5.5f + area.forward * +10) * medivac.transform.lossyScale.x;
            GameRoot.CreatePerTimer(() => {
                medivac.transform.position = Vector3.Lerp(medivac.transform.position, areaPoint + (Vector3.up * 5.5f + area.forward * -5.5f) * medivac.transform.lossyScale.x, 15 * Time.deltaTime);
            }, 3, null);

            medivac.Play("Land");
        }


        void Suspend()
        {
            AudioSvc.StopMusic();
            stage = EvacuateState.Activation;
            CreatNotice("Ayane", "Suspend");
            UpdateText("激活撤离终端", "");
            keyScreen.SetStage(0);

        }

        void Evacuate()
        {
            stage = EvacuateState.Evacuate;
            //CreatNotice("Ayane", "Suspend");
            UpdateText("进入雨云号", "");
        }

        void End()
        {
            stage = EvacuateState.End;
            countDown = 6;
            CreatNotice("Ayane", "TakeOff");
            //WndManager.Instance.movieWnd.SetWndState(true);
            GameRoot.GameState = Core.GameStateEnum.Transition;//测试，不确定
        }


        private bool AreaHavePlayer()
        {
            return ActorsManager.Players.Any(item => Vector3.Distance(item.transform.position, areaPoint) < m_EvacuateRange);
        }


    }

}