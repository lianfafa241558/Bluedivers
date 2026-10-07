using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.AI;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;
using FPSGame.Utils;
using FPSGame.Audio;
using FPSGame.Gameplay;
using Random = System.Random;
using FPSGame.GameContract;

namespace FPSGame.Managers
{
    /// <summary>
    /// 虫族空投波次：开场在预设生成点(或围绕中心的随机环)空投兵营，随后按批次投放单位。
    /// 生命周期与单位账本由 <see cref="WaveBase"/> 提供，本类只负责生成环维护与批次投放。
    /// </summary>
    public class ZergWave : WaveBase
    {
        /// <summary>centerGetter 模式下，实际中心偏离基准中心超过该距离就重新部署空投点</summary>
        const float RedeployDistance = 30f;

        /// <summary>空投特效存活时长(秒)</summary>
        const int PodLifeTime = 51;

        List<GameObject> creatObject;
        int perTickCreat;
        int waitTime;

        /// <summary>生成环所依据的基准中心(centerGetter 模式下随重新部署更新，用于判断"离太远")</summary>
        Vector3 anchorCenter;

        /// <summary>生成点是否由 center+range 随机得出(即 param.points==null)；只有这种模式能跟随 center 重新部署</summary>
        bool useCenterPoints;

        public ZergWave(WaveCreateParams param, Stack<GameObject> creats, List<GameObject> waveUseObject, int waitTime, string title)
            : base(param, creats, waveUseObject)
        {
            this.waitTime = waitTime;
            creatObject = new();
            anchorCenter = center;
            startTipName = "WaveStart_Zerg";
            infoTitle = title;
            if (param.points == null)
            {
                useCenterPoints = true;
                perTickCreat = Mathf.Max(1, Mathf.CeilToInt(creats.Count / 45f));//保底1个
                points = new Vector3[perTickCreat];
                ResetPoints();
            }
            else
            {
                perTickCreat = param.points.Length;
                points = new Vector3[perTickCreat];
                for (int i = 0; i < perTickCreat; ++i)
                {
                    points[i] = SnapToNavMesh(param.points[i]
                        + random.RandomVector2().ToVector3() * random.Range(0, param.range));
                }
            }

            Trans(WaveState.Start);
        }

        protected override void OnDispose()
        {
            creatObject.Clear();
        }

        protected override void OnCenterUpdated(Vector3 newCenter)
        {
            center = newCenter;

            //实际中心偏离基准中心过远：回收当前空投点、以当前中心为新基准重新部署(随机生成点模式，且波次尚未收尾)
            if (useCenterPoints && state == WaveState.Ongoing && !completeCreat
                && Vector3.Distance(center, anchorCenter) > RedeployDistance)
            {
                RedeployPods();
            }
        }

        protected override void TickStart()
        {
            if (time == -5) SpawnPods();
            if (time <= -waitTime) Trans(WaveState.Ongoing);
        }

        protected override void TickOngoing()
        {
            if (creats.Count >= perTickCreat)
            {
                for (int i = 0; i < perTickCreat; ++i)
                {
                    if (!creats.TryPop(out var tmp)) break;

                    var dir = Quaternion.LookRotation(points[i] - center);
                    dir.eulerAngles = new Vector3(dir.eulerAngles.x, 0, dir.eulerAngles.z);

                    var go = Object.Instantiate(tmp, points[i] + RandomOffset(10f), dir, null);
                    RegisterUnit(go);
                    SetUnitHome(go);
                }
            }
            else if (!completeCreat)
            {
                completeCreat = true;
                EndCreatObjects();
            }
            else
            {
                TryEnterNearEnd();
            }
        }

        /// <summary>在生成环上投放一批兵营特效(开场空投与重新部署共用)</summary>
        void SpawnPods()
        {
            for (int i = 0; i < perTickCreat; ++i)
            {
                var go = VFXManager.Creat(waveUseObject[0], points[i], RandomYaw());
                if (!go) continue;

                go.GetComponent<LimitedLife>().ResetLift(PodLifeTime);
                creatObject.Add(go);
            }
        }

        /// <summary>按当前 center/range 重新计算随机生成环(仅随机生成点模式使用)</summary>
        void ResetPoints()
        {
            float theta = random.Range(0, 2 * Mathf.PI);
            for (int i = 0; i < perTickCreat; ++i)
            {
                var dx = random.Range(-1, 1f);//范围20度
                points[i] = center + new Vector3(Mathf.Cos(theta + dx), 0, Mathf.Sin(theta + dx)) * random.Range(range, range + 10);
                points[i] = SnapToNavMesh(points[i]);
            }
        }

        /// <summary>
        /// 实际中心偏离基准中心过远时：回收当前空投特效(走 LimitedLife 回收)，
        /// 以当前中心为新基准重算生成环并重新空投一波，让空投点跟随目标移动。
        /// </summary>
        void RedeployPods()
        {
            //1. 回收当前空投特效
            EndCreatObjects();

            //2. 以当前中心为新基准重算生成环
            anchorCenter = center;
            ResetPoints();

            //3. 重新空投一波
            SpawnPods();
        }

        /// <summary>回收当前所有空投特效(播放结束动画并让 LimitedLife 自行回收)</summary>
        void EndCreatObjects()
        {
            for (int i = 0; i < creatObject.Count; ++i)
            {
                if (!creatObject[i]) continue;

                var animator = creatObject[i].GetComponentInChildren<Animator>();
                if (animator) animator.Play("End");

                var life = creatObject[i].GetComponent<LimitedLife>();
                if (life) life.ResetLift(6);
            }
            creatObject.Clear();
        }
    }
}
