using System.Collections.Generic;
using Core;
using FpsGame.MapUtils;
using GameContract;
using PEMaths;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

public class BattleManager : Singleton<BattleManager>
{
    public bool IsStartBattle;

    public AirdropController ADCont;
    public BattleRoleManager BRCont;
    public WaveManager WaveCont;
    public MissionController MissionCont;
    public PatrolContriller PatrolCont;


    private UnitQueryGrid unitQueryGrid;
    private MapRoot mapRoot;

    public System.Random BattleRandom { get;private set; }

    void Start()
    {

        var pos = mapRoot.rect;
        unitQueryGrid = new(new((PEVector2)pos.center, pos.size.x / 2, pos.size.z / 2), 30);
        

        GlobalEventManager.OnUnitPosChange += OnUnitPosChange;
        GlobalEventManager.OnEnemyCreate += OnEnemyCreate;
        GlobalEventManager.OnEnemyDead += OnEnemyDeath;
        GlobalEventManager.OnPlayerCreate += OnPlayerCreate;
        GlobalEventManager.OnPlayerDead += OnPlayerDeath;
        GlobalEventManager.OnOOPartCollect += OOPartCollect;
        GlobalEventManager.OnDaySwitch += OnDatSwitch;
    }
    private void OnDestroy()
    {
        GlobalEventManager.OnUnitPosChange -= OnUnitPosChange;
        GlobalEventManager.OnEnemyCreate -= OnEnemyCreate;
        GlobalEventManager.OnEnemyDead -= OnEnemyDeath;
        GlobalEventManager.OnPlayerCreate -= OnPlayerCreate;
        GlobalEventManager.OnPlayerDead -= OnPlayerDeath;
        GlobalEventManager.OnOOPartCollect -= OOPartCollect;
        GlobalEventManager.OnDaySwitch -= OnDatSwitch;
    }


    public static void Creat()
    {
        var manager = new GameObject("BattleManager").AddComponent<BattleManager>();
        manager.BattleRandom = new(TaskManager.Instance.nowTask.taskCfg.seed);
        
        manager.InitTerrain();

        manager.MissionCont = new GameObject("MissionController").AddComponent<MissionController>();
        manager.MissionCont.transform.SetParent(manager.transform);
        manager.ADCont = new GameObject("AirdropController").AddComponent<AirdropController>();
        manager.ADCont.Init();
        manager.ADCont.transform.SetParent(manager.transform);
        manager.BRCont = new GameObject("BattleRoleCont").AddComponent<BattleRoleManager>();
        manager.BRCont.transform.SetParent(manager.transform);
        manager.WaveCont = new GameObject("WaveCont").AddComponent<WaveManager>();
        manager.WaveCont.transform.SetParent(manager.transform);
        manager.PatrolCont = new GameObject("PatrolCont").AddComponent<PatrolContriller>();
        manager.PatrolCont.transform.SetParent(manager.transform);

        GameRoot.CreateTimer(() => WndManager.Instance.CreatNotice("Yuuka2", "MissionStart"), 8);

    }

    void InitTerrain()
    {
        Transform transMapRoot = GameObject.FindGameObjectWithTag("MapRoot").transform;
        mapRoot = transMapRoot.GetComponent<MapRoot>();
       
        var terrain=TerrainUtils.Main = mapRoot.terrain;
        var cfg=TaskManager.Instance.nowTask;
        var terrainData = terrain.terrainData;

        Debug.LogWarning("地图尺寸"+ cfg.MainCfg.sizeType+" 地图大小"+ cfg.MapSize);
        var mapRes= cfg.MainCfg.sizeType switch {
            SizeType.Small => 256,
            SizeType.Medium => 512,
            SizeType.Large => 512,
            _ => 256 
        };
        terrainData.size = new(cfg.MapSize, cfg.MapHeight, cfg.MapSize);
        terrainData.heightmapResolution = mapRes*2 + 1;
        terrainData.alphamapResolution = mapRes*2;

        mapRoot.Init();
        mapRoot.GetComponent<GenerateNoiseTerrain>().ApplyFractalNoiseToTerrain();

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

    public GameObject CreatUnit(UnitTier tier,Vector3 pos,float range,bool NoVfx=true)
    {
       return WaveCont.CreatUnit(tier, pos,range, NoVfx);
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
        IsStartBattle = true;
        GameRoot.GameState = GameStateEnum.Game;
        GameRoot.WindowState = WindowStateEnum.Game;

    }


    public void ReleaseAirdrop(Vector3 point,int id, System.Action<GameObject> action=default)
    {
        ReleaseAirdrop(point, RandomUtils.Range(0, 360), id, action);
    }
    public void ReleaseAirdrop(Vector3 point,float angle, int id, System.Action<GameObject> action = default)
    {
        var beacon = VFXManager.Creat(ResManager.Instance.LoadObject<GameObject>("Prefabs/Airdrop/VFX_AirdropPoint"), point, Quaternion.Euler(0,angle, 0), null);
        beacon.GetComponent<VFXAirdropEffect>()?.TmpAirdrop(point, ResManager.Instance.GetAirdrop(id), action);
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
            ResManager.Instance.AsyncLoadScene("GameEnd", () => {
                WndManager.Instance.movieWnd.SetWndState(false);
                GameRoot.GameState = GameStateEnum.GameEnd;
                GameRoot.WindowState = WindowStateEnum.UI;
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