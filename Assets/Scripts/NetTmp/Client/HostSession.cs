using KCPNet;
using UnityEngine;

namespace FPSGame.Net
{

/// <summary>
/// 【房主端会话】代表"房主 与 某一个成员之间的一条连接"。
/// 与 ClientSession 完全对称，但角色是"服务器/房主侧"：
///   - 房主用 KCPNet.StartAsServer 开监听，每个连进来的成员都会创建一个 HostSession。
///   - 房主通过这个会话向对应成员发消息（成员收发用 ClientSession）。
///
/// ▍理解"房主权威"下的会话模型（新手重点）：
///   - 房主：KCPNet&lt;HostSession, NetMessage&gt; 以服务器身份运行，维护"多个成员会话"。
///   - 成员：KCPNet&lt;ClientSession, NetMessage&gt; 以客户端身份运行，只有"一条会话"。
///   所以 HostSession 是"一个成员 = 一条会话"，成员列表就是"会话列表"。
///
/// ▍注意：房主不是也用 KCPNet&lt;ClientSession&gt; 连自己。
///   房主既开服务器(StartAsServer)，通常也维护一个本地会话用来跑游戏逻辑；
///   但网络传输上，房主就是"服务器"，成员是"客户端"。
/// </summary>
public class HostSession : KCPSession<NetMessage>
{
    /// <summary>
    /// 【序列化器】与 ClientSession 相同：用 MessagePack 把 NetMessage 和字节互相转换。
    /// </summary>
    protected override IKCPMsgSerializer Serializer => NetMessageSerializer.Instance;

    /// <summary>（可选）给每个成员记录一个名字/玩家ID，方便房主管理，不是必须。</summary>
    public string PlayerName = "";

    /// <summary>
    /// 对端地址（<c>ip:port</c>），用于排查"**谁在反复握手**"（2026-10-09：只有两个客户端却出现一串从未入房的会话）。
    /// <para>▍为什么用反射：基类只有 <c>private IPEndPoint m_remotePoint</c>（另有一个名字很怪的属性 <c>mremotePoint</c>）；
    /// 拿不到就返回 <c>?</c>，不影响任何逻辑。</para>
    /// </summary>
    public string PeerAddress
    {
        get
        {
            try
            {
                var f = typeof(KCPSession<NetMessage>).GetField("m_remotePoint",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (f != null)
                {
                    var ep = f.GetValue(this) as System.Net.IPEndPoint;
                    if (ep != null) return ep.ToString();
                }
            }
            catch { }
            return "?";
        }
    }

    /// <summary>
    /// 【连接成功回调】某个成员完成握手接入时，KCPNet 调用此方法。
    /// <para>⚠ 它是**握手成功**才会来的。实测（2026-10-09）：**同一条 socket 会被库反复重握手**，
    /// 服务端因此建出一串 sid（房主 console 看起来像"一直有人进进出出"，其实只有一个客机）。
    /// 所以这里对日志**限流**（前 3 次 + 之后每 10s 一次，带累计次数），既留证据又不刷屏。</para>
    /// </summary>
    protected override void OnConnected()
    {
        int n = ++s_connectCount;
        bool firstFew = n <= 3;
        bool periodic = (System.DateTime.UtcNow - s_lastLogUtc).TotalSeconds >= 10.0;
        if (!firstFew && !periodic) return;

        s_lastLogUtc = System.DateTime.UtcNow;
        Debug.Log($"[HostSession] 新成员接入, sid={GetSessionID()} 对端={PeerAddress}（累计 {n} 次）{System.DateTime.Now:HH:mm:ss.fff}");
    }

    /// <summary>
    /// 【断开回调】某个成员断开时调用（同样限流，见 <see cref="OnConnected"/>）。
    /// </summary>
    protected override void OnDisConnected()
    {
        int n = ++s_disconnectCount;
        if (n > 3 && (System.DateTime.UtcNow - s_lastLogUtc).TotalSeconds < 10.0) return;

        s_lastLogUtc = System.DateTime.UtcNow;
        Debug.Log($"[HostSession] 成员断开, sid={GetSessionID()} 对端={PeerAddress}（累计 {n} 次）");
    }

    // ⚠ 这两个回调跑在库的线程池线程上 ⇒ 不能碰 Unity API（所以用 DateTime 而不是 Time.unscaledTime）。
    static int s_connectCount;
    static int s_disconnectCount;
    static System.DateTime s_lastLogUtc = System.DateTime.MinValue;

    /// <summary>
    /// 【收到消息回调】某个成员发来一条消息时，KCPNet 调用此方法。
    /// 同样运行在网络线程，先丢进队列等主线程处理。
    /// 注意：必须把"来源 sid"一起传给队列，房主才知道这条消息来自哪个成员，
    /// 才能做定向回复、更新成员列表。这是房主权威的核心前提。
    /// </summary>
    protected override void OnReciveMsg(NetMessage msg)
    {
        // 带上本会话的 sid 一起入队（成员端不用带，因为只有一条连接）
        NetInbox.Enqueue(msg, GetSessionID());
    }

    /// <summary>
    /// 【每帧回调】目前不需要，留空。
    /// </summary>
    protected override void OnUpdate(System.DateTime now)
    {
    }
}
}
