using MessagePack;

namespace FPSGame.Net
{

/// <summary>
/// 心跳请求：客户端定时发给服务器，用于测量延迟和保活连接。
/// </summary>
[MessagePackObject]
public class PingReqMsg
{
    [Key(0)]
    public uint Id;

    [Key(1)]
    public long SendTime;
}

/// <summary>
/// 心跳响应：服务器回应客户端。
/// </summary>
[MessagePackObject]
public class PingRspMsg
{
    [Key(0)]
    public uint Id;

    [Key(1)]
    public long ServerTime;
}
}
