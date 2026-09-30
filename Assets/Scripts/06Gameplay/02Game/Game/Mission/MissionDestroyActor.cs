using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Attributes;
using FPSGame.GameContract;
using PEMaths;
using FPSGame.Game;
using UnityEngine;

namespace FPSGame.Mission
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
            //StartCoroutine(WaitCreatUnit());
            FindUnit();
            if (missionTag.HasFlag(MissionTag.DisplayProgress)) percentage = 0.01f;
        }
        
        IEnumerator WaitCreatUnit()
        {
            yield return new WaitForSeconds(1.0f);
            var list = FPSGame.GameContract.ServiceLocator.Battle.FindUnits(new PECircle((PEVector2)((PEVector3)pos), (PEInt)entitySize), new());
            var actors = list.Where(item => actorNames.Contains(item.Id)).ToArray();
            NowProgress = 0;
            MaxProgress = actors.Length;
            foreach (var item in actors)
            {
                item.OnDeath += OnActorDeath;
            }
        }
        private void FindUnit()
        {
            var list = FPSGame.GameContract.ServiceLocator.Battle.FindUnits(new PECircle((PEVector2)((PEVector3)pos), (PEInt)entitySize), new());
            var actors = list.Where(item => actorNames.Contains(item.Id)).ToArray();
            NowProgress = 0;
            MaxProgress = actors.Length;
            foreach (var item in actors)
            {
                item.OnDeath += OnActorDeath;
            }
        }


        void OnActorDeath()
        {
            //这里就不做取消绑定了，因为不知道是谁死了，只能计
            AddProgress(syncPercentage: true, updateBeforeComplete: true);
        }
       
    }
}