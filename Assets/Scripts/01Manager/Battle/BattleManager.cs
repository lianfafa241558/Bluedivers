using System.Collections.Generic;
using Core;
using GameContract;
using PEMaths;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

public class BattleManager : Singleton<BattleManager>
{
    public AirdropController ADCont;
    public BattleRoleManager BRCont;
    public WaveManager WaveCont;
    public MissionController MissionCont;

    private UnitQueryGrid unitQueryGrid;
    private MapRoot mapRoot;

    public System.Random BattleRandom { get;private set; }

    void Start()
    {
        Transform transMapRoot = GameObject.FindGameObjectWithTag("MapRoot").transform;
        mapRoot = transMapRoot.GetComponent<MapRoot>();
        var pos = mapRoot.rect;
        unitQueryGrid = new(new((PEVector2)pos.center, pos.size.x / 2, pos.size.z / 2), 30);

        GlobalEventManager.OnUnitPosChange += OnUnitPosChange;
        GlobalEventManager.OnEnemyCreate += OnEnemyCreate;
        GlobalEventManager.OnEnemyDead += OnEnemyDeath;
        GlobalEventManager.OnPlayerCreate += OnPlayerCreate;
        GlobalEventManager.OnPlayerDead += OnPlayerDeath;
        GlobalEventManager.OnOOPartCollect += OOPartCollect;
        var debugger = mapRoot.transform.GetComponent<UnitQueryGridDebugger>();
        if (debugger.IsValid())
        {
            debugger.grid = unitQueryGrid;
        }
    }
    private void OnDestroy()
    {
        GlobalEventManager.OnUnitPosChange -= OnUnitPosChange;
        GlobalEventManager.OnEnemyCreate -= OnEnemyCreate;
        GlobalEventManager.OnEnemyDead -= OnEnemyDeath;
        GlobalEventManager.OnPlayerCreate -= OnPlayerCreate;
        GlobalEventManager.OnPlayerDead -= OnPlayerDeath;
        GlobalEventManager.OnOOPartCollect -= OOPartCollect;
    }

    void Update()
    {

    }

    public static void Creat()
    {
        var manager = new GameObject("BattleManager").AddComponent<BattleManager>();
        manager.BattleRandom = new(TaskManager.Instance.nowTaskCfg.nowTask.seed);

        manager.MissionCont = new GameObject("MissionController").AddComponent<MissionController>();
        manager.MissionCont.transform.SetParent(manager.transform);
        manager.ADCont = new GameObject("AirdropController").AddComponent<AirdropController>();
        manager.ADCont.Init();
        manager.ADCont.transform.SetParent(manager.transform);
        manager.BRCont = new GameObject("BattleRoleManager").AddComponent<BattleRoleManager>();
        manager.BRCont.transform.SetParent(manager.transform);
        manager.WaveCont = new GameObject("WaveManager").AddComponent<WaveManager>();
        manager.WaveCont.transform.SetParent(manager.transform);
    }

    public List<I_Actor> FindUnits(IPERange range, TargetCfg targetCfg, System.Func<I_Actor, bool> customFilter = null)
    {
        return new List<I_Actor>(unitQueryGrid.QueryUnits(range, targetCfg, customFilter));
    }
    public List<I_Actor> FindUnits(TargetCfg targetCfg, System.Func<I_Actor, bool> customFilter = null)
    {
        return new List<I_Actor>(unitQueryGrid.QueryUnits(targetCfg, customFilter));
    }

    public bool CreatWave(Vector3 point, bool extraWave) => WaveCont.CreatWave(point, extraWave);
    
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

    public float GroundHeight(Vector2 worldPos)
    {
       return mapRoot.GroundHeight(worldPos);
    }


    /// <summary>
    /// 设置弹坑
    /// </summary>
    public void TryCreateCrater(Collider collider,Vector3 worldPosition, float radius, float depth)
    {
        if (collider.IsValid() && collider.gameObject == mapRoot.terrain.gameObject)
        {
            mapRoot.CreateCrater(worldPosition, radius/2, radius, depth,false);
        }
    }

    /// <summary>
    /// 设置弹坑
    /// </summary>
    public void TryCreateCrater(Vector3 worldPosition, float innerRadius, float outerRadius, float depth)
    {
        mapRoot.CreateCrater(worldPosition, innerRadius, outerRadius, depth,true);
    }


    public void ReleaseAirdrop(Vector3 point,int id, System.Action<GameObject> action=default)
    {
        var beacon = VFXManager.Creat(ResManager.Instance.LoadObject<GameObject>("Prefabs/Airdrop/VFX_AirdropPoint"), point,Quaternion.Euler(0,RandomUtils.Range(0,360),0), null);
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
        if (result != GameResult.Unknow) TaskManager.Instance.nowTaskCfg.result = result;
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
        var dic = TaskManager.Instance.nowTaskCfg.collectProperty;
        if (!dic.TryAdd(type, count)) dic[type]+= count;
        if (user&&user.TryGetComponent(out PlayerController player)) AddBattleDataItem(player.PlayerIndex, "采集欧帕兹数量");
    }

}
