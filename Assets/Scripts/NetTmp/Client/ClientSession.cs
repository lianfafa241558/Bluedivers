using KCPNet;
using UnityEngine;

namespace FPSGame.Net
{

/// <summary>
/// 【客户端会话】代表"客户端到服务器的一条网络连接"。
/// 完全对应旧项目的 ClientSession.cs，作用是：
///   KCPNet 网络库收到数据后，会回调这个类的方法，我们在这里接入自己的逻辑。
///
/// ▍理解 KCPNet 的工作方式（新手重点）：
///   KCPNet 库是"别人写好的网络框架"，它负责：
///     - 底层 UDP 收发
///     - 可靠传输（丢包重传、乱序重组）——这就是 KCP 协议的意义
///     - 维护一条连接（Session）
///   但 KCPNet 不知道你的游戏逻辑，所以它留了几个"钩子方法"（虚方法），
///   让你继承 KCPSession 并重写，从而在网络事件发生时执行你的代码。
///
/// ▍泛型参数说明：
///   KCPSession<T> 中的 T 是"这条连接收发的消息类型"。
///   旧项目用 RTSMsg，这里我们用自己的 NetMessage 信封。
/// </summary>
public class ClientSession : KCPSession<NetMessage>
{
    /// <summary>
    /// 【序列化器】告诉 KCPNet"用什么工具把 NetMessage 和字节互相转换"。
    /// KCPNet 收到字节后，会调用这里的 Serializer 把字节变成 NetMessage 对象，
    /// 再传给 OnReciveMsg 方法。
    /// </summary>
    protected override IKCPMsgSerializer Serializer => NetMessageSerializer.Instance;

    /// <summary>
    /// 【连接成功回调】TCP/KCP 握手成功后，KCPNet 调用此方法。
    /// 旧项目在这里弹"连接服务器成功"的提示。
    /// </summary>
    protected override void OnConnected()
    {
        Debug.Log("[ClientSession] 连接服务器成功");
    }

    /// <summary>
    /// 【断开回调】连接断开时，KCPNet 调用此方法。
    /// </summary>
    protected override void OnDisConnected()
    {
        Debug.Log("[ClientSession] 断开服务器连接");
    }

    /// <summary>
    /// 【收到消息回调】服务器发来一条消息时，KCPNet 调用此方法。
    /// 注意：msg 已经被 Serializer 从字节还原成了 NetMessage 对象。
    ///
    /// ▍重要：为什么这里不直接处理，而是丢进队列？
    ///   OnReciveMsg 可能运行在【网络线程】，而 Unity 游戏逻辑只能在【主线程】跑。
    ///   如果在网络线程直接改游戏状态，会线程冲突甚至崩溃。
    ///   所以做法是：把消息放进一个线程安全的队列，等主线程的 Update 里取出来处理。
    ///   对应旧项目：NetSvc.AddMsgQue(msg)。
    /// </summary>
    protected override void OnReciveMsg(NetMessage msg)
    {
        // 丢进传输层队列（NetInbox），等主线程处理
        NetInbox.Enqueue(msg);
    }

    /// <summary>
    /// 【每帧回调】KCPNet 每帧调用一次（旧项目这里是空的）。
    /// </summary>
    protected override void OnUpdate(System.DateTime now)
    {
        // 目前不需要，留空
    }
}
}
