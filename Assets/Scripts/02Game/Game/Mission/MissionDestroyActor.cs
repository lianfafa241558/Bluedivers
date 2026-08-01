using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Attribute;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;

namespace FpsGame.Mission
{
    /// <summary>
    /// 摧毁区域内的单位的通用类型
    /// </summary>
    [AddComponentMenu("任务/摧毁区域内的单位", 30)]
    public class MissionDestroyActor : MissionBase
    {

        /// <summary>
        /// 只有指定名称的物体被摧毁才算
        /// </summary>
        [SerializeField]
        string[] actorNames;

        protected override void StartMission()
        {
            //Debug.LogError("实体" + entity);
            //Debug.LogError("变换" + entity.transform);

            var list = entity.transform.GetComponentsInChildren<I_AIController>();
            var actors = list.Where(item => actorNames.Contains(item.ID)).ToArray();
            NowProgress = 0;
            MaxProgress = actors.Length;
            foreach (var item in actors)
            {
                item.OnDie += OnActorDeath;
            }
            if (missionTag.HasFlag(MissionTag.DisplayProgress)) percentage = 0.01f;
        }


        void OnActorDeath()
        {
            //这里就不做取消绑定了，因为不知道是谁死了，只能计
            if (++NowProgress < MaxProgress)
            {
                percentage = NowProgress / (MaxProgress + 0f);
                UpdateMission();
            }
            else
            {
                UpdateMission();
                CompleteMission();
            }
        }
       
    }
}