using System.Collections;
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
        root = TaskManager.Instance.nowTaskCfg;
        waitMissions = new();
        missionCreatPoints = new();

        waitMissions.Add(root.nowTask.main);
        waitMissions.Add(root.nowTask.evacuate);
        waitMissions.AddRange(root.nowTask.extra);
        waitMissions.AddRange(root.nowTask.nest.SelectMany(nestItem => nestItem));

        InitAllMission();
        //上面都折腾完了再刷新
        TerrainUtils.Refresh(true);
        //Debug.LogError("更新了地形" + gameObject);

    }

   
    /// <summary>
    /// 仅创建，没有初始化，而是添加到列表
    /// 最后按大小排列生成（优先刷大的，保证正确刷出）
    /// </summary>
    MissionBase CreatMission(TaskItem task)
    {
        var go = Instantiate(task.cfg.controller, transform);
        var size = RandomUtils.Range(random, go.mapEntitySize.x, go.mapEntitySize.y);
        go.Init(root, task,task.cfg.sprite,GenerateNewMissionPoint(size),size);
        foreach (var sub in go.subTask)
        {
            var subGo = Instantiate(sub, go.transform);
            size = RandomUtils.Range(random, go.mapEntitySize.x, go.mapEntitySize.y);
            //这里有问题，没有独立的图标
            subGo.Init(root, task, task.cfg.sprite, GenerateNewMissionPoint(size), size);
            subGo.parent = go;
        }
        return go;
    }

   

    /// <summary>
    /// 处理创建了，但是等待初始化的任务
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
        foreach (var task in waitMissions)
        {
            var go=CreatMission(task);
            if (task == root.nowTask.main) main = go;
            else if (task == root.nowTask.evacuate) evacuate = go;
        }           
        //让撤离任务链接主任务
        evacuate.Link(main);
        waitMissions = null;
        missionCreatPoints = null;
    }


    /// <summary>
    /// 生成新的任务点
    /// </summary>
    Vector3 GenerateNewMissionPoint(int newRange)
    {
        Vector2 statrPoint = Constants.MapBorder / 2 * Vector2.one;
        Vector2 center = root.MapSize / 2 * Vector2.one;
        int mapRadius = (root.CameraSize)/2;
        if (newRange == 0) return center;

        // 步骤1：将地图划分为网格，保证均匀分布（网格大小为“最小安全间距”）
        float gridSize = newRange * 2; // 新点与其他点的最小安全间距（避免相切）
        int gridCount = Mathf.CeilToInt(mapRadius * 2 / gridSize); // 网格数量
        //Debug.LogError("创建半径:"+newRange+"网格数量"+ gridCount);
        for (int attemptCount=0; attemptCount < 300; ++attemptCount)
        {
            float gridX = (RandomUtils.Range(random, 0, gridCount) + RandomUtils.Range(random, 0.2f, 0.8f)) * gridSize;
            float gridY = (RandomUtils.Range(random, 0, gridCount) + RandomUtils.Range(random, 0.2f, 0.8f)) * gridSize;
            var candidatePos = statrPoint+ new Vector2(gridX, gridY);

            if (Vector2.Distance(candidatePos, center) < mapRadius - 5 - newRange//没超出地图范围
                && !IsOverlapWithExistingPoints(candidatePos, newRange))//没和其他任务实体相交
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

}
