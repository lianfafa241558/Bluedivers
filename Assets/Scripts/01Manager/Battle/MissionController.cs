using System.Collections.Generic;
using System.Linq;
using FpsGame.Mission;
using Unity.BaseTool;
using UnityEngine;
using Utils;
using TaskItem = TaskManager.TaskItem;

public class MissionController : MonoBehaviour
{
    BattleManager manager;
    System.Random random => manager.BattleRandom;

    TaskManager.SelectTaskData root;


    List<TaskItem> waitMissions;
    List<(Vector2 Pos,int Range)> missionCreatPoints;



    void Start()
    {
        manager = BattleManager.Instance;
        root = TaskManager.Instance.nowTask;
        waitMissions = new();
        missionCreatPoints = new();
        waitMissions.Add(root.main);
        waitMissions.Add(root.evacuate);
        waitMissions.AddRange(root.extras);
        waitMissions.AddRange(root.nests.SelectMany(nestItem => nestItem));
        waitMissions.AddRange(root.subs);
        InitAllMission();
        InitInterestPoint();
        //missionCreatPoints = null;
        //上面都折腾完了再刷新
        TerrainUtils.Refresh(true);
        //Debug.LogError("更新了地形" + gameObject);
        //TODO:为了方便测试
        /*
        foreach (var ad in root.RequiredAD)
        {
            BattleManager.Instance.Authorize(ad, true);
        }*/
    }

   
    /// <summary>
    /// 创建任务
    /// </summary>
    MissionBase CreatMission(TaskItem task)
    {
        var go = Instantiate(task.cfg.controller, transform);
        var size = RandomUtils.Range(random, go.mapEntitySize.x, go.mapEntitySize.y);
        
        go.Init(root, task, task.cfg.sprite, GenerateNewMissionPoint(size), size);
        return go;
    }



    /// <summary>
    /// 处理创建了，但是等待初始化的任务
    /// 最后按大小排列生成（优先刷大的，保证正确刷出）
    /// </summary>
    void InitAllMission() 
    {
        foreach(var task in waitMissions)
        {
            if (task.cfg.controller == null)
            {
                Debug.LogError("类型" + task.cfg.type + Tool.GetEnumString(task.cfg.type) + "没有控制器");
            }
        }

        waitMissions = waitMissions.Where(task=>task.cfg.controller).OrderByDescending(task => task.cfg.controller.mapEntitySize.y).ToList();
        //这里的实现就很丑陋了，但是没办法，不能让主任务直接生成
        MissionBase main=null, evacuate = null;
        List<MissionBase> subs=new();
        foreach (var task in waitMissions)
        {
            var go=CreatMission(task);
            if (task == root.main) main = go;
            else if (task == root.evacuate) evacuate = go;
            else if (root.subs.Contains(task)) subs.Add(go);
        }           
        //让撤离任务链接主任务
        evacuate.Link(main);
        foreach (var sub in subs)
        {
            sub.parent = main;
        }
        main.subTask = subs.ToArray();

        waitMissions = null;
    }
    /// <summary>
    /// 创建兴趣点
    /// </summary>
    void InitInterestPoint()
    {
        int count = random.Range(6,(int)Mathf.Sqrt(root.CameraSize));
        //Debug.LogWarning("兴趣点数量"+count);
        int totleWeight = root.mapCfg.interestPoints.Sum(item=>item.Value);
        //GameObject[] objects= new GameObject[count];
        for (int i =0;i< count;++i)
        {
            var pos = GenerateNewMissionPoint(8);
            if (pos == default) { Debug.LogWarning("兴趣点数量" + i); break; }
            Instantiate(root.mapCfg.interestPoints.WeightTake(totleWeight, random), pos, Quaternion.Euler(0, RandomUtils.Range(0, 360), 0), null);
            
        }
    }

    /// <summary>
    /// 生成新的任务点
    /// </summary>
    Vector3 GenerateNewMissionPoint(int newRange)
    {
        int mapRadius = (root.CameraSize) / 2;
        Vector2 center = root.MapSize / 2 * Vector2.one;
        Vector2 statrPoint = root.MapBorder * Vector2.one;
        if (newRange == 0) return center.ToVector3();

        // 步骤1：将地图划分为网格，保证均匀分布（网格大小为“最小安全间距”）
        float gridSize = newRange * 2; // 新点与其他点的最小安全间距（避免相切）
        int gridCount = Mathf.CeilToInt(mapRadius * 2 / gridSize); // 网格数量
        //Debug.LogError("创建半径:"+newRange+"网格数量"+ gridCount+"随机数"+ RandomUtils.Range(random, 0, gridCount));
        //比如地图半径128，newrange=64，那gridCount=2;取值会在[0,2)，最后随机为[0.5,1.5] +- 0.3
        //但是实际上最大只能是[0.5-1.5],[0.2,0.5)和 (1.5,1.8]是无效的
        //应该做范围限制为[0.5,那gridCount-0.5]
        //Debug.LogError("取值范围:[" + 0.5f+","+ (gridCount - 0.5f)+"]");
        for (int attemptCount=0; attemptCount < 100; ++attemptCount)
        {
            float gridX = Mathf.Clamp(RandomUtils.Range(random, 0, gridCount) +0.5f + RandomUtils.Range(random, -0.3f, 0.3f),0.5f, gridCount-0.5f) * gridSize;
            float gridY = Mathf.Clamp(RandomUtils.Range(random, 0, gridCount) +0.5f + RandomUtils.Range(random, -0.3f, 0.3f), 0.5f, gridCount - 0.5f) * gridSize;
            var candidatePos = statrPoint+ new Vector2(gridX, gridY);

            if (Vector2.Distance(candidatePos, center)+ newRange <= Mathf.Max( mapRadius - 5, newRange) //没超出地图范围
                && !IsOverlapWithExistingPoints(candidatePos, (int)(newRange*(100- attemptCount) /100f)))//没和其他任务实体相交
            {
                //Debug.LogError("创建位置:" + candidatePos);
                missionCreatPoints.Add((candidatePos, newRange));
                return new Vector3(candidatePos.x, TerrainUtils.WSToHeight(candidatePos), candidatePos.y);
            }
        }
        Debug.LogError("没有找到可用的点");
        return Vector3.zero;
    }

    bool IsOverlapWithExistingPoints(Vector2 candidatePos, int newRange)
    {
        foreach (var existing in missionCreatPoints)
        {
            // 如果横坐标/纵坐标差值已超过半径和，直接跳过（减少距离计算）
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
        return false;
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
       
        for (int i=0;i< missionCreatPoints.Count;++i)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(missionCreatPoints[i].Pos.ToVector3(), missionCreatPoints[i].Range);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(missionCreatPoints[i].Pos.ToVector3(), missionCreatPoints[i].Range+5);
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
