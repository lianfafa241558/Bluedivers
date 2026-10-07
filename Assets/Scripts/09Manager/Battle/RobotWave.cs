using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.AI;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Audio;
using FPSGame.Gameplay;
using Random = System.Random;

namespace FPSGame.Managers
{
    /// <summary>
    /// 运输船(凤凰鹰)空投波次：按人口把单位装船，飞到等待点后向下投放。
    /// 生命周期与单位账本由 <see cref="WaveBase"/> 提供，本类只负责鹰群编排与投放。
    /// </summary>
    public class RobotWave : WaveBase
    {
        /// <summary>鹰到达等待点后多久投放单位(秒)</summary>
        const float DropDelay = 3f;

        /// <summary>每船人口上限</summary>
        const int MaxPopulation = 16;

        class EagleGroupInfo
        {
            public GameObject eagle;
            public List<GameObject> unitObjects;
            public float waitStartTime; // -1表示尚未进入等待阶段
            public bool dropped;
        }

        List<EagleGroupInfo> groups;

        float lastGroupSpawnTime;
        float spawnInterval;

        public RobotWave(WaveCreateParams param, Stack<GameObject> creats, List<GameObject> waveUseObject)
            : base(param, creats, waveUseObject)
        {
            groups = new();
            startTipName = "WaveStart_Robot";
            infoTitle = "侦测发现机器人";
            //机器人平均人口2.23左右
            spawnInterval = Mathf.Clamp(480f / Mathf.Max(1, creats.Count), 5, 15);
            lastGroupSpawnTime = Time.time - spawnInterval + 5;

            Trans(WaveState.Start);
        }

        protected override void OnDispose()
        {
            foreach (var g in groups)
            {
                if (g.eagle && !g.dropped && g.eagle.TryGetComponent(out PhoenixEagleController ctrl))
                {
                    ctrl.onWait.RemoveAllListeners();
                }
            }
            groups.Clear();
        }

        protected override void TickStart()
        {
            if (time == StartDelayTick) Trans(WaveState.Ongoing);
        }

        protected override void TickOngoing()
        {
            // 检查鹰群是否需要投放单位（等待阶段开始后3秒）
            bool hasPendingDrop = DropReadyGroups();

            // 周期性生成新鹰群
            if (creats.Count > 0)
            {
                if (Time.time - lastGroupSpawnTime >= spawnInterval)
                {
                    lastGroupSpawnTime = Time.time;
                    SpawnGroup();
                }
            }
            else
            {
                completeCreat = true;
            }

            // 所有单位已生成完毕，且没有待投放的单位，才检查是否进入 NearEnd
            if (completeCreat && !hasPendingDrop) TryEnterNearEnd();
        }

        protected override void TickNearEnd()
        {
            // NearEnd 阶段仍需检查投放（可能有鹰刚到达等待点）
            DropReadyGroups();
            base.TickNearEnd();
        }

        /// <summary>投放所有等待满 <see cref="DropDelay"/> 秒的鹰群，返回是否还有未投放的鹰群</summary>
        bool DropReadyGroups()
        {
            bool hasPending = false;

            for (int i = groups.Count - 1; i >= 0; --i)
            {
                var g = groups[i];
                if (g.dropped) continue;

                if (g.waitStartTime > 0 && Time.time - g.waitStartTime >= DropDelay)
                {
                    DropGroupUnits(g);
                    g.dropped = true;
                }
                else
                {
                    hasPending = true;
                }
            }

            return hasPending;
        }

        /// <summary>计算单位人口：Drone=1, HalfRange>=1=16, 其他=2</summary>
        static int GetUnitPopulation(GameObject prefab)
        {
            var entity = prefab.GetComponent<IEntity>();
            if (entity == null) return 2;

            if (entity.HalfRange >= 1f) return 16;
            if (entity.Id != null && entity.Id.Contains("Drone", System.StringComparison.OrdinalIgnoreCase))
                return 1;

            return 2;
        }

        /// <summary>创建运输船并按人口装船</summary>
        void SpawnGroup()
        {
            // 按人口取单位，每船16人口上限
            List<GameObject> popped = new();
            int totalPopulation = 0;

            while (creats.Count > 0 && totalPopulation < MaxPopulation)
            {
                if (!creats.TryPop(out var tmp)) break;

                int pop = GetUnitPopulation(tmp);
                // 如果加入这个单位会超出上限且已经有单位了，则放回并停止
                if (totalPopulation + pop > MaxPopulation && popped.Count > 0)
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
                var entity = popped[i].GetComponent<IEntity>();
                if (entity != null && entity.HalfRange >= 1f)
                {
                    bigIndex = i;
                    break;
                }
            }

            // points 为 null 时围绕中心取环，否则围绕预设点取环
            var eaglePos = GetDropPoint(points == null ? 5f : 0f);

            var eagle = VFXManager.Creat(waveUseObject[0], eaglePos, RandomYaw(), null);
            if (!eagle) return;

            EagleGroupInfo group = new()
            {
                eagle = eagle,
                unitObjects = new(),
                waitStartTime = -1,
                dropped = false
            };
            groups.Add(group);

            // 到达等待点时记录时间，DropDelay 秒后由 Tick 统一投放
            if (eagle.TryGetComponent(out PhoenixEagleController eagleCtrl))
            {
                EagleGroupInfo capturedGroup = group;
                eagleCtrl.onWait.RemoveAllListeners();
                eagleCtrl.onWait.AddListener(() =>
                {
                    if (!capturedGroup.dropped) capturedGroup.waitStartTime = Time.time;
                });
            }

            if (bigIndex >= 0) SpawnBigUnit(popped, bigIndex, group);
            else SpawnNormalUnits(popped, group);
        }

        /// <summary>大型单位：只实例化它，其余单位重新入栈</summary>
        void SpawnBigUnit(List<GameObject> popped, int bigIndex, EagleGroupInfo group)
        {
            for (int i = 0; i < popped.Count; ++i)
            {
                if (i != bigIndex) creats.Push(popped[i]);
            }

            var go = Object.Instantiate(popped[bigIndex], FpsHelper.GetNavMeshPoint(center), default, null);
            SetBehaviourEnabled(go, false);

            go.transform.parent = group.eagle.transform;
            go.transform.localPosition = new Vector3(0, -10, 0);
            group.unitObjects.Add(go);

            RegisterUnit(go);
            SetUnitAnimator(go, false);
        }

        /// <summary>普通单位：挂在鹰下方，2列布局按数量沿 Z 轴均匀分布</summary>
        void SpawnNormalUnits(List<GameObject> popped, EagleGroupInfo group)
        {
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
                SetBehaviourEnabled(go, false);

                go.transform.parent = group.eagle.transform;
                go.transform.localPosition = relativePos;
                group.unitObjects.Add(go);

                RegisterUnit(go);
                SetUnitAnimator(go, false);
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
                SetBehaviourEnabled(unit, true);

                // 确保所有Collider处于启用状态（Animator关键帧可能在启用后将其关闭）
                EnableUnitColliders(unit);

                // 启用Animator（开始落地动画）
                SetUnitAnimator(unit, true);

                // 设置长期落点：到达后不移除，途中被中断(回Idle)会继续走向该点
                SetUnitHome(unit);
            }
        }
    }
}
