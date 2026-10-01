using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Attributes;
using FPSGame.Gameplay;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 和指定物体交互的通用类型
    /// </summary>
    [AddComponentMenu("任务/和指定物体交互", 30)]
    public class MissionOperationFurn : MissionBase
    {
        [SerializeField]
        private int meetCount=1;

        /// <summary>
        /// 只有指定名称的物体被交互才算
        /// </summary>
        [SerializeField]
        string[] actorNames;

        protected override void StartMission()
        {
            //Debug.LogError("实体" + entity);
            //Debug.LogError("变换" + entity.transform);

            var list = entity.transform.GetComponentsInChildren<IFurniture>();
            var actors = list.Where(item => actorNames.Contains(item.Id)).ToArray();
            NowProgress = 0;
            MaxProgress = meetCount;
            foreach (var item in actors)
            {
                item.OnOperate += OnOperate;
            }
            if (missionTag.HasFlag(MissionTag.DisplayProgress)) percentage = 0.01f;
        }


        void OnOperate()
        {
            //这里就不做取消绑定了，因为不知道是谁交互了，只能计
            AddProgress(syncPercentage: true, updateBeforeComplete: true);
        }
       
    }
}