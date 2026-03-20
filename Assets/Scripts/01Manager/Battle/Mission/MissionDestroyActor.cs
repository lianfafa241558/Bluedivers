using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;

namespace FpsGame.Mission
{
    /// <summary>
    /// 摧毁区域内的单位的通用类型
    /// </summary>
    public class MissionDestroyActor : MissionBase
    {
        [Foldout("标旗")]
        [CustomLabel("显示进度条")]
        [SerializeField]
        bool useBar;

        /// <summary>
        /// 只有指定名称的物体被摧毁才算
        /// </summary>
        [SerializeField]
        string[] actorNames;

        protected override void CreatMission()
        {
            base.CreatMission();
            //Debug.LogError("实体" + entity);
            //Debug.LogError("变换" + entity.transform);

            var list = entity.transform.GetComponentsInChildren<I_AIController>();
            var actors = list.Where(item => actorNames.Contains(item.ID)).ToArray();
            NowProgress = 0;
            if (actors.Length > 1) MaxProgress = actors.Length;
            foreach (var item in actors)
            {
                item.OnDie += OnActorDeath;
            }
            if (useBar) percentage = 0.01f;
        }


        void OnActorDeath()
        {
            //这里就不做取消绑定了，因为不知道是谁死了，只能计数
            if (++NowProgress < MaxProgress)
            {
                percentage = NowProgress / (MaxProgress + 0f);
                UpdateMission(false);
            }
            else
            {
                UpdateMission(false);
                CompleteMission();
            }
        }
        public override bool Tick()
        {
            base.Tick();
            if (Vector3.Distance(ActorsManager.Player.Pos, pos) < entitySize)
            {

            }

            return true;
        }
    }
}