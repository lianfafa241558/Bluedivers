using MessagePack;

namespace FPSGame.Net
{

// ============================================================
// 房间业务消息（房主权威 / 局域网联机）
// 通信模型：成员 --消息--> 房主，房主决定并转发/广播。
// 新增房间消息时在这里加 [MessagePackObject] 类，并在 CmdId 加常量。
// ============================================================

/// <summary>
/// 单个玩家信息（用于玩家列表同步）。
/// </summary>
[MessagePackObject]
public class PlayerInfo
{
    [Key(0)]
    public uint Sid;          // 房主分配/持有的会话ID
    [Key(1)]
    public string PlayerName; // 玩家名
    [Key(2)]
    public bool IsReady;      // 是否已准备
    [Key(3)]
    public bool IsHost;       // 是否是房主
}

/// <summary>
/// 加入房间请求：成员 -> 房主。
/// 成员在局域网发现房间并回连成功后，用它向房主申请进房。
/// </summary>
[MessagePackObject]
public class JoinRoomReq
{
    [Key(0)]
    public string PlayerName; // 成员想用的名字
    [Key(1)]
    public string Password;   // 房间密码（没有则为空）
}

/// <summary>
/// 加入房间响应：房主 -> 成员。
/// ErrorCode==0 表示成功，否则带失败原因。
/// </summary>
[MessagePackObject]
public class JoinRoomRsp
{
    [Key(0)]
    public int ErrorCode;     // 0=成功，其他见错误码
    [Key(1)]
    public string Reason;     // 失败原因（如"房间已满""密码错误"）
    [Key(2)]
    public PlayerInfo Self;   // 加入成功后，自己在这个房间里的信息
    [Key(3)]
    public PlayerInfo[] Players; // 当前房间里所有玩家（含自己）
}

/// <summary>
/// 离开房间通知：成员 -> 房主。
/// 成员退出时告知房主，房主再广播 PlayerListSync 给其余成员。
/// </summary>
[MessagePackObject]
public class LeaveRoomNtf
{
    [Key(0)]
    public uint Sid;          // 离开者的会话ID（房主用它识别是谁）
}

/// <summary>
/// 玩家列表同步：房主 -> 全体。
/// 每当有人加入/离开/改变准备状态时，房主把最新玩家列表广播给所有成员。
/// </summary>
[MessagePackObject]
public class PlayerListSync
{
    [Key(0)]
    public PlayerInfo[] Players;
}

/// <summary>
/// 准备状态：成员 -> 房主。
/// 成员点击"准备/取消准备"时上报，房主汇总后广播 PlayerListSync。
/// </summary>
[MessagePackObject]
public class ReadyState
{
    [Key(0)]
    public bool IsReady;      // true=准备，false=取消准备
}

/// <summary>
/// 开始游戏通知：房主 -> 全体。
/// 房主在"玩家齐了且都准备"时广播，所有成员据此加载战斗场景。
/// </summary>
[MessagePackObject]
public class StartGameNtf
{
    [Key(0)]
    public string MapName;    // 要加载的地图
}
}
