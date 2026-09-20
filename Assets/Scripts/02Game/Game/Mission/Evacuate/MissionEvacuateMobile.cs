using System.Collections;
using System.Collections.Generic;
using FPSGame.Furn;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FpsGame.Mission
{

    //第一步 SpecUnitKei调用一个方法显示ReturnBag（要写一个简单的furniture或者写进Furniture_AttachedGeneral或者Furniture_General），然后这边注册监听 播放语音
    //第二步 等待玩家和ReturnBag交互
    //第三步 创建运输机并落地等待登机，要在300秒内完成撤离√播放语音
    //此时会在玩家和撤离点的中点刷一波小规模波次
    // SpecUnitKei会在触发BattleEventSub.Evacuate后尝试前往撤离点，并且每5米往地上放一根returnPoint
    //第四步 玩家全部登机或者时间到了就起飞
    /// <summary>
    /// 撤离任务(移动)
    /// </summary>
    [AddComponentMenu("任务/撤离/动态撤离", 30)]
    public class MissionEvacuateMobile : MissionEvacuateBase
    {

        enum EvacuateState
        {
            /// <summary>等待凯伊取出回收信标</summary>
            Activation,
            /// <summary>已请求撤离，运输船接近中</summary>
            Approach,
            /// <summary>运输船已就位，等待登机</summary>
            Land,
            /// <summary>结束(撤离成功或超时)</summary>
            End
        }

        [SerializeField]
        [InspectorName("撤离时限(秒)")]
        [Tooltip("从玩家请求撤离开始计时，超时运输船起飞、任务失败")]
        private int m_EvacuateTime = 300;

        [SerializeField]
        [InspectorName("运输船接近时长(秒)")]
        [Tooltip("运输船从高空下降至撤离点上空所用的表现时长")]
        private float m_ApproachTime = 3f;

        [SerializeField]
        [InspectorName("着陆时长(秒)")]
        private float m_LandTime = 3f;

        [SerializeField]
        [InspectorName("悬停高度(米)")]
        private float m_HoverHeight = 50f;

        [SerializeField]
        [InspectorName("每名玩家波次倍率")]
        [Tooltip("请求撤离后，为每名玩家在其位置刷出的追击增援波次倍率；波次结束后会自动续刷")]
        private float m_WaveScale = 0.35f;

        /// <summary>是否持续为玩家续刷追击增援(波次结束后自动续刷，任务结束/超时后停止)</summary>
        private bool m_ChaseReinforce;

        /// <summary>因阵亡(倒地)而暂停续刷、等待复活的追击玩家；复活后由 Tick 恢复</summary>
        private readonly List<I_Actor> m_PausedChase = new();

        private EvacuateState stage = EvacuateState.Activation;

        /// <summary>动态撤离的撤离点由任务实体给出，快速模式也不必换成场景起始点</summary>
        protected override bool UseSceneStartPoint => false;


        public override void Activation(MissionBase mission)
        {
            base.Activation(mission);
            stage = EvacuateState.Activation;

            //快速模式：跳过"等凯伊取出信标+等玩家交互"，激活即发起撤离
            if (IsFast)
            {
                OnStartEvacuate(null);
                return;
            }

            //回收信标是凯伊身上的家具，直接订阅它的取出/交互，只有本任务激活时才允许交互
            if (kei)
            {
                kei.OnReturnBagShow += OnReturnBagShow;

                var bag = kei.ReturnBag;
                if (bag)
                {
                    bag.Usable = true;
                    bag.OnRequestEvacuate += OnReturnBagOperate;
                }
            }

            UpdateText("呼叫撤离", "呼叫凯伊取出回收信标");
        }


        public override bool Tick()
        {
            base.Tick();

            switch (stage)
            {
                case EvacuateState.Activation:

                    break;
                case EvacuateState.Approach:
                    UpdateTip("运输船接近中，前往撤离点 [" + Tool.FloatToTime(countDown) + "]");
                    if (--countDown <= 0) TimeOut();
                    break;
                case EvacuateState.Land:
                    UpdateTip("进入雨云号完成撤离 [" + Tool.FloatToTime(countDown) + "]");
                    if (--countDown <= 0) TimeOut();
                    break;
                case EvacuateState.End:
                    //与静态撤离一致：收尾 6 秒后播报最终台词
                    if (--countDown == 0) CreatNotice("Yuuka", IsComplete ? "End" : "Fail");
                    break;
            }

            ResumePausedChase();

            return true;
        }

        protected override void Uninit()
        {
            //任务卸载时停掉追击增援的续刷链
            StopChaseReinforcement();
            base.Uninit();
            if (kei)
            {
                kei.OnReturnBagShow -= OnReturnBagShow;

                var bag = kei.ReturnBag;
                if (bag)
                {
                    bag.Usable = false;
                    bag.OnRequestEvacuate -= OnReturnBagOperate;
                }
            }
        }


        /// <summary>凯伊取出回收信标：播报语音并提示玩家交互</summary>
        private void OnReturnBagShow()
        {
            if (stage != EvacuateState.Activation) return;
            CreatNotice("Yuuka", IsComplete ? "Evacuate" : "EvacuateFail");
            UpdateTip("在回收信标上交互，请求撤离");
        }

        /// <summary>玩家在回收信标上请求撤离</summary>
        private void OnReturnBagOperate(GameObject user)
        {
            if (stage != EvacuateState.Activation) return;
            OnStartEvacuate(user);
        }

        /// <summary>发起撤离：user 为空表示快速模式，没有玩家点击信标</summary>
        private void OnStartEvacuate(GameObject user)
        {
            stage = EvacuateState.Approach;
            countDown = m_EvacuateTime;

            UpdateText("运输船接近中", "前往撤离点");
            CreatNotice("Ayane", "CountDownBegins");
            if (user) GlobalEventSub.PlayMeetSpeech(user, SpeechTypeEnum.Evacuate);
            if (IsComplete) AudioSvc.PlayMusic(AudioSvc.MusicGroup.Evacuate, 0.5f);

            //通知凯伊带队前往撤离点(沿途留下回收标记)，巡逻队也会随之向撤离点收缩
            BattleEventSub.Evacuate(new(areaPoint));

            CreatReinforcement();
            CreatMedivac();
        }

        /// <summary>为每名玩家刷出一波以其位置为目标的追击增援，波次结束后自动续刷(不会结束)</summary>
        private void CreatReinforcement()
        {
            m_ChaseReinforce = true;
            m_PausedChase.Clear();
            foreach (var player in ActorsManager.Players)
            {
                CreatChaseWave(player);
            }
        }

        /// <summary>停掉追击增援的续刷链(任务结束/超时/卸载时调用)</summary>
        private void StopChaseReinforcement()
        {
            m_ChaseReinforce = false;
            m_PausedChase.Clear();
        }

        /// <summary>
        /// 为指定玩家刷出一波以其当前位置为目标的追击增援：
        /// 波次 center 每 Tick 跟踪该玩家(玩家跑动时增援持续追来)，该波清空后自动续刷；
        /// 若玩家已阵亡(倒地)则暂停本玩家的续刷，等其复活后由 ResumePausedChase 恢复。
        /// </summary>
        private void CreatChaseWave(I_Actor player)
        {
            if (!m_ChaseReinforce) return;

            //玩家对象已销毁：彻底退出，不再续刷
            if (!player.IsValidMono())
            {
                m_PausedChase.Remove(player);
                return;
            }

            //玩家阵亡(倒地)：暂停本玩家续刷，等复活后恢复
            if (player.ActorState == Core.ActorState.Dead)
            {
                if (!m_PausedChase.Contains(player)) m_PausedChase.Add(player);
                return;
            }

            m_PausedChase.Remove(player);

            var param = WaveCreateParams.Evacuate.Set(player.Pos).Scale(m_WaveScale);
            //波次中心持续跟踪该玩家位置(玩家移动时新刷出的单位会走向最新位置)
            Vector3 last = player.Pos;
            param.centerGetter = () =>
            {
                if (player.IsValidMono()) last = player.Pos;
                return last;
            };
            param.onEnd = () => CreatChaseWave(player);
            BattleManager.Instance.CreatWave(param);
        }

        /// <summary>每秒检查：阵亡的追击玩家复活后，恢复其续刷链</summary>
        private void ResumePausedChase()
        {
            if (!m_ChaseReinforce || m_PausedChase.Count == 0) return;

            for (int i = m_PausedChase.Count - 1; i >= 0; --i)
            {
                var player = m_PausedChase[i];
                //对象已销毁：彻底移除
                if (!player.IsValidMono())
                {
                    m_PausedChase.RemoveAt(i);
                    continue;
                }
                //还没复活，继续等
                if (player.ActorState == Core.ActorState.Dead) continue;

                m_PausedChase.RemoveAt(i);
                CreatChaseWave(player);
            }
        }

        /// <summary>创建撤离用运输机：从高空下降至撤离点上空，随后着陆</summary>
        private void CreatMedivac()
        {
            medivac = ResSvc.Instance.CreatPrefab("Prefabs/BattleBase/NeoNimbus", true,
                areaPoint + Vector3.up * 500).GetComponent<MedivacController>();
            medivac.transform.LookAt(areaPoint);
            medivac.Complete += End;
            medivac.SetType(MedivacController.MedivacState.Evacuate);
            medivac.Play("Idle");

            float scale = medivac.transform.lossyScale.x;
            Vector3 hover = areaPoint + Vector3.up * (m_HoverHeight * scale);
            Vector3 euler = area.eulerAngles;
            GameRoot.CreatePerTimer(() => {
                if (!medivac || stage != EvacuateState.Approach) return;
                medivac.transform.position = Vector3.Lerp(medivac.transform.position, hover, 30 * Time.deltaTime);
                medivac.transform.eulerAngles = Vector3.Lerp(medivac.transform.eulerAngles, euler, 30 * Time.deltaTime);
            }, m_ApproachTime, Landing);
        }

        /// <summary>着陆：运输船落到撤离点，开始等待登机</summary>
        private void Landing()
        {
            if (stage != EvacuateState.Approach || !medivac) return;
            stage = EvacuateState.Land;
            CreatNotice("Ayane", "Landing");
            UpdateTip("进入雨云号完成撤离 [" + Tool.FloatToTime(countDown) + "]");

            float scale = medivac.transform.lossyScale.x;
            Vector3 landPos = areaPoint + (Vector3.up * 5.5f - area.forward * 5.5f) * scale;
            medivac.transform.position = areaPoint + (Vector3.up * 5.5f + area.forward * 10f) * scale;
            medivac.Play("Land");

            GameRoot.CreatePerTimer(() => {
                if (!medivac || stage != EvacuateState.Land) return;
                medivac.transform.position = Vector3.Lerp(medivac.transform.position, landPos, 15 * Time.deltaTime);
            }, m_LandTime, null);
        }

        /// <summary>超时：强制起飞(只带走已登船玩家)，收尾统一由运输机的 Complete 回调处理</summary>
        private void TimeOut()
        {
            if (stage == EvacuateState.End) return;
            //没有运输机(异常)时直接走统一收尾
            if (!medivac)
            {
                End();
                return;
            }
            medivac.ForceTakeOff();
        }

        /// <summary>
        /// 统一收尾(与静态撤离 MissionEvacuateStatic.End 一致)：运输船起飞 → 进入过场 → 14 秒后结算；
        /// 不论玩家是否全部登机(超时会强制起飞)都会走到这里，最终台词由 Tick 的 End 阶段播报。
        /// </summary>
        private void End()
        {
            if (stage == EvacuateState.End) return;
            stage = EvacuateState.End;
            StopChaseReinforcement();
            countDown = 6;
            CreatNotice("Ayane", "TakeOff");
            GameRoot.GameState = Core.GameStateEnum.Transition;
            BattleManager.Instance.EndGame(14);
        }


    }
}
