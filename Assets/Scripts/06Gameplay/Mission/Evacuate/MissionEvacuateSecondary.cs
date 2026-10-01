using System;
using FPSGame.Attributes;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Gameplay;

namespace FPSGame.Mission
{
    /// <summary>
    /// 次要撤离区
    /// <para>本体与 <see cref="MissionDestroyActor"/> 一致：摧毁本区域内的指定单位即完成（额外任务）。</para>
    /// <para>额外作用：充当地图上的"可选撤离点"。静态撤离任务激活时，除了自己的撤离信标，还会往每个次要撤离区的中心
    /// 各呼叫一个撤离信标(0号战备)；玩家可以自行选择在主要或任意一个次要撤离点完成信标流程来发起撤离。</para>
    /// <para>某一处被激活后，其余信标统一播放 Hide 消失，撤离流程(范围判定/终端/运输机)随之转移到被选中的那一处
    /// —— 由 <see cref="MissionEvacuateStatic"/> 接管，本类只负责"呼叫信标"与"上报被选中"。</para>
    /// <para>注意：必须挂在标旗为"额外任务(MissionType.Extra)"的控制器 prefab 上，并由 <see cref="MissionController"/>
    /// 在任务创建完成后登记给静态撤离任务；否则撤离信标不会被呼叫。</para>
    /// </summary>
    [AddComponentMenu("任务/撤离/次要撤离区", 31)]
    public class MissionEvacuateSecondary : MissionDestroyActor
    {
        /// <summary>本区的撤离信标(0号战备)创建完成，参数为本区自身</summary>
        public event Action<MissionEvacuateSecondary> OnBeaconReady;

        /// <summary>本区的信标流程走到最后一步(=玩家选择了在本区撤离)，参数为本区自身</summary>
        public event Action<MissionEvacuateSecondary> OnActivated;

        [SerializeField]
        [InspectorName("信标等待时长(秒)")]
        [Tooltip("信标最后一步(等待)的时长，应与静态撤离任务的撤离时间一致；实际由静态撤离任务呼叫信标时注入，此处仅作缺省值")]
        private int _beaconWaitTime = 120;

        /// <summary>本区呼叫出来的撤离信标(0号战备)，未呼叫时为 null</summary>
        private GameObject _beacon;

        /// <summary>本区信标上的撤离终端</summary>
        private KeyScreen _keyScreen;

        /// <summary>本区作为撤离点时的中心点</summary>
        public Vector3 EvacuatePoint => pos;

        /// <summary>本区作为撤离点时的朝向锚点(任务实体，实体缺失时退化为任务物体自身)</summary>
        public Transform EvacuateAnchor => entity ? entity.transform : transform;

        /// <summary>本区呼叫出来的撤离信标(0号战备)，未呼叫时为 null</summary>
        public GameObject Beacon => _beacon;

        /// <summary>本区信标上的撤离终端，未呼叫时为 null</summary>
        public KeyScreen KeyScreen => _keyScreen;

        public override bool Tick()
        {
            base.Tick();
            if (!completed && MaxProgress > 0)
            {
                UpdateTip($"摧毁区域内的目标单位 [{NowProgress}/{MaxProgress}]");
            }
            return true;
        }

        /// <summary>
        /// 在本区中心呼叫一个撤离信标(0号战备)，由静态撤离任务在激活时统一调用；重复呼叫会被忽略。
        /// </summary>
        /// <param name="waitTime">信标最后一步(等待)的时长，取静态撤离任务的撤离时间</param>
        public void CallBeacon(int waitTime)
        {
            if (_beacon||!completed) return;
            _beaconWaitTime = waitTime;
            FPSGame.GameContract.BattleHub.Current.ReleaseAirdrop(pos, 0, InitBeacon);
        }

        private void InitBeacon(GameObject beacon)
        {
            _beacon = beacon;
            _keyScreen = beacon.GetComponentInChildren<KeyScreen>();
            if (_keyScreen)
            {
                //等待阶段时长与静态撤离任务的撤离倒计时保持一致
                foreach (var item in _keyScreen.procedure)
                {
                    if (item.type == KeyScreen.ProcedureType.Wait) item.time = _beaconWaitTime;
                }
                _keyScreen.OnUpdateStage += OnKeyScreenStage;
            }
            else
            {
                Debug.LogError("次要撤离区的信标上没有撤离终端(KeyScreen)，无法作为撤离点使用", this);
            }
            OnBeaconReady?.Invoke(this);
        }

        private void OnKeyScreenStage(int nowStage)
        {
            if (!_keyScreen) return;
            if (nowStage == _keyScreen.procedure.Count - 1) OnActivated?.Invoke(this);
        }
    }
}
