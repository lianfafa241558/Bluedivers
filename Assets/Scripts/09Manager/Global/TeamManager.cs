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
public class TeamManager : Singleton<TeamManager> ,I_GlobaManager
{
    public PlayerData Self { get; private set; }
    public int SelfIndex => Self.index;
    public PlayerData Master =>players.Find(item=>!item.isEmpty&&!item.isBot);


    /// <summary>
    /// ⚠ **任何修改本列表的地方都必须调用 <see cref="SyncTeamState"/>**（目前 4 处：
    /// <c>Init</c> / <c>AddPlayer</c> / <c>LeavePlayer</c> / <c>JoinPlayer</c>）——
    /// 玩法层读的是数据自持快照 <see cref="FPSGame.Data.TeamState"/>，漏同步就会读到过期值。
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
    private void SyncTeamState()
    {
        FPSGame.Data.TeamState.PlayerCount = players.Count;
        var master = Master;
        FPSGame.Data.TeamState.MasterIndex = master != null ? master.index : 0;
    }

    public bool AddPlayer(PlayerData player)
    {
        if (player == null) return false;
        if (players.Count >= Constants.MaxPlayer) return false;
        player.index = players.Count;   // index 恒等于下标（见 Reindex）
        players.Add(player);
        SyncTeamState();//⚠ 变更点：发布玩家数/房主序号
        //TODO:玩家加入游戏(非自己)
        return true;
    }

    /// <summary>
    /// 【联机同步用】按 <c>id</c> 增或改。
    /// ⚠ 不 remove 再 append：`index` 是 UI 的下标真理（`ArmamentWnd`/`GameEndWnd` 全靠 <c>players[i]</c>），
    /// 已存在就**原地覆盖**，不存在才追加，避免别人的 index 漂移。
    /// </summary>
    public bool UpsertPlayer(PlayerData player)
    {
        if (player == null) return false;

        int at = players.FindIndex(item => item != null && item.id == player.id);
        if (at >= 0)
        {
            player.index = at;
            players[at] = player;
        }
        else
        {
            if (players.Count >= Constants.MaxPlayer) return false;
            player.index = players.Count;
            players.Add(player);
        }
        SyncTeamState();
        return true;
    }

    public void LeavePlayer(int id) => RemoveById(id);

    /// <summary>【联机同步用】按 id 移除并重新编号（别人离开后本地下标会整体前移）。</summary>
    public void RemoveById(int id)
    {
        if (players.RemoveAll(item => item != null && item.id == id) > 0)
        {
            Reindex();
            SyncTeamState();//⚠ 变更点：发布玩家数/房主序号
        }
        //TODO:玩家离开游戏
    }

    /// <summary>
    /// 让 <c>index</c> 与列表下标一致，并重新指向 <see cref="Self"/>。
    /// ▍为什么必须有：任何一次增删之后若不重排，UI（按下标取）与 <c>Self.index</c> 就会各说各话。
    /// </summary>
    private void Reindex()
    {
        for (int i = 0; i < players.Count; ++i)
        {
            if (players[i] != null) players[i].index = i;
        }
        if (arch != null) Self = players.Find(item => item != null && item.id == arch.UID);
        if (Self == null && players.Count > 0) Self = players[0];   // 兜底：Self 不悬空
    }

    /// <summary>
    /// 设定本局随机种子（全队必须一致，否则每人一条随机线）。
    /// <para>⚠ 联机时由房主决定并通过 <c>TaskConfirmNtf.Seed</c> 下发；<paramref name="seed"/> = 0 表示未指定
    /// （单机 / 旧版房主）⇒ 退回本地随机，与改造前行为一致。</para>
    /// </summary>
    public void SetSeed(int seed)
    {
        if (seed == 0) RandomUtils.InitRandom();
        else RandomUtils.InitRandom(seed);
    }

    /// <summary>【成员侧】整表替换（房主下发的本局玩家表 + 统一种子）。</summary>
    public void JoinPlayer(List<PlayerData> list, int seed)
    {
        if (list != null && list.Count > 0) players = list;
        Reindex();
        SetSeed(seed);
        SyncTeamState();//⚠ 变更点：发布玩家数/房主序号
        //TODO:玩家加入游戏(自己)
    }

    /// <summary>
    /// 【联机同步用】整表替换但**不碰随机种子**（玩家进出/资料变更时的高频路径）。
    /// ▍为什么要和 <see cref="JoinPlayer"/> 分开：种子只在"开局"定一次，
    /// 名单每变一次就重随机，会让各端的随机线越走越远。
    /// </summary>
    public void ReplacePlayers(List<PlayerData> list)
    {
        if (list == null || list.Count == 0) return;
        players = list;
        Reindex();
        SyncTeamState();//⚠ 变更点：发布玩家数/房主序号
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
        SyncTeamState();//⚠ 变更点：发布玩家数/房主序号
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
    /// <summary>是否已就绪（由 09 的桥按名单权威写入）。⚠ 窗口要**按它渲染**、不能只靠"变化事件"：
    /// 事件只在值变化时推一次，而窗口可能是在变化之后才打开的（2026-10-07 实测：客机看不到主机的就绪）。</summary>
    public bool isReady;
    /// <summary>已选择的全队强化 ID（0 表示未选择）</summary>
    public int boosterId;
    public bool isEmpty;
    public bool IsVaild() => id != 0;
}
}
