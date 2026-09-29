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
    public class MissionEvacuateStatic : MissionEvacuateBase
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


        private EvacuateState stage;

        private int m_EvacuateTime = 120;//撤离时间
        private int m_EvacuateRange = 30;//撤离范围
        private int suspendTime = 10;

        Transform beacon;
        KeyScreen keyScreen;

        /// <summary>本次撤离呼叫出去的所有信标(0号战备)：本任务的 + 各次要撤离区的</summary>
        private readonly List<GameObject> _beacons = new();

        protected override void InitMission()
        {
            base.InitMission();
            if (IsFast)
            {
                m_EvacuateTime = 5;
                m_EvacuateRange = 999;
            }
        }

        public override void Activation(MissionBase mission)
        {
            base.Activation(mission);
            stage = EvacuateState.Activation;
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
                        //呼叫撤离信标
                        BattleManager.Instance.ReleaseAirdrop(areaPoint, 0, InitBeacon);
                        //次要撤离区同样各呼叫一个信标，玩家可以自行选择在哪一处撤离
                        CallSecondaryBeacons();
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
            _beacons.Add(beacon);
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

        private void OnKeyScreenStage(int nowStage)
        {
            //只有还停在"激活"阶段才响应：本终端已经激活过、或撤离点已被次要撤离区接管，就忽略
            if (this.stage != EvacuateState.Activation) return;
            if (nowStage != keyScreen.procedure.Count - 1) return;
            //主撤离点先被激活：次要撤离区的信标全部收起
            HideOtherBeacons(beacon ? beacon.gameObject : null);
            StartWait();
        }

        /// <summary>让每个次要撤离区各呼叫一个撤离信标(0号战备)</summary>
        private void CallSecondaryBeacons()
        {
            foreach (var zone in _secondaryZones)
            {
                if (!zone) continue;
                zone.OnBeaconReady += OnSecondaryBeaconReady;
                zone.OnActivated += OnSecondaryActivated;
                zone.CallBeacon(m_EvacuateTime);
            }
        }

        private void OnSecondaryBeaconReady(MissionEvacuateSecondary zone)
        {
            if (!zone.Beacon) return;
            _beacons.Add(zone.Beacon);
        }

        /// <summary>玩家在某个次要撤离区完成了信标流程：把撤离点整体切换到那一处</summary>
        private void OnSecondaryActivated(MissionEvacuateSecondary zone)
        {
            if (stage != EvacuateState.Activation) return;
            if (!zone.Beacon || !zone.KeyScreen)
            {
                Debug.LogError("次要撤离区没有可用的撤离信标/终端，无法接管撤离流程", zone);
                return;
            }
            //被选中的那一处留下，其余信标全部收起
            HideOtherBeacons(zone.Beacon);
            //撤离流程(范围判定/终端/运输机)转移到实际使用的撤离点
            beacon = zone.Beacon.transform;
            keyScreen = zone.KeyScreen;
            area = zone.EvacuateAnchor;
            areaPoint = zone.EvacuatePoint;
            pos = zone.EvacuatePoint;
            StartWait();
        }

        /// <summary>收起除 keep 之外的所有撤离信标(播放 Hide 动画消失)</summary>
        private void HideOtherBeacons(GameObject keep)
        {
            for (int i = 0; i < _beacons.Count; ++i)
            {
                GameObject item = _beacons[i];
                if (!item || item == keep) continue;
                HideBeacon(item);
            }
        }

        /// <summary>信标消失：与运输船着陆时收起的表现一致，播放信标 Animator 上的 Hide</summary>
        private static void HideBeacon(GameObject beacon)
        {
            if (!beacon) return;
            Animator animator = beacon.GetComponent<Animator>();
            if (animator) animator.Play("Hide");
        }

        protected override void Uninit()
        {
            base.Uninit();
            foreach (var zone in _secondaryZones)
            {
                if (!zone) continue;
                zone.OnBeaconReady -= OnSecondaryBeaconReady;
                zone.OnActivated -= OnSecondaryActivated;
            }
            _beacons.Clear();
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
            //if(IsComplete) AudioSvc.PlayMusic(AudioSvc.MusicGroup.Evacuate, 0.5f);
            CreatNotice("Ayane", "CountDownBegins");
            BattleEventSub.Evacuate(new(pos));
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
                medivac.transform.position = Vector3.Lerp(medivac.transform.position, areaPoint + (Vector3.up * 50) * medivac.transform.lossyScale.x, 30 * Time.deltaTime);
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
            GameRoot.GameState = Core.GameStateEnum.Transition;
            BattleManager.Instance.EndGame(14);
        }


        private bool AreaHavePlayer()
        {
            return ActorsManager.Players.Any(item => Vector3.Distance(item.transform.position, areaPoint) < m_EvacuateRange);
        }


    }

}