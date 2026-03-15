using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FpsGame.Mission
{
    /// <summary>
    /// 撤离任务(移动)
    /// </summary>
    public class TaskTargetEvacuateMobile : MissionBase
    {

        enum EvacuateState
        {
            Activation,
            Mobile,

        }
        private EvacuateState stage = EvacuateState.Activation;
        private int downcount;

        protected override void Start()
        {
            base.Start();
            //GlobalEventManager.OnStartEvacuate += OnStartEvacuate;

        }

        public override bool Tick()
        {
            switch (stage)
            {
                case EvacuateState.Activation:

                    break;
                case EvacuateState.Mobile:
                    UpdateTip("前往运输船降落位置  [" + downcount + "]");
                    if (downcount <= 0)
                    {
                        FailMission();
                    }
                    break;
            }

            return true;
        }

        private void OnStartEvacuate(GameObject kei)
        {
            stage = EvacuateState.Mobile;
            title = "运输船接近中";
            downcount = 300;
            UpdateMission();
        }


    }
}