using System.Collections.Generic;
using Core;
using Core.Interface;
using FPSGame.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using Utils;
using Random = System.Random;

namespace FPSGame.Game
{
    public class ZergWave : I_TickClass, System.IDisposable
    {
        /// <summary>centerGetter 模式下，实际中心偏离基准中心超过该距离就重新部署空投点</summary>
        const float RedeployDistance = 30f;

        List<GameObject> waveUseObject;
        List<GameObject> creatObject;
        Stack<GameObject> creats;
        List<Actor> units;

        WaveState state;
        int perTickCreat;
        int time;
        Vector3[] points;
        Vector3 center;
        /// <summary>生成环所依据的基准中心(centerGetter 模式下随重新部署更新，用于判断"离太远")</summary>
        Vector3 anchorCenter;
        /// <summary>生成环半径参数(来自 param.range，重新部署时复用)</summary>
        float range;
        /// <summary>生成点是否由 center+range 随机得出(即 param.points==null)；只有这种模式能跟随 center 重新部署</summary>
        bool useCenterPoints;
        bool completeCreat;
        bool tip;
        int waitTime;
        bool IsDisposed;
        System.Action onEnd;
        System.Func<Vector3> centerGetter;

        System.Random random;
        public ZergWave(WaveCreateParams param, Stack<GameObject> creats, List<GameObject> waveUseObject,int waitTime)
        {
            random = new Random(RandomUtils.Range(0, 1000));
            this.waveUseObject = new(waveUseObject);
            this.creats = creats;
            this.tip = param.tip;
            this.waitTime = waitTime;
            onEnd = param.onEnd;
            centerGetter = param.centerGetter;
            creatObject = new();
            units = new();
            center = param.center;
            anchorCenter = center;
            range = param.range;

            BattleEventSub.OnEnemyDead += OnUnitDeath;

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
                    points[i] = param.points[i] + random.RandomVector2().ToVector3() * random.Range(0, param.range);

                    if (NavMesh.SamplePosition(points[i], out var hit, 100, NavMesh.AllAreas))
                    {
                        points[i] = hit.position;
                    }
                    else
                    {
                        points[i] = new Vector3(points[i].x, center.y, points[i].z);
                    }
                }
            }
            Trans(WaveState.Start);
        }
        public void Dispose()
        {
            if (IsDisposed) return;
            BattleEventSub.OnEnemyDead -= OnUnitDeath;

            units?.Clear();
            waveUseObject?.Clear();
            creatObject?.Clear();
            creats?.Clear();

            waveUseObject = null;
            creatObject = null;
            creats = null;
            units = null;
            points = null;
            random = null;
            centerGetter = null;

            IsDisposed = true;

            //波次结束(所有单位清空)回调，用于续航/续刷
            var callback = onEnd;
            onEnd = null;
            callback?.Invoke();
        }

        public bool Tick()
        {

            --time;
            //中心点持续跟踪(追击)：有 centerGetter 时每 Tick 刷新，新刷出的单位会走向最新位置
            if (centerGetter != null)
            {
                center = centerGetter();
                //实际中心偏离基准中心过远：回收当前空投点、以当前中心为新基准重新部署(随机生成点模式，且波次尚未收尾)
                if (useCenterPoints && state == WaveState.Ongoing && !completeCreat
                    && Vector3.Distance(center, anchorCenter) > RedeployDistance)
                {
                    RedeployPods();
                }
            }
            switch (state)
            {
                case WaveState.Start:
                    if (time == -5)
                    {
                        for (int i = 0; i < perTickCreat; ++i)
                        {
                            var go = VFXManager.Creat(waveUseObject[0], points[i], Quaternion.Euler(0f, random.Range(0, 360), 0f));
                            go.GetComponent<LimitedLife>().ResetLift(51);
                            creatObject.Add(go);
                        }
                    }
                    if (time <= -waitTime)
                    {
                        Trans(WaveState.Ongoing);
                    }
                    break;
                case WaveState.Ongoing:
                    if (creats.Count > 3)
                    {
                        for (int i = 0; i < perTickCreat; ++i)
                        {
                            if (creats.TryPop(out var tmp))
                            {
                                var dir = Quaternion.LookRotation(points[i] - center);
                                dir.eulerAngles = new Vector3(dir.eulerAngles.x, 0, dir.eulerAngles.z);

                                var go = Object.Instantiate(tmp, points[i] + random.InsideUnitCircle().ToVector3() * 10, dir, null);
                                units.Add(go.GetComponent<Actor>());

                                var ec = go.GetComponent<EnemyController>();
                                // 设置长期落点：到达后不移除，途中被中断(回Idle)会继续走向该点
                                ec.HomePoint = center + random.InsideUnitCircle().ToVector3() * 5;
                                ec.SetNavDestination(ec.HomePoint);

                            }
                            else
                            {
                                break;
                            }
                        }

                    }
                    else if (completeCreat == false)
                    {
                        completeCreat = true;
                        EndCreatObjects();
                    }
                    else if (time % 5 == 0)
                    {
                        if (units.Count <= 3)
                        {
                            Trans(WaveState.NearEnd);
                        }
                    }

                    break;
                case WaveState.NearEnd:
                    if (time % 5 == 0)
                    {
                        if (units.Count == 0)
                        {
                            Trans(WaveState.End);
                        }
                    }
                    break;
                case WaveState.End:
                    Dispose();
                    return false;
            }
            return true;
        }

        /// <summary>按当前 center/range 重新计算随机生成环(仅随机生成点模式使用)</summary>
        void ResetPoints()
        {
            float theta = random.Range(0, 2 * Mathf.PI);
            for (int i = 0; i < perTickCreat; ++i)
            {
                var dx = random.Range(-1, 1f);//范围20度
                points[i] = center + new Vector3(Mathf.Cos(theta + dx), 0, Mathf.Sin(theta + dx)) * random.Range(range, range + 10);

                if (NavMesh.SamplePosition(points[i], out var hit, 100, NavMesh.AllAreas))
                {
                    points[i] = hit.position;
                }
                else
                {
                    points[i] = new Vector3(points[i].x, center.y, points[i].z);
                }
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
            for (int i = 0; i < perTickCreat; ++i)
            {
                var go = VFXManager.Creat(waveUseObject[0], points[i], Quaternion.Euler(0f, random.Range(0, 360), 0f));
                if (!go) continue;
                go.GetComponent<LimitedLife>().ResetLift(51);
                creatObject.Add(go);
            }
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

        void OnUnitDeath(Actor actor)
        {
            units.Remove(actor);
        }
        void Trans(WaveState state)
        {
            this.state = state;
            switch (state)
            {
                case WaveState.Start:
                    if (tip) WndManager.Instance.CreatNotice("Yuuka", "WaveStart_Zerg");

                    if (tip) AudioSvc.PlayMusic(AudioSvc.MusicGroup.Wave, 0.5f);
                    break;
                case WaveState.Ongoing:
                    time = 0;
                    break;
                case WaveState.NearEnd:
                    if (tip) WndManager.Instance.CreatNotice("Yuuka", "WaveEnd_Zerg");
                    break;
                case WaveState.End:
                    if (tip) AudioSvc.PlayMusic(AudioSvc.MusicGroup.Game, 0.5f);
                    break;
            }
        }

    }
}