using MessagePack;

namespace FPSGame.Net
{

/// <summary>
/// 登录请求：客户端 -> 服务器。
/// </summary>
[MessagePackObject]
public class LoginReqMsg
{
    [Key(0)]
    public string Account;

    [Key(1)]
    public string Password;
}

/// <summary>
/// 登录响应：服务器 -> 客户端。
/// </summary>
[MessagePackObject]
public class LoginRspMsg
{
    [Key(0)]
    public int ErrorCode; // 0 表示成功

    [Key(1)]
    public string Token;

    [Key(2)]
    public string UserName;
}
}
