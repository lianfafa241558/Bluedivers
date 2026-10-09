using System.Collections.Generic;
using KCPNet;

namespace FPSGame.Net
{
    /// <summary>
    /// 【传输层入站队列】网络线程收到的消息先放这里，由**主线程**取出分发。
    ///
    /// <para>▍为什么单独抽出来：会话类（<see cref="ClientSession"/> / <see cref="HostSession"/>）跑在
    /// 传输线程、只该依赖传输层；原先它们直接调 <c>NetSvc.Instance.AddMsgQue</c> /
    /// <c>NetHostSvc.Instance.AddMsgQue</c>（游戏层）⇒ 会话就再也下沉不回传输层了。
    /// 现在队列放在 02_Net，谁取由 07_NetGame 的 NetSvc / NetHostSvc 决定 ⇒ 方向变成单向
    /// "游戏层 → 传输层"。</para>
    /// </summary>
    public static class NetInbox
    {
        /// <summary>房主侧入队项：消息 + 来源会话号。</summary>
        public struct Item
        {
            public NetMessage Msg;
            public uint Sid;
        }

        private static readonly Queue<NetMessage> _client = new Queue<NetMessage>();
        private static readonly Queue<Item> _host = new Queue<Item>();
        private static readonly object _lock = new object();

        /// <summary>成员侧入队（只有一条连接，不带 sid）。传输线程调用。</summary>
        public static void Enqueue(NetMessage msg)
        {
            if (msg == null) return;
            lock (_lock) _client.Enqueue(msg);
        }

        /// <summary>房主侧入队（带来源 sid，房主才能做定向回复）。传输线程调用。</summary>
        public static void Enqueue(NetMessage msg, uint sid)
        {
            if (msg == null) return;
            lock (_lock) _host.Enqueue(new Item { Msg = msg, Sid = sid });
        }

        /// <summary>主线程取一条成员侧消息。</summary>
        public static bool TryDequeueClient(out NetMessage msg)
        {
            lock (_lock)
            {
                if (_client.Count == 0) { msg = null; return false; }
                msg = _client.Dequeue();
                return true;
            }
        }

        /// <summary>主线程取一条房主侧消息。</summary>
        public static bool TryDequeueHost(out Item item)
        {
            lock (_lock)
            {
                if (_host.Count == 0) { item = default; return false; }
                item = _host.Dequeue();
                return true;
            }
        }

        public static void ClearClient()
        {
            lock (_lock) _client.Clear();
        }

        public static void ClearHost()
        {
            lock (_lock) _host.Clear();
        }
    }
}
