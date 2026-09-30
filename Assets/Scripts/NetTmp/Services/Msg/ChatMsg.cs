using MessagePack;

namespace FPSGame.Net
{

/// <summary>
/// 聊天发送：客户端 -> 服务器。
/// </summary>
[MessagePackObject]
public class ChatSendMsg
{
    [Key(0)]
    public string Content;
}

/// <summary>
/// 聊天广播：服务器 -> 所有客户端。
/// </summary>
[MessagePackObject]
public class ChatBroadcastMsg
{
    [Key(0)]
    public int FromUserId;

    [Key(1)]
    public string FromName;

    [Key(2)]
    public string Content;
}
}
