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

        List<GameObject> waveUseObject;
        List<GameObject> creatObject;
        Stack<GameObject> creats;
        List<Actor> units;

        WaveState state;
        int perTickCreat;
        int time;
        Vector3[] points;
        Vector3 center;
        bool completeCreat;
        bool tip;

        bool IsDisposed;

        System.Random random;
        public ZergWave(WaveCreateParams param, Stack<GameObject> creats, List<GameObject> waveUseObject)
        {
            random = new Random(RandomUtils.Range(0, 1000));
            this.waveUseObject = new(waveUseObject);
            this.creats = creats;
            this.tip = param.tip;

            creatObject = new();
            units = new();
            center = param.center;

            BattleEventSub.OnEnemyDead += OnUnitDeath;

            if (param.points == null)
            {
                perTickCreat = Mathf.Max(1, Mathf.CeilToInt(creats.Count / 45f));//保底1个
                points = new Vector3[perTickCreat];
                float theta = random.Range(0, 2 * Mathf.PI);
                for (int i = 0; i < perTickCreat; ++i)
                {
                    var dx = random.Range(-1, 1f);//范围20度
                    points[i] = center + new Vector3(Mathf.Cos(theta + dx), 0, Mathf.Sin(theta + dx)) * random.Range(param.range, param.range + 10);

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

            IsDisposed = true;
        }

        public bool Tick()
        {

            --time;
            switch (state)
            {
                case WaveState.Start:
                    if (time == -5)
                    {
                        for (int i = 0; i < perTickCreat; ++i)
                        {
                            var go = VFXManager.Creat(waveUseObject[0], points[i]);
                            go.GetComponent<LimitedLife>().ResetLift(51);
                            creatObject.Add(go);
                        }
                    }
                    if (time == -10)
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

                                go.GetComponent<EnemyController>().SetNavDestination(
                                    center + random.InsideUnitCircle().ToVector3() * 5
                                );

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
                        for (int i = 0; i < creatObject.Count; ++i)
                        {
                            creatObject[i].GetComponentInChildren<Animator>().Play("End");
                            creatObject[i].GetComponent<LimitedLife>().ResetLift(6);
                        }

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

                    AudioSvc.PlayMusic(AudioSvc.MusicGroup.Wave, 0.3f);
                    break;
                case WaveState.Ongoing:
                    time = 0;
                    break;
                case WaveState.NearEnd:
                    if (tip) WndManager.Instance.CreatNotice("Yuuka", "WaveEnd_Zerg");
                    break;
                case WaveState.End:
                    AudioSvc.PlayMusic(AudioSvc.MusicGroup.Game, 0.2f);
                    break;
            }
        }

    }
}