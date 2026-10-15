using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.AI;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 拦截火车 · 火车卫队（partial）：随列车发车生成（房主 <c>CreatPatrol</c> + 4052 复刻）、
    /// 按 Tick 节拍把巡逻点挪到火车当前位置、任务收尾时静默移除。
    /// <para>⚠ 引导必须"先发车、后引导"且落在<b>同一帧</b>内（理由见 <c>Tick</c> 里的注释）。</para>
    /// </summary>
    public partial class MissionInterceptTrain
    {
        [Foldout("火车卫队", true)]
        [InspectorName("卫队落点后移(米)")]
        [SerializeField] private float guardSpawnBack = 25f;

        #region 卫队

        /// <summary>
        /// 生成火车卫队（房主权威：<c>CreatPatrol</c> 内部就是房主生成 + 4052 复刻）。
        /// <para>⚠ 卫队里带 <see cref="ActorFlag.Boss"/> 的单位要**立刻摘掉**这个标：否则会占一个 boss 血条。</para>
        /// </summary>
        private void SpawnGuards(TrainRuntime runtime, int squads)
        {
            if (squads <= 0 || manager == null || !manager.IsPresent) return;
            Vector2 now = runtime.Unit != null ? runtime.Unit.Position : default;
            Vector3 behind = new(now.x, 0f, now.y);
            if (runtime.Engine != null) behind -= runtime.Engine.transform.forward * guardSpawnBack;
            Vector3 spawnPos = FpsHelper.GetNavMeshPoint(behind);

            for (int i = 0; i < squads; i++)
            {
                List<GameObject> units = manager.CreatPatrol(spawnPos);
                if (units == null) continue;
                for (int k = 0; k < units.Count; k++)
                {
                    GameObject unit = units[k];
                    if (unit == null) continue;
                    if (unit.TryGetComponent(out Actor actor) && actor.HasFlag(ActorFlag.Boss)) actor.RemoveFlag(ActorFlag.Boss);
                    runtime.Guards.Add(unit);
                }
            }
        }

        /// <summary>给所有活车刷一遍卫队目标点（由 <see cref="Tick"/> 按 <c>TickTime</c> = 3 秒的节拍调用）</summary>
        private void RefreshAllGuards()
        {
            for (int i = 0; i < liveTrains.Count; i++) RefreshGuards(liveTrains[i]);
        }

        /// <summary>把卫队的巡逻目标点一直挪到火车当前位置（<c>PatrolPos</c> 是静态点，只能外部改写才能"跟随"）</summary>
        private void RefreshGuards(TrainRuntime runtime)
        {
            if (runtime.Ended || runtime.Guards.Count == 0 || runtime.Unit == null) return;
            Vector2 p = runtime.Unit.Position;
            // ⚠ 必须先投影到 NavMesh：目标不可达时 SetDestination 会失败，而 EnemyController 的去重会把失败记成"已请求"
            Vector3 target = FpsHelper.GetNavMeshPoint(new Vector3(p.x, 0f, p.y));
            for (int i = runtime.Guards.Count - 1; i >= 0; i--)
            {
                GameObject unit = runtime.Guards[i];
                if (unit == null)
                {
                    runtime.Guards.RemoveAt(i);
                    continue;
                }
                if (unit.TryGetComponent(out EnemyController controller)) controller.PatrolPos = target;
            }
        }

        /// <summary>静默移除卫队（<c>IsRemove</c> 语义：无死亡动画、不计击杀归属）</summary>
        private void RemoveGuards(TrainRuntime runtime)
        {
            for (int i = 0; i < runtime.Guards.Count; i++)
            {
                GameObject unit = runtime.Guards[i];
                if (unit == null) continue;
                if (unit.TryGetComponent(out I_AIController ai)) ai.Kill(true);
                else Destroy(unit);
            }
            runtime.Guards.Clear();
        }

        #endregion
    }
}