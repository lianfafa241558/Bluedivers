using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using FpsGame.MapUtils;
using GameContract;
using PEMaths;

using Unity.FPS.Game;
using UnityEngine;
using Utils;

public class BattleManager : Singleton<BattleManager>
{
    public bool IsStartBattle;

    public ActorsManager ACCont;
    public AirdropController ADCont;
    public BattleRoleManager BRCont;
    public WaveManager WaveCont;
    public MissionController MissionCont;
    public PatrolContriller PatrolCont;
    public PathRequestManager RequestManager;

    private UnitQueryGrid unitQueryGrid;
    private MapRoot mapRoot;

    public System.Random BattleRandom { get;private set; }

    public static void Creat()
    {
        var manager = new GameObject("BattleManager").AddComponent<BattleManager>();
        manager.StartCoroutine(manager.Init());

    }
    private IEnumerator Init()
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        BattleRandom = new(TaskManager.Instance.nowTask.taskCfg.seed);

        yield return InitTerrain();
        Debug.Log($"地形耗时: {sw.ElapsedMilliseconds} ms");
        sw.Restart();


        ACCont = new GameObject("ActorsManager").AddComponent<ActorsManager>();
        ACCont.transform.SetParent(transform);

        MissionCont = new GameObject("MissionController").AddComponent<MissionController>();
        MissionCont.transform.SetParent(transform);
        Debug.Log($"开始任务");
        yield return MissionCont.WaitForInitialization();
        Debug.Log($"任务耗时: {sw.ElapsedMilliseconds} ms");
        sw.Restart();

        ADCont = new GameObject("AirdropController").AddComponent<AirdropController>();
        ADCont.Init();
        ADCont.transform.SetParent(transform);
        BRCont = new GameObject("BattleRoleCont").AddComponent<BattleRoleManager>();
        BRCont.transform.SetParent(transform);
        WaveCont = new GameObject("WaveCont").AddComponent<WaveManager>();
        WaveCont.transform.SetParent(transform);
        PatrolCont = new GameObject("PatrolCont").AddComponent<PatrolContriller>();
        PatrolCont.transform.SetParent(transform);
        WndManager.Instance.CreatNotice("Yuuka", "MissionStart");
        RequestManager = new GameObject("RequestManager").AddComponent<PathRequestManager>();
        RequestManager.transform.SetParent(transform);
        Debug.Log("完成主要内容初始?");
        yield return null;

        //ResManager.Instance.SetLoadSceneExtraProgress(1);
        GameRoot.GameState = GameStateEnum.Game;
        yield return null;
        WndManager.WindowState = WindowStateEnum.Game;
        IsStartBattle = true;
        Debug.Log($"其他初始化耗时 {sw.ElapsedMilliseconds} ms");


    }

    void Start()
    {

        var pos = mapRoot.rect;
        unitQueryGrid = new(new((PEVector2)pos.center, pos.size.x / 2, pos.size.z / 2), 30);
        

        BattleEventSub.OnUnitPosChange += OnUnitPosChange;
        BattleEventSub.OnEnemyCreate += OnEnemyCreate;
        BattleEventSub.OnEnemyDead += OnEnemyDeath;
        GlobalEventSub.OnPlayerCreate += OnPlayerCreate;
        BattleEventSub.OnPlayerDead += OnPlayerDeath;
        GlobalEventSub.OnOOPartCollect += OOPartCollect;
        GlobalEventSub.OnDaySwitch += OnDatSwitch;
    }
    private void OnDestroy()
    {
        BattleEventSub.OnUnitPosChange -= OnUnitPosChange;
        BattleEventSub.OnEnemyCreate -= OnEnemyCreate;
        BattleEventSub.OnEnemyDead -= OnEnemyDeath;
        GlobalEventSub.OnPlayerCreate -= OnPlayerCreate;
        BattleEventSub.OnPlayerDead -= OnPlayerDeath;
        GlobalEventSub.OnOOPartCollect -= OOPartCollect;
        GlobalEventSub.OnDaySwitch -= OnDatSwitch;
    }



    IEnumerator InitTerrain()
    {
        Transform transMapRoot = GameObject.FindGameObjectWithTag("MapRoot").transform;
        mapRoot = transMapRoot.GetComponent<MapRoot>();
       
        var terrain=TerrainUtils.Main = mapRoot.terrain;
        var cfg=TaskManager.Instance.nowTask;
        var terrainData = terrain.terrainData;

       
        var mapRes= cfg.MainCfg.sizeType switch {
            SizeType.Small => 512,
            SizeType.Medium => 1024,
            SizeType.Large => 1024,
            _ => 512 
        };
        
        terrainData.heightmapResolution = mapRes + 1;
        terrainData.alphamapResolution = mapRes;
        // 分辨率变更后重新设置Main，同步静态缓存
        TerrainUtils.Main = terrain;
        //不能调换顺序，会出问题
        terrainData.size = new(cfg.MapSize, cfg.MapHeight, cfg.MapSize);
        // size.y 变更后刷新 terrainHeight 缓存，否则 AdditionTerrain 高度计算使用旧值
        TerrainUtils.Main = terrain;
        //Debug.LogWarning("地图尺寸" + cfg.MainCfg.sizeType + " 地图大小 + cfg.MapSize);
        //Debug.LogWarning("地图真实" + mapRoot.terrain.terrainData.size);
        //terrainData.size = new(cfg.MapSize, cfg.MapHeight, cfg.MapSize);
        mapRoot.Init();
        List<TerrainItemInfo> infos = new(TaskManager.Instance.nowTask.mapCfg.TerrainItem);
        var nestinfo = TaskManager.Instance.nowTask.campData.NestTerrainItem;
        infos[3] = nestinfo;
        yield return mapRoot.GetComponent<GenerateNoiseTerrain>().SetTextures(infos.Select(item=>item.diffuseTexture).ToArray(), infos.Select(item => item.tileSize).ToArray());

        yield return mapRoot.GetComponent<GenerateNoiseTerrain>().ApplyFractalNoiseToTerrain(cfg.taskCfg.terrainType);

        var debugger = transMapRoot.GetComponent<UnitQueryGridDebugger>();
        if (debugger.IsValid())
        {
            debugger.grid = unitQueryGrid;
        }
    }


    public List<I_Actor> FindUnits(IPERange range, TargetCfg targetCfg, System.Func<I_Actor, bool> customFilter = null)
    {
        return new List<I_Actor>(unitQueryGrid.QueryUnits(range, targetCfg, customFilter));
    }
    public List<I_Actor> FindUnits(TargetCfg targetCfg, System.Func<I_Actor, bool> customFilter = null)
    {
        return new List<I_Actor>(unitQueryGrid.QueryUnits(targetCfg, customFilter));
    }

    public GameObject CreatUnit(UnitTier tier,Vector3 pos,float range,bool isFixed=true)
    {
       return WaveCont.CreatUnit(tier, pos,range, isFixed);
    }

    public List<GameObject> CreatPatrol(Vector3 pos)
    {
        return WaveCont.CreatPatrol(pos);
    }

    public bool CreatWave(WaveCreateParams param) => WaveCont.CreatWave(param);

    private void OnUnitPosChange(I_Actor unit)
    {
        if (unit.Type != UnitTypeEnum.None)
        {
            unitQueryGrid.UpdateNodes(unit);
        }
    }

    private void OnEnemyDeath(Actor unit)
    {
        unitQueryGrid.RemoveUnit(unit);
    }

    private void OnEnemyCreate(Actor unit)
    {
        unitQueryGrid.AddUnit(unit);
    }
    private void OnPlayerDeath(Actor unit)
    {
        //unitQueryGrid.RemoveUnit(unit.GetComponent<Actor>());
    }

    private void OnPlayerCreate(I_Actor unit)
    {
        unitQueryGrid.AddUnit(unit);
    }


    public void ReleaseAirdrop(Vector3 point,int id, System.Action<GameObject> action=default)
    {
        ReleaseAirdrop(point, RandomUtils.Range(0, 360), id, action);
    }
    public void ReleaseAirdrop(Vector3 point,float angle, int id, System.Action<GameObject> action = default)
    {
        var beacon = VFXManager.Creat(ResSvc.Instance.LoadObject<GameObject>("Prefabs/Airdrop/VFX_AirdropPoint"), point, Quaternion.Euler(0,angle, 0), null);
        beacon.GetComponent<VFXAirdropEffect>()?.TmpAirdrop(point, ResSvc.Instance.GetAirdrop(id), action);
    }

    public void Authorize(int id, bool state)
    {
        ADCont.Authorize(id,state);
    }


    public void AddBattleDataItem(int playerIndex, string name)
    {
        MissionCont.AddBattleDataItem(playerIndex, name);
    }

    public void EndGame(int delay,GameResult result= GameResult.Unknow)
    {
        if (result != GameResult.Unknow) TaskManager.Instance.nowTask.result = result;
        //GlobalEventManager.Evacuate();
        GameRoot.CreateTimer(() => {
            // 先切到 UI 状态，让 PlayerWnd/SubtitleWnd 的 Update 不再执行，避免场景卸载期间 NRE
            WndManager.WindowState = WindowStateEnum.UI;
            ResSvc.Instance.AsyncLoadScene("GameEnd", () => {
                
                GameRoot.GameState = GameStateEnum.GameEnd;
                WndManager.WindowState = WindowStateEnum.UI;
            }, false);
        }, delay);
    }

    private void OOPartCollect(GameObject user, OOPartEnum type, int count)
    {
        var dic = TaskManager.Instance.nowTask.collectProperty;
        if (!dic.TryAdd(type, count)) dic[type]+= count;
        if (user&&user.TryGetComponent(out PlayerController player)) AddBattleDataItem(player.PlayerIndex, "采集欧帕兹数量");
    }
    private void OnDatSwitch(bool isNoon)
    {
        //Debug.LogError("昼夜交替"+ isNoon);
        ADCont.Authorize(16, !isNoon);
        ADCont.Authorize(17, !isNoon);
        //应该加语音播报
    }

}

public struct WaveCreateParams
{
    public Vector3 center;
    public Vector3[] points;

    public bool extraWave;
    public float range;
    public float scale;
    public bool tip;

    public static WaveCreateParams Default => new WaveCreateParams {
        extraWave = false,
        range = 35,
        scale = 1,
        tip = true
    };

    public static WaveCreateParams Extra => new WaveCreateParams {
        extraWave = true,
        range = 35,
        scale = 0.5f,
        tip = true
    };

    public static WaveCreateParams Defensive => new WaveCreateParams {
        extraWave = true,
        range = 5,
        scale = 1,
        tip = true,
    };


}
public static class WaveUtil
{
    public static WaveCreateParams Set(this WaveCreateParams para, Vector3 center)
    {
        para.center = center;
        return para;
    }
    public static WaveCreateParams Set(this WaveCreateParams para, Vector3 center,Vector3[] points)
    {
        para.center = center;
        para.points = points;
        return para;
    }
    public static WaveCreateParams Scale(this WaveCreateParams para, float scale)
    {
        para.scale = scale;
        return para;
    }
}