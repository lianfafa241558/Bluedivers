
namespace FPSGame.Net
{
using System;
using System.Collections.Generic;
using System.Text;
using KCPNet;
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
///   业务模块注册：  MessageCenter.Register<某消息类型>(某命令号, OnXxx);
///   收到消息分发：  MessageCenter.Dispatch(msg);  ← 自动找到 OnXxx 并调用
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
        // ⚠ 必须走 NetMsgCodec（AOT 安全），不能再写 MessagePackSerializer.Serialize：
        //   后者默认 StandardResolver → DynamicObjectResolver（Reflection.Emit），
        //   IL2CPP 不支持动态代码生成 ⇒ 打包版一发消息就抛 PlatformNotSupportedException
        //   （编辑器走 Mono 所以完全看不出来）。详见 NetMsgCodec 的类注释。
        return new NetMessage
        {
            CmdId = cmdId,
            Data = NetMsgCodec.Serialize(msg, typeof(T))
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
            //    ⚠ 与 Pack 同理：必须走 NetMsgCodec，不能再用 MessagePackSerializer.Deserialize
            //       （它最终落到 DynamicObjectResolver，IL2CPP 下会抛 PlatformNotSupportedException）。
            object body = NetMsgCodec.Deserialize(handler.MsgType, msg.Data, out int remaining);

            // ★ 载荷自检（2026-11-15）：读完声明的字段还剩下字节 ⇒ 两端 DTO 布局对不上（多端混旧包）。
            //   这时值已经不可信，但解码"成功"了 ⇒ 不在这里留痕就完全查不到。
            if (remaining > 0)
            {
                Debug.LogError($"[解码自检] cmd={msg.CmdId} type={handler.MsgType.Name} 解完还剩 {remaining} 字节 ⇒ 两端 DTO 布局不一致（多半混了旧包）；本次解析值不可信。{DescribeData(msg.Data)}");
            }

            // 3. 调用对应的处理回调
            handler.Callback(body);
        }
        catch (Exception e)
        {
            // ⚠ 必须打**完整异常**（e.ToString() = 类型 + 消息 + 调用栈）：
            //   只打 e.Message 会把"哪个处理器、哪一行"全丢掉（2026-10-06 打包版实测：
            //   只看到"消息 4004 处理失败:Object reference not set..."，定位不到具体位置）。
            Debug.LogError($"消息 {msg.CmdId} 处理失败:{e}");

            // ★ 载荷自检（2026-11-15 加）：EndOfStream 这类"字节比声明短"的失败，光看异常分不清是
            //   ①两端 4054 对应的 DTO 不同源（旧包）还是 ②载荷被截断（传输/信封）。
            //   ⚠ NetMsgCodec 对字段数**增减是双向兼容**的（多→Skip、少→缺省）⇒ 单纯"字段数量不同"
            //     不会抛这个异常，所以这两条日志（长度 + 头部字节 + 期望字段数）是唯一能定性的证据。
            Debug.LogError($"[解码自检] cmd={msg.CmdId} type={handler.MsgType.Name} 期望字段={NetMsgCodec.MemberCount(handler.MsgType)} {DescribeData(msg.Data)}");
        }
    }

    /// <summary>诊断用：载荷长度 + 前 16 字节 hex（够看出 array header 的值与字段边界）。</summary>
    private static string DescribeData(byte[] data)
    {
        if (data == null) return "len=null";
        int n = data.Length < 16 ? data.Length : 16;
        var sb = new StringBuilder(80);
        sb.Append("len=").Append(data.Length).Append(" head=");
        for (int i = 0; i < n; ++i) sb.Append(data[i].ToString("X2")).Append(' ');
        if (data.Length > n) sb.Append("...");
        return sb.ToString();
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
