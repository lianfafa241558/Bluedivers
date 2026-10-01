using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using UnityEngine;
using FPSGame.Game;
using FPSGame.Utils;
using FPSGame.GameData;

namespace FPSGame.Managers
{


/// <summary>
/// 房间与玩家数据管理。
/// </summary>
public class RoomManager : Singleton<RoomManager> ,I_GlobaManager
{
    public PlayerData Self { get; private set; }
    public int SelfIndex => Self.index;
    public PlayerData Master =>players.Find(item=>!item.isEmpty&&!item.isBot);


    /// <summary>
    /// ⚠ **任何修改本列表的地方都必须调用 <see cref="SyncRoomState"/>**（目前 4 处：
    /// <c>Init</c> / <c>AddPlayer</c> / <c>LeavePlayer</c> / <c>JoinPlayer</c>）——
    /// 玩法层读的是数据自持快照 <see cref="FPSGame.Data.RoomState"/>，漏同步就会读到过期值。
    /// </summary>
    public List<PlayerData> players = new List<PlayerData>(Constants.MaxPlayer);
    private ArchivesData_SO arch;

    public int IdToIndex(int id) => players.FindIndex(item=>item.id==id);

    /// <summary>
    /// 把"玩家数 / 房主序号"发布到数据自持点（2026-10-01 取代 <c>ServiceLocator.Room</c> 槽）。
    ///
    /// <para>▍为什么需要显式同步：旧接口 <c>IRoomService</c> 是"每次读时现算"（<c>players.Count</c> 等），
    /// 改为数据自持后是**快照** ⇒ 必须在所有变更点发布，否则玩法层读到过期值。</para>
    /// </summary>
    private void SyncRoomState()
    {
        FPSGame.Data.RoomState.PlayerCount = players.Count;
        var master = Master;
        FPSGame.Data.RoomState.MasterIndex = master != null ? master.index : 0;
    }

    public bool AddPlayer(PlayerData player)
    {
        if (players.Count >= Constants.MaxPlayer) return false;
        players.Add(player);
        //同步随机种子
        RandomUtils.InitRandom();
        SyncRoomState();//⚠ 变更点：发布玩家数/房主序号
        //TODO:玩家加入游戏(非自己)
        return true;
    }

    public void LeavePlayer(int id)
    {
        players.RemoveAll(item=>item.id==id);
        SyncRoomState();//⚠ 变更点：发布玩家数/房主序号
        //TODO:玩家离开游戏
    }
    public void JoinPlayer(List<PlayerData> players,int seed)
    {
        this.players = players;
        Self = players.Find(item =>item.id==arch.UID);
        RandomUtils.InitRandom(seed);
        SyncRoomState();//⚠ 变更点：发布玩家数/房主序号
        //TODO:玩家加入游戏(自己)
    }

    public bool IsSingle => players.FindAll(item=>!item.isBot).Count==1;

    public void Init()
    {
        Awake();
        arch = ArchivesData_SO.Current;
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
        SyncRoomState();//⚠ 变更点：发布玩家数/房主序号
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
