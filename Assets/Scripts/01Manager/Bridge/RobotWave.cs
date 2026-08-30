using System.Collections.Generic;
using Core;
using Core.Interface;
using FPSGame.AI;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using Utils;
using Random = System.Random;

namespace FPSGame.Game
{
    public class RobotWave : I_TickClass, System.IDisposable
    {
        List<GameObject> waveUseObject;
        Stack<GameObject> creats;
        List<Actor> units;
        List<EagleGroupInfo> groups;

        WaveState state;
        int time;
        Vector3 center;
        Vector3[] points;
        float range;
        bool completeCreat;
        bool tip;
        bool IsDisposed;

        System.Random random;

        float lastGroupSpawnTime;
        float spawnInterval;



        class EagleGroupInfo
        {
            public GameObject eagle;
            public List<GameObject> unitObjects;
            public float waitStartTime; // -1表示尚未进入等待阶段
            public bool dropped;
        }

        public RobotWave(WaveCreateParams param, Stack<GameObject> creats, List<GameObject> waveUseObject)
        {
            random = new Random(RandomUtils.Range(0, 1000));
            this.waveUseObject = new(waveUseObject);
            this.creats = creats;//机器人平均人口2.23左右
            this.tip = param.tip;
            range = param.range;
            points = param.points;
            units = new();
            groups = new();
            center = param.center;

            spawnInterval = Mathf.Clamp(480f / Mathf.Max(1, creats.Count),5,15);
            lastGroupSpawnTime = Time.time - spawnInterval+5;

            BattleEventSub.OnEnemyDead += OnUnitDeath;

            Trans(WaveState.Start);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            BattleEventSub.OnEnemyDead -= OnUnitDeath;

            foreach (var g in groups)
            {
                if (g.eagle && !g.dropped && g.eagle.TryGetComponent(out PhoenixEagleController ctrl))
                {
                    ctrl.onWait.RemoveAllListeners();
                }
            }
            groups?.Clear();
            units?.Clear();
            waveUseObject?.Clear();
            creats?.Clear();

            waveUseObject = null;
            creats = null;
            units = null;
            groups = null;
            random = null;

            IsDisposed = true;
        }

        public bool Tick()
        {
            --time;
            switch (state)
            {
                case WaveState.Start:
                    if (time == -4)
                    {
                        Trans(WaveState.Ongoing);
                    }
                    break;

                case WaveState.Ongoing:
                    // 检查鹰群是否需要投放单位（等待阶段开始后3秒）
                    bool hasPendingDrop = false;
                    for (int i = groups.Count - 1; i >= 0; --i)
                    {
                        var g = groups[i];
                        if (g.dropped) continue;
                        if (g.waitStartTime > 0 && Time.time - g.waitStartTime >= 3f)
                        {
                            DropGroupUnits(g);
                            g.dropped = true;
                        }
                        else
                        {
                            hasPendingDrop = true;
                        }
                    }

                    // 周期性生成新鹰群
                    if (creats.Count > 0)
                    {
                        if (Time.time - lastGroupSpawnTime >= spawnInterval)
                        {
                            lastGroupSpawnTime = Time.time;
                            SpawnGroup();
                        }
                    }
                    else if (!completeCreat)
                    {
                        completeCreat = true;
                    }

                    // 所有单位已生成完毕，且没有待投放的单位，才检查是否进入 NearEnd
                    if (completeCreat && !hasPendingDrop && time % 5 == 0)
                    {
                        if (units.Count <= 3)
                        {
                            Trans(WaveState.NearEnd);
                        }
                    }
                    break;

                case WaveState.NearEnd:
                    // NearEnd 阶段仍需检查投放（可能有鹰刚到达等待点）
                    for (int i = groups.Count - 1; i >= 0; --i)
                    {
                        var g = groups[i];
                        if (g.dropped) continue;
                        if (g.waitStartTime > 0 && Time.time - g.waitStartTime >= 3f)
                        {
                            DropGroupUnits(g);
                            g.dropped = true;
                        }
                    }

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

        /// <summary>计算单位人口：Drone=1, HalfRange>=1=16, 其他=2</summary>
        static int GetUnitPopulation(GameObject prefab)
        {
            var entity = prefab.GetComponent<I_Entity>();
            if (entity == null) return 2;

            if (entity.HalfRange >= 1f) return 16;
            if (entity.Id != null && entity.Id.Contains("Drone", System.StringComparison.OrdinalIgnoreCase))
                return 1;

            return 2;
        }

        /// <summary>
        /// 创建运输船
        /// </summary>
        void SpawnGroup()
        {
            // 按人口取单位，每船16人口上限
            const int maxPopulation = 16;
            List<GameObject> popped = new();
            int totalPopulation = 0;

            while (creats.Count > 0 && totalPopulation < maxPopulation)
            {
                if (!creats.TryPop(out var tmp)) break;

                int pop = GetUnitPopulation(tmp);
                // 如果加入这个单位会超出上限且已经有单位了，则放回并停止
                if (totalPopulation + pop > maxPopulation && popped.Count > 0)
                {
                    creats.Push(tmp);
                    break;
                }

                popped.Add(tmp);
                totalPopulation += pop;
            }

            if (popped.Count == 0) return;

            // 检查是否有大型单位（HalfRange >= 1，人口=16）
            int bigIndex = -1;
            for (int i = 0; i < popped.Count; ++i)
            {
                var entity = popped[i].GetComponent<I_Entity>();
                if (entity != null && entity.HalfRange >= 1f)
                {
                    bigIndex = i;
                    break;
                }
            }

            Vector3 eaglePos;
            if (points == null)
            {
                eaglePos = FpsHelper.GetNavMeshPoint(VectorUtils.GetRandomPointInCircle(center, range + 5, range + 15));
            }
            else
            {
                eaglePos = FpsHelper.GetNavMeshPoint(VectorUtils.GetRandomPointInCircle(points.RandomTake(), range, range + 10));
            }

            var eagle = VFXManager.Creat(waveUseObject[0], eaglePos, Quaternion.AngleAxis(RandomUtils.Range(0f, 360f), Vector3.up), null);
            if (!eagle) return;

            EagleGroupInfo group = new() {
                eagle = eagle,
                unitObjects = new(),
                waitStartTime = -1,
                dropped = false
            };

            var eagleCtrl = eagle.GetComponent<PhoenixEagleController>();
            if (eagleCtrl)
            {
                groups.Add(group);
                EagleGroupInfo capturedGroup = group;
                eagleCtrl.onWait.RemoveAllListeners();
                eagleCtrl.onWait.AddListener(() => {
                    if (!capturedGroup.dropped)
                    {
                        capturedGroup.waitStartTime = Time.time;
                    }
                });
            }
            else
            {
                groups.Add(group);
            }

            if (bigIndex >= 0)
            {
                // 大型单位：只实例化它，其余重新入栈
                var bigUnit = popped[bigIndex];
                for (int i = 0; i < popped.Count; ++i)
                {
                    if (i != bigIndex)
                    {
                        creats.Push(popped[i]);
                    }
                }

                var go = Object.Instantiate(bigUnit, FpsHelper.GetNavMeshPoint(center), default, null);
                foreach (var item in go.GetComponents<Behaviour>()) if (item is not Health) item.enabled = false;

                go.transform.parent = eagle.transform;
                go.transform.localPosition = new Vector3(0, -10, 0);
                group.unitObjects.Add(go);

                var actor = go.GetComponent<Actor>();
                if (actor) units.Add(actor);

                var fx = go.GetComponent<EnemyControllerFX>();
                if (fx && fx.Animator)
                {
                    fx.Animator.enabled = false;
                }
            }
            else
            {
                // 正常创建，相对偏移计算（2列布局，根据数量均匀分布）
                const float columnOffsetX = 7f;
                const float totalLengthZ = 12f;
                const float heightY = -1f;
                const int columnCount = 2;

                int actualCount = popped.Count;
                int rowCount = (actualCount + columnCount - 1) / columnCount;
                float startZ = -totalLengthZ / 2f;
                float rowSpacingZ = rowCount > 1 ? totalLengthZ / (rowCount - 1) : 0f;

                for (int i = 0; i < actualCount; ++i)
                {
                    int col = i % columnCount;
                    int row = i / columnCount;
                    float x = col == 0 ? -columnOffsetX : columnOffsetX;
                    float z = startZ + row * rowSpacingZ;
                    Vector3 relativePos = new(x, heightY, z);

                    var go = Object.Instantiate(popped[i], FpsHelper.GetNavMeshPoint(center), default, null);
                    foreach (var item in go.GetComponents<Behaviour>()) if (item is not Health) item.enabled = false;
                    go.transform.parent = eagle.transform;
                    go.transform.localPosition = relativePos;
                    group.unitObjects.Add(go);

                    var actor = go.GetComponent<Actor>();
                    if (actor) units.Add(actor);

                    var fx = go.GetComponent<EnemyControllerFX>();
                    if (fx && fx.Animator)
                    {
                        fx.Animator.enabled = false;
                    }
                }
            }
        }

        void DropGroupUnits(EagleGroupInfo group)
        {
            if (!group.eagle) return;

            foreach (var unit in group.unitObjects)
            {
                if (!unit) continue;

                unit.transform.SetParent(null);
                var pos = unit.transform.position;
                unit.transform.position = FpsHelper.GetNavMeshPoint(pos);
                foreach (var item in unit.GetComponents<Behaviour>()) if (item is not Health) item.enabled = true;

                // 确保所有Collider处于启用状态（Animator关键帧可能在启用后将其关闭）
                foreach (var col in unit.GetComponentsInChildren<Collider>())
                {
                    col.enabled = true;
                }

                // 启用Animator（开始落地动画）
                if (unit.TryGetComponent(out EnemyControllerFX fx)&& fx.Animator)
                {
                    fx.Animator.enabled = true;
                }
                var ec = unit.GetComponent<EnemyController>();
                // 设置长期落点：到达后不移除，途中被中断(回Idle)会继续走向该点
                ec.HomePoint = center + random.InsideUnitCircle().ToVector3() * 5;
                ec.SetNavDestination(ec.HomePoint);
            }

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
                    //TODO:现在没有对应的语音
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