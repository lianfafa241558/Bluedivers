using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Game;
using FPSGame.Utils;

namespace FPSGame.Managers
{


/// <summary>
/// 房间与玩家数据管理。
/// </summary>
[AddComponentMenu("管理/房间管理")]
public class RoomManager : Singleton<RoomManager> ,I_GlobaManager, FPSGame.GameContract.IRoomService
{
    public PlayerData Self { get; private set; }
    public int SelfIndex => Self.index;
    public PlayerData Master =>players.Find(item=>!item.isEmpty&&!item.isBot);


    public List<PlayerData> players = new List<PlayerData>(Constants.MaxPlayer);
    private ArchivesData_SO arch;

    public int IdToIndex(int id) => players.FindIndex(item=>item.id==id);

    public bool AddPlayer(PlayerData player)
    {
        if (players.Count >= Constants.MaxPlayer) return false;
        players.Add(player);
        //同步随机种子
        RandomUtils.InitRandom();
        //TODO:玩家加入游戏(非自己)
        return true;
    }

    public void LeavePlayer(int id)
    {
        players.RemoveAll(item=>item.id==id);
        //TODO:玩家离开游戏
    }
    public void JoinPlayer(List<PlayerData> players,int seed)
    {
        this.players = players;
        Self = players.Find(item =>item.id==arch.UID);
        RandomUtils.InitRandom(seed);
        //TODO:玩家加入游戏(自己)
    }

    public bool IsSingle => players.FindAll(item=>!item.isBot).Count==1;

    // FPSGame.GameContract.IRoomService：只给下层两个"整数投影"，避免契约层暴露 PlayerData（见 IRoomService.cs）
    int FPSGame.GameContract.IRoomService.MasterIndex => Master != null ? Master.index : 0;
    int FPSGame.GameContract.IRoomService.PlayerCount => players.Count;

    public void Init()
    {
        Awake();
        FPSGame.GameContract.ServiceLocator.Room = this;//注册房间服务：供玩法层（AI/任务）等下层访问（见 ServiceLocator.cs）
        arch = ArchiveSvc.Archive;
        arch.GetRoleLevel(arch.lastSelectRole, out int level, out var exp);
        players.Add(new() {
            name = arch.playerName,
            id = arch.UID,
            index = 0,
            roleName = arch.lastSelectRole,
            roleLevel = level,
            roleExp = exp,
            airdrop = new int[4],
            weapons = arch.GetWeaponSelect(arch.lastSelectRole),
            Upgrades = arch.GetWeaponUpgrade(arch.lastSelectRole),
            boosterId = 0,
        });
        Self = players[0];
    }
    public void UnInit()
    {
        
    }
}

[System.Serializable]
public class PlayerData
{
    public string name;
    public bool isBot = false;
    public int id = 0;
    public int index = 0;//序号
    public string roleName;
    public int roleLevel;
    public float roleExp;
    public int[] airdrop;
    public int[] weapons;
    public int[][] Upgrades;
    /// <summary>已选择的全队强化 ID（0 表示未选择）</summary>
    public int boosterId;
    public bool isEmpty;
    public bool IsVaild() => id != 0;
}
}
