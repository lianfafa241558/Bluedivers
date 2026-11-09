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
        /// <summary>当前点亮"等待建造"标记（tip = PipelineA 里名为 Wait 的子物体）的那根管道</summary>
        Furniture_Pipe _tipPipe;

        protected override void StartMission()
        {
            furniture = entity.GetComponentInChildren<Furniture_Attached>();
            furniture.OnOperate += OnOperate;
        }
        protected override void Uninit()
        {
            if (furniture) furniture.OnOperate -= OnOperate;
            UnsubscribePipes();
            _tipPipe = null;
            base.Uninit();
        }


        /// <summary>油泵（PipeHead）被接上时触发：这一刻整条链已经长满，取一次快照后改由管道状态驱动。</summary>
        private void OnOperate()
        {
            furniture.OnOperate -= OnOperate;
            UpdateTip("建造全部管道");
            pipes = furniture.relatedTrans.GetComponent<Furniture_Pipe>().GetAllPipes();
            foreach (var item in pipes)
            {
                if (item) item.OnStateSettled += Refresh;
            }
            Refresh();
        }

        /// <summary>把 Wait 标记对齐到"此刻唯一可建造的那根"，全部建完即完成子任务。
        /// <para>⚠ 驱动源必须是 <see cref="Furniture_Pipe.OnStateSettled"/>（switch 之后触发）。
        /// 原来的 <c>item.OnOperate += OnLink</c> 是在 <c>base.Operate()</c> 里发出的 —— 那时本根 Id 还是
        /// PipeWait、3 秒长按的 Complete() 还没跑 ⇒ 下一根判不成"可建造"，而被点的那根随后的 Complete()
        /// 又不会再通知任何人，于是 Wait 永远停在第一根上。</para>
        /// <para>⚠ <c>pipes</c> 是连接瞬间的快照：建管道阶段不会再产生新管道，所以不用每帧刷新；
        /// 若将来出现"接上之后还能继续延长"，要在这里重新取快照。</para></summary>
        private void Refresh()
        {
            Furniture_Pipe buildable = null;
            int remain = 0;
            for (int i = 0; i < pipes.Count; ++i)
            {
                var item = pipes[i];
                if (!item) continue;
                if (item.Id == "PipeWait") ++remain;
                if (buildable == null && item.CanBuildNow) buildable = item;
            }

            // 同一时刻只有一根可建造：上一根的 Wait 由这里熄灭（它的 Complete() 也会熄一次，幂等）
            if (_tipPipe && _tipPipe != buildable) _tipPipe.UnTip();
            _tipPipe = buildable;
            if (buildable) buildable.Tip();

            if (remain > 0) return;
            UnsubscribePipes();
            CompleteMission();
        }

        private void UnsubscribePipes()
        {
            if (pipes == null) return;
            foreach (var item in pipes)
            {
                if (item) item.OnStateSettled -= Refresh;
            }
        }
    }
}
