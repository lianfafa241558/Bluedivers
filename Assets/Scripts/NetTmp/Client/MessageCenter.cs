
namespace FPSGame.Net
{
using System;
using System.Collections.Generic;
using KCPNet;
using MessagePack;
using UnityEngine;

/// <summary>
/// 【消息中心】负责消息的注册、打包、分发。
/// 这是整套框架的核心改进，替代了旧项目 NetSvc 里的巨型 switch。
///
/// ▍它解决什么问题？
///   旧项目 NetSvc.HandoutMsg 用一个 40+ 行的 switch 分发消息：
///     case CMD.RspLogin: LoginSys.NtfLogin(msg); break;
///     case CMD.NtfChat:  BattleSys.NtfChat(msg);  break;
///     ...
///   每加一个消息就要去改 NetSvc，文件越来越臃肿。
///
/// ▍这里怎么做？
///   用"注册表 + 泛型处理器"：每个业务模块自己注册自己关心的消息，
///   分发时按 CmdId 查字典找到对应处理器调用，不再需要 switch。
///
/// ▍流程：
///   业务模块注册：  MessageCenter.Register<LoginRspMsg>(CmdId.LoginRsp, OnLoginRsp);
///   收到消息分发：  MessageCenter.Dispatch(msg);  ← 自动找到 OnLoginRsp 并调用
/// </summary>
public static class MessageCenter
{
    /// <summary>
    /// 单个命令的注册项：保存"消息类型"和"处理回调"。
    /// </summary>
    private sealed class Handler
    {
        public Type MsgType;          // 具体消息的类型（用于反序列化 Data）
        public Action<object> Callback; // 处理回调（泛型转 object 便于统一存储）
    }

    /// <summary>命令号 → 处理器 的注册表（用字典替代 switch）</summary>
    private static readonly Dictionary<int, Handler> _handlers = new();

    /// <summary>
    /// 【注册】把某个命令号和一个处理函数绑定。
    /// </summary>
    /// <typeparam name="T">消息类型（必须是 [MessagePackObject] 类）</typeparam>
    /// <param name="cmdId">命令号（见 CmdId.cs）</param>
    /// <param name="handler">收到该消息时执行的回调</param>
    public static void Register<T>(int cmdId, Action<T> handler) where T : class
    {
        _handlers[cmdId] = new Handler
        {
            MsgType = typeof(T),
            Callback = obj => handler(obj as T)
        };
    }

    /// <summary>
    /// 【打包】构造一个 NetMessage 信封并序列化，供发送用。
    /// 业务代码调用：NetSvc.Instance.SendMsg(MessageCenter.Pack(cmdId, msg));
    /// </summary>
    public static NetMessage Pack<T>(int cmdId, T msg) where T : class
    {
        // 创建信封：装命令号 + 序列化后的消息内容
        return new NetMessage
        {
            CmdId = cmdId,
            Data = MessagePackSerializer.Serialize(msg)
        };
    }

    /// <summary>
    /// 【分发】根据 NetMessage 的 CmdId 查表，反序列化 Data 并调用对应处理器。
    /// 由 NetSvc.Update() 在主线程调用（替代旧项目的巨型 switch）。
    /// </summary>
    /// <param name="msg">已经反序列化好的 NetMessage 信封</param>
    public static void Dispatch(NetMessage msg)
    {
        // 1. 查表：找到这个命令号对应的处理器
        if (!_handlers.TryGetValue(msg.CmdId, out var handler))
        {
            Debug.LogError($"未注册消息:{msg.CmdId}");
            return;
        }

        try
        {
            // 2. 反序列化 Data，还原成具体消息对象（用注册时保存的类型）
            //    ⚠️ MessagePack 3.1.8 的正确写法：非泛型 Deserialize(Type, ReadOnlyMemory<byte>)
            //       byte[] 会隐式转成 ReadOnlyMemory<byte>，不需要 ref。
            //       参数顺序是 Type 在前，别写反了。
            object body = MessagePackSerializer.Deserialize(handler.MsgType, msg.Data);

            // 3. 调用对应的处理回调
            handler.Callback(body);
        }
        catch (Exception e)
        {
            Debug.LogError($"消息 {msg.CmdId} 处理失败:{e.Message}");
        }
    }

    /// <summary>
    /// 【反注册】模块销毁时调用，避免残留导致错误分发。
    /// </summary>
    public static void Unregister(int cmdId)
    {
        _handlers.Remove(cmdId);
    }
}

}
