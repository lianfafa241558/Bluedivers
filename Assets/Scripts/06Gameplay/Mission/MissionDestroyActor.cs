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
      
        private void FindUnit()
        {
            var list = UnitQuery.FindUnits(new PECircle((PEVector2)((PEVector3)pos), (PEInt)entitySize), new());
            var actors = list.Where(item => actorNames.Contains(item.Id)).ToArray();
            showarr = list.Select(item => item.gameObject).ToList();
            NowProgress = 0;
            MaxProgress = actors.Length;
            if (MaxProgress == 0)
            {
                Debug.LogWarning($"[任务] {name} 摧毁区域内没有匹配到单位：半径={entitySize} 名单=[{string.Join(",", actorNames)}] " +
                    $"圈内({list.Count})=[{string.Join(",", list.Select(item => item.Id).Distinct())}]", this);
            }
            foreach (var item in actors)
            {
                item.OnDeath += OnActorDeath;
            }
        }
        [SerializeField]
        private List<GameObject> showarr;
        void OnActorDeath()
        {
            //这里就不做取消绑定了，因为不知道是谁死了，只能计
            AddProgress(syncPercentage: true, updateBeforeComplete: true);
        }
       
    }
}