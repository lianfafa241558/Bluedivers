using System.Collections;
using System.Collections.Generic;
using FpsGame.Mission;
using UnityEngine;
namespace FpsGame.Mission
{
    /// <summary>
    /// 连接管道
    /// </summary>
    [AddComponentMenu("任务/子任务/连接管道", 30)]
    public class MissionSubConnectPipes : MissionBase
    {
        public List<Furniture_Pipe> pipes;
        Furniture_Base furniture;
        int count;
        protected override void StartMission()
        {
            furniture = entity.GetComponentInChildren<Furniture_Base>();
            furniture.OnOperate += OnOperate;
        }
        protected override void Uninit()
        {
            furniture.OnOperate -= OnOperate;
            base.Uninit();
        }


        private void OnOperate()
        {
            furniture.OnOperate -= OnOperate;
            UpdateTip("建造全部管道");
            pipes = furniture.relatedTrans.GetComponent<Furniture_Pipe>().GetAllPipes();
            count = 0;
            foreach (var item in pipes)
            {
                if (item.Id == "PipeWait")
                {
                    item.OnOperate += OnLink;
                    ++count;
                }
                
            }

        }
        private void OnLink()
        {
            if ((--count) == 0)
            {
                foreach (var item in pipes)
                {
                    if(item) item.OnOperate -= OnLink;
                }
                CompleteMission();
            }  
        }
    }
}