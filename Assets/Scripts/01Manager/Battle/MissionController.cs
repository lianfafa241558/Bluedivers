using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FpsGame.Mission;

using UnityEngine;
using Utils;
using TaskItem = TaskManager.TaskItem;

public enum MissionInitMode
{
    [InspectorName("从数据生成")]
    GenerateFromData,
    [InspectorName("从场景获取")]
    FindFromScene
}

public class MissionController : MonoBehaviour
{
    BattleManager manager;
    System.Random random => manager.BattleRandom;

    TaskManager.SelectTaskData root;

    [SerializeField]
    [InspectorName("任务初始化模式")]
    private MissionInitMode _initMode = MissionInitMode.GenerateFromData;

    List<TaskItem> waitMissions;
    /// <summary>当前战斗中所有任务实例</summary>
    public List<MissionBase> missions;
    List<(Vector2 Pos,int Range)> missionCreatPoints;

    private bool isInitialized;
    private Transform EntityRoot;

    /// <summary>
    /// 地表占地圆缓存（巨型悬崖等）：每次生成任务点前从 <see cref="TerrainUtils.AreaCircles"/> 现读一次。
    /// <para>不能只读一次就长期用——石头可能被 ModifyTerrain 清除，缓存到底会一直躲着已经不存在的石头。</para>
    /// </summary>
    readonly List<TerrainUtils.AreaCircle> _areaCircles = new();

    public void Init(MissionInitMode mode)
    {
        _initMode = mode;
        StartCoroutine(InitializeAsync());
    }


    private List<TaskItem> GetTask()
    {
        List<TaskItem> list = new() {
            root.main,
            root.evacuate
        };
        list.AddRange(root.extras);
        list.AddRange(root.nests.SelectMany(nestItem => nestItem));
        list.AddRange(root.subs);
        return list;
    }

    private IEnumerator InitializeAsync()
    {
        manager = BattleManager.Instance;
        root = TaskManager.Instance.nowTask;

        missions = new();

        if (_initMode == MissionInitMode.FindFromScene)
        {
            Debug.Log("从场景收集任务");
            yield return CollectMissionsFromScene();
        }
        else
        {
            EntityRoot = new GameObject("EntityRoot").transform;
            waitMissions = GetTask();
            missionCreatPoints = new();

            Debug.Log("开始生成任务");
            yield return InitAllMission();
            Debug.Log("开始生成兴趣点");
            yield return InitInterestPoint();

            var async = TerrainUtils.AsyncRefresh(true);
            while (!async.isDone)
            {
                yield return null;
            }
        }
        //TODO:为了方便测试。这个不该扔到战备控制器里
        /*
        foreach (var ad in root.RequiredAD)
        {
            BattleManager.Instance.Authorize(ad, true);
        }*/
        isInitialized = true;
        while (!manager.IsStartBattle)
        {
            yield return null;
        }
        foreach (var item in missions)
        {
            if (!item.parent)
            {
                item.enabled = true;
                item.EventStart();

            }
        }
        yield return null;
        foreach (var item in missions)
        {
            if (item.parent)
            {
                item.enabled = true;
                item.EventStart();
            }
        }
        waitMissions = null;
        //missionCreatPoints = null;
        //missions = null;

        yield break;
    }

    public IEnumerator WaitForInitialization()
    {
        while (!isInitialized)
            yield return null;
    }


    /// <summary>
    /// 创建任务
    /// </summary>
    IEnumerator CreatMission(TaskItem task,Action<MissionBase> onComplete)
    {
        
        MissionBase go = Instantiate(task.cfg.controller, transform);
        var size = RandomUtils.Range(random, go.mapEntitySize.x, go.mapEntitySize.y);
        go.Init(root, task, task.cfg.sprite, GenerateNewMissionPoint(size), size, EntityRoot);
        while (!go.IsInitialized) yield return null;
        missions.Add(go);
        go.enabled = false;
        onComplete?.Invoke(go);
    }



    /// <summary>
    /// 处理创建了，但是等待初始化的任务
    /// 最后按大小排列生成（优先刷大的，保证正确刷出）
    /// </summary>
    IEnumerator InitAllMission() 
    {
        foreach(var task in waitMissions)
        {
            if (task.cfg.controller == null)
            {
                Debug.LogError("类型" + task.cfg.type + Tool.GetEnumString(task.cfg.type) + "没有控制器");
            }
        }
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        waitMissions = waitMissions.Where(task=>task.cfg.controller).OrderByDescending(task => task.cfg.controller.mapEntitySize.y).ToList();
        //这里的实现就很丑陋了，但是没办法，不能让主任务直接生成
        MissionBase main=null, evacuate = null;
        List<MissionBase> subs=new();
        foreach (var task in waitMissions)
        {
            MissionBase go=null;
            yield return CreatMission(task, (re) => go = re);
            switch (go.missionType)
            {
                case MissionType.Main:
                    main = go;
                    break;
                case MissionType.Extra:
                    break;
                case MissionType.Nest:
                    break;
                case MissionType.Sub:
                    subs.Add(go);
                    break;
                case MissionType.Evacuate:
                    evacuate = go;
                    break;
            }
            Debug.Log($"创建任务{go.name}耗时: {sw.ElapsedMilliseconds} ms");
            sw.Restart();
            yield return null;
        }
        //让撤离任务链接主任务
        evacuate.Link(main);
        foreach (var sub in subs)
        {
            sub.parent = main;
        }
        main.subTask = subs.ToArray();

        //waitMissions = null;
    }
    /// <summary>
    /// 场景模式：从场景中收集已布置好的 Mission，注入数据引用并建立链接
    /// </summary>
    private IEnumerator CollectMissionsFromScene()
    {
        var sceneMissions = FindObjectsByType<MissionBase>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
        foreach (var mission in sceneMissions)
        {
            mission.InitFromSceneData(root);
            missions.Add(mission);
            mission.transform.parent = transform;
            mission.enabled = false;
            yield return null;
        }

        // 按 MissionEnum 分类（与 InitAllMission 的分类逻辑一致）
        MissionBase main = null, evacuate = null;
        List<MissionBase> subs = new();

        foreach (var go in sceneMissions)
        {
            switch (go.missionType)
            {
                case MissionType.Main:
                    main = go;
                    break;
                case MissionType.Extra:
                    break;
                case MissionType.Nest:
                    break;
                case MissionType.Sub:
                    subs.Add(go);
                    break;
                case MissionType.Evacuate:
                    evacuate = go;
                    break;
            }
        }

        if (evacuate != null && main != null)
            evacuate.Link(main);
        foreach (var sub in subs)
            sub.parent = main;
        if (main != null)
            main.subTask = subs.ToArray();
    }
    /// <summary>
    /// 创建兴趣点
    /// </summary>
    IEnumerator InitInterestPoint()
    {
        int count = random.Range(6,(int)Mathf.Sqrt(root.CameraSize));
        //Debug.LogWarning("兴趣点数"+count);
        int totleWeight = root.mapCfg.interestPoints.Sum(item=>item.Value);
        //GameObject[] objects= new GameObject[count];
        for (int i =0;i< count;++i)
        {
            var pos = GenerateNewMissionPoint(8);
            if (pos == default) { Debug.LogWarning("兴趣点数量" + i); break; }
            Instantiate(root.mapCfg.interestPoints.WeightTake(totleWeight, random), pos, Quaternion.Euler(0, RandomUtils.Range(0, 360), 0), EntityRoot);
            yield return null;
        }
    }

    /// <summary>
    /// 生成新的任务点
    /// </summary>
    Vector3 GenerateNewMissionPoint(int newRange)
    {
        // 地表占地圆（巨型悬崖等）每次现读一遍：它们可能已被 ModifyTerrain 清除，
        // 只读一次就长期用的话会一直躲着已经不存在的石头
        RefreshAreaCircles();

        int mapRadius = (root.CameraSize) / 2;
        Vector2 center = root.MapSize / 2 * Vector2.one;
        Vector2 statrPoint = root.MapBorder * Vector2.one;
        if (newRange == 0)
        {
            // 半径 0 的任务（如主任务）固定刷在地图中心，这里不做挪位，只警告：
            // 中心若已被占（任务点/地表占地圆如巨型悬崖），应该在地形或布局侧解决
            if (IsOverlapWithExistingPoints(center, 0))
            {
                Debug.LogWarning($"地图中心 {center} 落在已占用范围内，该任务点可能与已有物体重叠");
            }
            return center.ToVector3();
        }

        // 步骤1：将地图划分为网格，保证均匀分布（网格大小为"最小安全间距"）
        float gridSize = newRange * 2; // 新点与其他点的最小安全间距（避免相切）
        int gridCount = Mathf.CeilToInt(mapRadius * 2 / gridSize); // 网格数量

        // 收集多个符合条件的候选点，然后选最平坦的
        const int candidateCount = 4;
        List<Vector2> candidates = new(candidateCount);

        for (int attemptCount=0; attemptCount < 100 && candidates.Count < candidateCount; ++attemptCount)
        {
            float gridX = Mathf.Clamp(RandomUtils.Range(random, 0, gridCount) +0.5f + RandomUtils.Range(random, -0.3f, 0.3f),0.5f, gridCount-0.5f) * gridSize;
            float gridY = Mathf.Clamp(RandomUtils.Range(random, 0, gridCount) +0.5f + RandomUtils.Range(random, -0.3f, 0.3f), 0.5f, gridCount - 0.5f) * gridSize;
            var candidatePos = statrPoint+ new Vector2(gridX, gridY);

            if (Vector2.Distance(candidatePos, center)+ newRange <= Mathf.Max( mapRadius - 5, newRange) //没超出地图范围
                && !IsOverlapWithExistingPoints(candidatePos, (int)(newRange*(100- attemptCount) /100f)))//没和其他占地点（任务/兴趣点/地表占地圆）相交
            {
                candidates.Add(candidatePos);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"没有找到可用的点（半径 {newRange}，已有任务点 {missionCreatPoints.Count} 个，"
                + $"需避让的地表占地圆 {_areaCircles.Count} 个）");
            return Vector3.zero;
        }

        // 选最平坦的点：在候选点周围采样高度，计算标准差
        Vector2 bestPoint = candidates[0];
        float bestFlatness = float.MaxValue;
        int sampleStep = Mathf.Max(2, newRange / 4); // 采样间距

        foreach (var pos in candidates)
        {
            float mean = 0;
            int sampleCount = 0;
            // 从 -range/2 到 +range/2 范围内均匀采样
            for (float dx = -newRange * 0.5f; dx <= newRange * 0.5f; dx += sampleStep)
            {
                for (float dy = -newRange * 0.5f; dy <= newRange * 0.5f; dy += sampleStep)
                {
                    mean += TerrainUtils.WSToHeight(pos + new Vector2(dx, dy));
                    sampleCount++;
                }
            }
            mean /= sampleCount;

            float variance = 0;
            for (float dx = -newRange * 0.5f; dx <= newRange * 0.5f; dx += sampleStep)
            {
                for (float dy = -newRange * 0.5f; dy <= newRange * 0.5f; dy += sampleStep)
                {
                    float h = TerrainUtils.WSToHeight(pos + new Vector2(dx, dy));
                    variance += (h - mean) * (h - mean);
                }
            }
            variance /= sampleCount;

            if (variance < bestFlatness)
            {
                bestFlatness = variance;
                bestPoint = pos;
            }
        }

        missionCreatPoints.Add((bestPoint, newRange));
        return new Vector3(bestPoint.x, TerrainUtils.WSToHeight(bestPoint), bestPoint.y);
    }

    /// <summary>
    /// 候选点是否与"已占用的地方"相交。两张表都查：
    /// <para>① <see cref="missionCreatPoints"/>（已生成的任务点 / 兴趣点）</para>
    /// <para>② <see cref="_areaCircles"/>（地表占地圆：巨型悬崖等，由 <see cref="RefreshAreaCircles"/> 现读缓存）</para>
    /// </summary>
    bool IsOverlapWithExistingPoints(Vector2 candidatePos, int newRange)
    {
        foreach (var existing in missionCreatPoints)
        {
            // 如果横坐标、纵坐标差值已超过半径和，直接跳过（减少距离计算）
            float dx = Mathf.Abs(candidatePos.x - existing.Pos.x);
            float dy = Mathf.Abs(candidatePos.y - existing.Pos.y);
            float sumRadius = newRange + existing.Range+5;//至少间隔5
            if (dx > sumRadius || dy > sumRadius)
            {
                continue;
            }

            float distance = Vector2.Distance(candidatePos, existing.Pos);
            if (distance < sumRadius)
            {
                return true;
            }
        }

        // 地表占地圆（巨型悬崖等）：同一套"圆心距 < 半径和 + 5"
        for (int i = 0; i < _areaCircles.Count; i++)
        {
            TerrainUtils.AreaCircle circle = _areaCircles[i];
            float circleSumRadius = newRange + circle.Radius + 5f;
            float dx = Mathf.Abs(candidatePos.x - circle.Center.x);
            float dy = Mathf.Abs(candidatePos.y - circle.Center.y);
            if (dx > circleSumRadius || dy > circleSumRadius)
            {
                continue;
            }

            if (Vector2.Distance(candidatePos, circle.Center) < circleSumRadius)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 现读 <see cref="TerrainUtils.AreaCircles"/>（地表占地圆：巨型悬崖等）到本地缓存。
    /// <para>纯数据列表就是"当前还活着的占地集合"：石头被 <c>RockCoverDestructor</c> 清除后会自动从里面消失，
    /// 所以这里每次都重读，不需要事件、也不需要在别处维护副本。</para>
    /// <para>本类只认"圆"，不认识是谁放的、也不需要引用地形生成器所在程序集。</para>
    /// </summary>
    void RefreshAreaCircles()
    {
        int before = _areaCircles.Count;
        _areaCircles.Clear();
        IReadOnlyList<TerrainUtils.AreaCircle> circles = TerrainUtils.AreaCircles;
        for (int i = 0; i < circles.Count; i++)
        {
            _areaCircles.Add(circles[i]);
        }
        if (_areaCircles.Count != before)
        {
            Debug.Log($"[任务点] 地表占地圆：{before} → {_areaCircles.Count}（石头被清除的话这里会减少）");
        }
    }



    public void AddBattleDataItem(int playerIndex,string name)
    {
        //Debug.LogError("root"+root);
        //Debug.LogError("BattleData" + root.BattleData);
        //Debug.LogError("playerIndex" + root.BattleData[playerIndex]);
        ++root.BattleData[playerIndex][name];
    }


    private void OnDrawGizmosSelected()
    {
        // 非运行期 root 未初始化，下面要用 MapSize/CameraSize，直接返回避免空引用
        if (root == null) return;

        if (missionCreatPoints!=null)
        {
            for (int i = 0; i < missionCreatPoints.Count; ++i)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(missionCreatPoints[i].Pos.ToVector3(), missionCreatPoints[i].Range);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(missionCreatPoints[i].Pos.ToVector3(), missionCreatPoints[i].Range + 5);
            }
        }
        Gizmos.color = Color.green;
        Vector3 center= new Vector3(root.MapSize / 2,30, root.MapSize / 2);
        float range = root.CameraSize / 2;
        for (int i = 0; i < 36; ++i)
        {
            Gizmos.DrawLine(
                center + new Vector3(Mathf.Sin(Mathf.PI / 18 * i) * range, 0, Mathf.Cos(Mathf.PI / 18 * i) * range),
                center + new Vector3(Mathf.Sin(Mathf.PI / 18 * (i + 1)) * range, 0, Mathf.Cos(Mathf.PI / 18 * (i + 1)) * range)
            );
        }
    }

}
