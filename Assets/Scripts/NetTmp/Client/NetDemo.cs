using KCPNet;
using UnityEngine;

namespace FPSGame.Net
{

/// <summary>
/// 【完整使用示例】演示基于 KCP + MessagePack 的新网络框架怎么用。
/// 挂到任意 GameObject 上即可测试（需要先有可用的服务器，或理解流程即可）。
///
/// ▍三步使用法（对应旧项目的写法）：
///   1. 注册：告诉 MessageCenter "收到某条消息时调用哪个函数"
///   2. 发送：构造消息对象 → MessageCenter.Pack 打包 → NetSvc.SendMsg 发送
///   3. 接收：NetSvc 收到 → MessageCenter.Dispatch 自动调注册的函数
///
/// ▍对比旧项目写法：
///   旧：NetSvc 里写 switch case → 各 Sys 写 NtfXxx 方法
///   新：这里直接 Register 绑定，各模块自己处理，NetSvc 不用改
/// </summary>
public class NetDemo : MonoBehaviour
{
    private float _pingTimer;
    private uint _pingId;

    // ---- 简易 UI 显示状态 ----
    private string _loginStatus = "未登录";
    private string _pingStatus = "未发送";
    private readonly System.Collections.Generic.List<string> _chatLog = new System.Collections.Generic.List<string>();
    private long _lastDelay = -1;

    private void Awake()
    {
        // 第一步：注册消息处理器（在场景启动时注册一次）
        InitMessageHandlers();
    }

    private void Update()
    {
        // 每 3 秒发一次 Ping（演示定时消息）
        _pingTimer += Time.deltaTime;
        if (_pingTimer >= 3f)
        {
            _pingTimer = 0f;
            SendPing();
        }

        // 演示按键触发：按 L 登录，按 B 发聊天
        // 注：不用 C，因为 LanRoomDemo 里 C 已用于"连接房间"，避免同场景按键冲突。
        if (Input.GetKeyDown(KeyCode.L)) SendLogin();
        if (Input.GetKeyDown(KeyCode.B)) SendChat();
    }

    // ==================== 第一步：注册处理器 ====================
    private void InitMessageHandlers()
    {
        // 泛型绑定：收到 LoginRspMsg 时调用 OnLoginRsp
        MessageCenter.Register<LoginRspMsg>(CmdId.LoginRsp, OnLoginRsp);

        // 收到 PingRspMsg 时调用 OnPingRsp
        MessageCenter.Register<PingRspMsg>(CmdId.PingRsp, OnPingRsp);

        // 收到 ChatBroadcastMsg 时调用 OnChatBroadcast
        MessageCenter.Register<ChatBroadcastMsg>(CmdId.ChatBroadcast, OnChatBroadcast);

        Debug.Log("[NetDemo] 消息处理器注册完成");
    }

    // ==================== 第二步：发送消息 ====================
    private void SendLogin()
    {
        if (!EnsureConnected("登录")) return;

        var req = new LoginReqMsg { Account = "admin", Password = "123456" };

        // 打包：MessageCenter.Pack(cmdId, 消息) → 生成 NetMessage 信封
        // 发送：NetSvc.SendMsg(信封) → 交给 KCP 网络层
        NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.LoginReq, req));

        Debug.Log("[NetDemo] 已发送登录请求");
    }

    private void SendPing()
    {
        if (!EnsureConnected("Ping")) return;

        _pingId++;
        var req = new PingReqMsg
        {
            Id = _pingId,
            SendTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PingReq, req));
    }

    private void SendChat()
    {
        if (!EnsureConnected("聊天")) return;

        var msg = new ChatSendMsg { Content = "大家好，我是新手！" };
        NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.ChatSend, msg));
    }

    /// <summary>
    /// 【连接检查】NetSvc 默认不自动连接（InitSvc(false)），
    /// 直连服务器模式需要先连上再发消息，否则会 NRE/静默失败。
    /// </summary>
    private bool EnsureConnected(string action)
    {
        if (NetSvc.Instance != null && NetSvc.Instance.IsConnected) return true;

        _pingStatus = $"未连接服务器，无法{action}。请确认服务器已开并调用 NetSvc.ConnectDefaultServer()";
        Debug.LogWarning($"[NetDemo] 未连接服务器，无法{action}");
        return false;
    }

    // ==================== 第三步：处理收到的消息 ====================
    private void OnLoginRsp(LoginRspMsg rsp)
    {
        if (rsp.ErrorCode == 0)
        {
            _loginStatus = $"登录成功！用户:{rsp.UserName} Token:{rsp.Token}";
            Debug.Log($"[NetDemo] 登录成功！用户名:{rsp.UserName} Token:{rsp.Token}");
        }
        else
        {
            _loginStatus = $"登录失败 错误码:{rsp.ErrorCode}";
            Debug.LogWarning($"[NetDemo] 登录失败 错误码:{rsp.ErrorCode}");
        }
    }

    private void OnPingRsp(PingRspMsg rsp)
    {
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long delay = now - rsp.ServerTime;
        _lastDelay = delay; // 供 UI 显示，也用 < 0 判断"从未收到过响应"
        _pingStatus = $"已收到响应 Id:{rsp.Id} 延迟约:{delay}ms";
        Debug.Log($"[NetDemo] 收到 Ping 响应 Id:{rsp.Id} 延迟约:{delay}ms");
    }

    private void OnChatBroadcast(ChatBroadcastMsg msg)
    {
        string line = $"{msg.FromName}({msg.FromUserId}): {msg.Content}";
        _chatLog.Add(line);
        if (_chatLog.Count > 20) _chatLog.RemoveAt(0); // 只保留最近 20 条
        Debug.Log($"[NetDemo] {msg.FromName}({msg.FromUserId}): {msg.Content}");
    }

    // ==================== 简易 UI 显示 ====================
    // 直接用 OnGUI 画一个半透明面板，实时显示各阶段是否成功，方便观察。
    private void OnGUI()
    {
        // 顶部操作提示
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10, 10, 420, Screen.height - 20));
        GUILayout.Label("<size=16><b>【NetDemo 网络联调】</b></size>");
        GUILayout.Label("按键：L=登录  B=发聊天  (每3秒自动Ping)");
        GUILayout.Space(6);

        // 状态面板
        GUI.backgroundColor = new Color(0, 0, 0, 0.6f);
        GUILayout.BeginVertical("box");
        bool connected = NetSvc.Instance != null && NetSvc.Instance.IsConnected;
        GUILayout.Label($"<b>连接：</b>{((connected ? "已连接" : "未连接") + (_lastDelay < 0 ? "" : $" 最近延迟 {_lastDelay}ms"))}");
        GUILayout.Label($"<b>登录：</b>{_loginStatus}");
        GUILayout.Label($"<b>心跳：</b>{_pingStatus}");
        if (!connected)
        {
            GUILayout.Label("<color=orange>提示：NetSvc 默认不自动连接，请先调用 NetSvc.ConnectDefaultServer() 连上服务器</color>");
        }
        GUILayout.EndVertical();
        GUILayout.Space(6);

        // 聊天记录面板
        GUILayout.Label("<b>聊天记录：</b>");
        GUILayout.BeginVertical("box");
        foreach (var line in _chatLog)
        {
            GUILayout.Label(line);
        }
        if (_chatLog.Count == 0)
        {
            GUILayout.Label("（暂无，按 B 发一条试试）");
        }
        GUILayout.EndVertical();

        GUILayout.EndArea();
    }

    // 销毁时反注册，避免残留
    private void OnDestroy()
    {
        MessageCenter.Unregister(CmdId.LoginRsp);
        MessageCenter.Unregister(CmdId.PingRsp);
        MessageCenter.Unregister(CmdId.ChatBroadcast);
    }
}
}
