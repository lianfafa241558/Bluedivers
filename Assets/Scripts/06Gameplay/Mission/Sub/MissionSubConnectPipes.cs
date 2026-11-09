using System.Collections;
using System.Collections.Generic;
using FPSGame.Mission;
using UnityEngine;
using FPSGame.Gameplay;
namespace FPSGame.Mission
{
    /// <summary>
    /// 连接管道
    /// </summary>
    [AddComponentMenu("任务/子任务/连接管道", 30)]
    public class MissionSubConnectPipes : MissionBase
    {
        public List<Furniture_Pipe> pipes;
        Furniture_Attached furniture;
        int count;
        /// <summary>当前点了 tip 的那根（OnLink 早于 Complete 触发，靠它把"刚建完的"排除掉）</summary>
        Furniture_Pipe _tipPipe;
        protected override void StartMission()
        {
            furniture = entity.GetComponentInChildren<Furniture_Attached>();
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
            // 进入"建造管道"阶段：先把此刻能建的那一根点亮（tip），免得玩家在一排管道里挨个试
            TipBuildable(null);
        }

        /// <summary>把 tip 挪到"当前可建造"的那根管道（同一时刻只有一根满足条件；<paramref name="exclude"/> = 刚建完的那根）。</summary>
        private void TipBuildable(Furniture_Pipe exclude)
        {
            foreach (var item in pipes)
            {
                if (!item || item == exclude || !item.CanBuildNow) continue;
                _tipPipe = item;
                item.Tip();
                return;
            }
            _tipPipe = null;
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
                return;
            }
            // ⚠ OnLink 由 Operate() 在 Complete() 之前触发 ⇒ 刚建完那根的 Id 还是 PipeWait、
            //   判据仍成立，只能按引用排除；否则会重新点亮它，紧接着又被 Complete() 的 tip 关掉。
            var done = _tipPipe;
            TipBuildable(done);
        }
    }
}