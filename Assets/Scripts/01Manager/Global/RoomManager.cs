using System.Collections.Generic;
using Core.Interface;
using GameContract;
using Unity.BaseTool;

public class RoomManager : Singleton<RoomManager> ,I_GlobaManager
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

    public void Init()
    {
        Awake();
        arch = GameRoot.Archive;
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
    public bool isEmpty;
    public bool isVaild() => id != 0;
}