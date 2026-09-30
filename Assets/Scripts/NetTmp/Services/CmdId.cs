
namespace FPSGame.Net
{

/// <summary>
/// 命令号常量表。
/// 作用：为每条网络消息分配一个唯一的整数编号，用于在信封中区分消息类型。
/// 约定：客户端与服务器必须使用同一份 CmdId 表，编号不能重复、不能随意修改。
/// 分段规则：按业务模块分段，方便扩展和排查。
/// 提示：本表为示例，实际业务可在此基础上扩展。
/// </summary>
public static class CmdId
{
    // ===== 通用 =====
    public const int PingReq = 1001;
    public const int PingRsp = 1002;

    // ===== 账号模块 =====
    public const int LoginReq = 2001;
    public const int LoginRsp = 2002;

    // ===== 聊天模块 =====
    public const int ChatSend = 3001; // 客户端 -> 服务器
    public const int ChatBroadcast = 3002; // 服务器 -> 所有客户端

    // ===== 房间模块（房主权威 / 局域网联机）=====
    public const int JoinRoomReq = 4001;   // 成员 -> 房主：请求加入房间
    public const int JoinRoomRsp = 4002;   // 房主 -> 成员：加入结果（成功/失败+原因）
    public const int LeaveRoomNtf = 4003;  // 成员 -> 房主：离开房间
    public const int PlayerListSync = 4004; // 房主 -> 全体：成员列表同步（加入/离开/变更时）
    public const int ReadyState = 4005;    // 成员 -> 房主：准备状态切换
    public const int StartGameNtf = 4006;  // 房主 -> 全体：开始游戏
}

}
