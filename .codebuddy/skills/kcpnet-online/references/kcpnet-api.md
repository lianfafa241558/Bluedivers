# KCPNet 库 API 参考（反射导出）

> 来源：对 `Assets/Plugins/KCPNet/*.dll` 的反射/元数据解析结果（2026-09-30 实测）。
> 库无源码，本文件是**唯一可信的 API 事实来源**；升级 DLL 后用 Unity MCP `unity_reflect`（首选）或 `scripts/inspect_dll.py`（离线）重新核对。
> 命名空间：`KCPNet`（另有子命名空间 `KCPNet.Config`、`KCPNet.Util`）；底层 KCP 实现在 `System.Net.Sockets.Kcp`（`Kcp.dll`）。

## 1. 程序集与依赖矩阵

`KCPNet.dll`（43 KB，assembly identity `KCPNet 1.0.0.0`）程序集引用：

| 引用 | 版本 | 工程状态 |
| --- | --- | --- |
| `netstandard` | 2.0.0.0 | 目标框架 |
| `Kcp` | 2.0.0.0 | ✅ 同目录 `Kcp.dll`（20 KB，identity `Kcp 2.0.0.0`） |
| `MessagePack` | 3.1.8.0 | ✅ 已装 `MessagePack.dll`（netstandard2.0 资产） |
| `MessagePack.Annotations` | 3.1.8.0 | ✅ 已装 `MessagePack.Annotations.dll` |
| `System.Memory` | 4.0.1.2 | ✅ 由 Unity 自带的 `System.Memory 4.0.99.0` 满足，**不要自备** |
| `System.Buffers` | 4.0.2.0 | ✅ 同上，Unity 自带 |

`Kcp.dll` 程序集引用：`netstandard 2.0.0.0`、`System.Memory 4.0.1.0`（同样由 Unity 自带满足）。

`MessagePack.dll`（netstandard2.0 资产，380 KB，identity `MessagePack 3.1.8.0`）的 13 条引用，是本项目依赖闭包的真实来源：

| 引用 | 版本 | 处理 |
| --- | --- | --- |
| `MessagePack.Annotations` | 3.1.8.0 | ✅ 已装 |
| `Microsoft.Bcl.AsyncInterfaces` | 8.0.0.0 | ✅ 已装（8.0.0，netstandard2.0 资产） |
| `Microsoft.NET.StringTools` | 1.0.0.0 | ✅ 已装（NuGet 包 17.11.4，程序集版本即 1.0.0.0） |
| `System.Collections.Immutable` | 8.0.0.0 | ✅ 已装 |
| `System.Runtime.CompilerServices.Unsafe` | 6.0.0.0 | ✅ 已装 |
| `System.Memory` | 4.0.1.2 | Unity 自带 |
| `System.Buffers` | 4.0.2.0 | Unity 自带 |
| `System.Numerics.Vectors` | 4.1.3.0 | Unity 自带 |
| `System.Reflection.Emit` | 4.0.0.0 | Unity 自带 |
| `System.Reflection.Emit.ILGeneration` | 4.0.0.0 | Unity 自带 |
| `System.Reflection.Emit.Lightweight` | 4.0.0.0 | Unity 自带 |
| `System.Threading.Tasks.Extensions` | 4.2.0.1 | Unity 自带 |
| `netstandard` | 2.0.0.0 | 目标框架 |

> ⚠ 工程 API 兼容级别实测为 **`NET_Standard_2_0`**（不是 2.1），因此统一取 `lib/netstandard2.0/` 资产。MessagePack 也提供 `lib/netstandard2.1/`（依赖表只有 `MessagePack.Annotations` + `Microsoft.NET.StringTools` + `System.Collections.Immutable`），但 Std2.0 profile 下用不了。
> 只放上表标 ✅ 的 4 个额外 DLL 就够；多塞 Unity 自带的同名程序集会触发“预编译程序集重复”。

`Kcp.dll` 内类型（底层协议实现，业务不直接使用）：

```
System.Net.Sockets.Kcp.Kcp                 // KCP 协议状态机
System.Net.Sockets.Kcp.KcpSegment
System.Net.Sockets.Kcp.IKcpCallback / IKcpSetting / IKcpUpdate / IRentable
System.Net.Sockets.Kcp.KcpExtension_FDF71D0BC31D49C48EEA8FAA51F017D4
```

`KCPNet.dll` 内类型全清单（21 个公开类型 + 编译器生成的闭包/状态机类）：

```
KCPNet.Config.RegionConfig
KCPNet.Config.ServerRegionConfig
KCPNet.ConnectResult
KCPNet.IKCPMsgSerializer              (interface)
KCPNet.KCPLogColor                    (enum)
KCPNet.KCPMsg                         (class，NetMessage 的基类)
KCPNet.KCPNet`2                       (class，泛型：会话类型 + 消息类型)
KCPNet.KCPSession`1                   (class，泛型：消息类型)
KCPNet.KCPTool
KCPNet.LanBroadcaster
KCPNet.LanDiscoverer
KCPNet.LanDiscoverer+RoomEntry
KCPNet.LanDiscoveryConfig
KCPNet.LanRoomInfo
KCPNet.NetConfig
KCPNet.NetMessage                     (: KCPMsg)
KCPNet.NetMessageSerializer
KCPNet.SessionState                   (enum)
KCPNet.Util.ClientSecurity
KCPNet.Util.EncryptionHelper
KCPNet.Util.ServerSecurity
MessagePack.GeneratedMessagePackResolver (+ KCPNet / FormatterCache`1 / GetFormatterHelper)
```

> 最后一行说明：**库作者用 MessagePack 源生成器为自己的消息类型预生成了 resolver**，并编译进了 `KCPNet.dll`。若准备在 IL2CPP 下用 MessagePack，这就是可参考的正确姿势（生成 resolver + `StaticCompositeResolver`）。

## 2. `KCPNet<T, K>` —— 连接主体

```csharp
public KCPNet(bool isUnityEnvironment = true)   // ← 可选参数，所以 new KCPNet<T,K>() 合法
```

字段（public）：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `_serializer` | `IKCPMsgSerializer` | 内部消息序列化器 |
| `udp` | `UdpClient` | 底层 UDP |
| `remotePoint` | `IPEndPoint` | 对端地址 |
| `sessionDic` | `Dictionary<...>` | sid → 会话 |
| `security` | `ServerSecurity` | 服务器侧 RSA/AES 密钥管理 |
| `OnSessionConnected` / `OnSessionDisconnected` / `OnServerRestartDetected` | `Action<uint>` | 事件（也可用 `+=` 订阅，项目即如此） |
| `sid` | `uint` | 会话 ID 计数器 |
| `clientSession` | `T` | 客户端模式下自己那条会话（`NetSvc` 直接读它） |
| `_isAttemptingConnect` | `bool` | 连接中标记 |
| `SERVER_RESTART_CODE` | `uint` | 服务器重启特殊码，静态常量 = `4294967295`（`uint.MaxValue`） |

方法：

| 签名 | 说明 |
| --- | --- |
| `void StartAsServer(string ip, int port)` | 房主/服务器：绑定并开始 `ServerRecive()` 循环。抛 `SocketException`（端口占用/未释放） |
| `void StartAsClient(string ip, int port)` | 成员：绑定本机 UDP 并指向目标，开始 `ClientRecive()` 循环 |
| `Task<ConnectResult> ConnectServer(int interval, int maxintervalSum = 5000)` | 每 `interval` ms 重试握手，累计超过 `maxintervalSum` ms 判定失败。**异步**，用 `await` |
| `void SendToAll(K msg)` | 广播给所有已连接会话 |
| `bool TryGetSession(uint sid, out T session)` | 按 sid 取会话（房主定向发送用） |
| `List<...> GetSessions()` / `int GetSessionCount()` | 会话列表/数量 |
| `void CloseServer()` / `void CloseClient()` | 关闭（停止 UDP + 循环） |
| `void SendUDPMsg(byte[] bytes, IPEndPoint remotePoint)` | 底层直发（一般不用） |
| `uint GenerateUniqueSessionID()` | 生成 sid |

`ConnectResult`：

```csharp
public bool Success { get; set; }
public string Message { get; set; }
```

## 3. `KCPSession<T>` —— 单条连接

```csharp
public KCPSession()          // 无参，由库实例化
```

必须 override 的成员：

```csharp
protected abstract/ virtual IKCPMsgSerializer Serializer { get; }   // 项目：=> NetMessageSerializer.Instance
protected virtual void OnConnected();                               // 握手成功
protected virtual void OnDisConnected();                            // 断开
protected virtual void OnReciveMsg(T msg);                          // 收到业务消息（可能在网络线程）
protected virtual void OnUpdate(DateTime now);                       // 周期回调（项目留空）
```

可用的公开方法/属性：

| 成员 | 说明 |
| --- | --- |
| `void SendMsg(T msg)` / `void SendMsg(byte[] msg_bytes)` | 发送业务消息 / 原始字节 |
| `bool IsConnected()` | 是否已连接（业务发送前判断） |
| `uint GetSessionID()` | 本会话 sid（房主端关键） |
| `void InitSession(uint sid, Action<...> udpSender, IPEndPoint remotePoint, byte[] aesKey)` | 库内部初始化（勿手动调） |
| `void CloseSession()` | 关闭该会话 |
| `Task UpdateAsync()` | 库内部驱动（`Kcp.dll` 的 update 循环） |
| `void ModifyConnected()` | 强制标记已连接 |
| `IPEndPoint mremotePoint` / `Action<...> mudpSender` | 公开字段，一般不用 |
| `byte[] AesKey` | 会话 AES 密钥 |
| `long TimeoutMs` / `bool EnableTimeoutCheck` / `void SetTimeout(long)` / `void RefreshLastRecvTime()` | 心跳超时控制 |
| `T Deserialize(byte[] bytes)` | 用 `Serializer` 反序列化 |

`SessionState` 枚举：`None = 0`、`Connected = 1`、`DisConnected = 2`。

## 4. 序列化

```csharp
public interface IKCPMsgSerializer
{
    byte[] Serialize(KCPMsg msg);
    KCPMsg Deserialize(byte[] bytes);
}

public class KCPMsg { }                       // 标记基类，无成员

public class NetMessage : KCPMsg
{
    public int CmdId;                          // 命令号（见项目 CmdId.cs）
    public byte[] Data;                        // DTO 的 MessagePack 字节
}

public class NetMessageSerializer            // 具体实现，单例
{
    public static NetMessageSerializer Instance;
    public byte[] Serialize(KCPMsg msg);
    public KCPMsg Deserialize(byte[] bytes);
}
```

> 库只负责**信封**（`CmdId` + `Data`）的序列化；`Data` 的业务语义由项目的 `MessageCenter` 用 MessagePack 处理。这条分工是理解整个框架的关键。

## 5. 配置

### `NetConfig`

```csharp
public const int HostGamePort      = 17666;   // 房主 KCP 监听端口
public const int LanBroadcastPort  = 29800;   // 局域网广播/发现端口
public const int LanSelfBroadcastPort = 29801;// 同机双实例时成员侧用
```

### `LanDiscoveryConfig`（静态可注入，默认值如下）

| 字段 | 默认值 |
| --- | --- |
| `BroadcastPort` | `29800` |
| `ListenPort` | `29800` |
| `BroadcastAddress` | `255.255.255.255` |
| `AnnounceIntervalMs` | `2000` |
| `DiscoveryTimeoutMs` | `1500` |
| `RoomExpireMs` | `6000` |
| `BroadcastTtl` | `1` |
| `Magic` | `"KCPLAN:"` |
| `AnnounceType` / `DiscoverType` | `"ANNOUNCE"` / `"DISCOVER"` |

```csharp
public static void Setup(
    int? broadcastPort = null, int? listenPort = null, IPAddress broadcastAddress = null,
    int? announceIntervalMs = null, int? discoveryTimeoutMs = null, int? roomExpireMs = null);

public static string BuildPacket(string type, string payload);
public static bool TryParse(string raw, out string type, out string payload);
```

### `KCPNet.Config.RegionConfig` / `ServerRegionConfig`

```csharp
public class RegionConfig
{
    public string Code, DisplayName, ServerIP;      // 分区码/显示名/服务器地址
    public int ServerPort, RedisDatabase, MaxPlayers, MaxConcurrentBattles, QueueCapacity;
    public string DataBaseAddress, RedisEndPoint, RedisPassword;
}
public class ServerRegionConfig
{
    public static Dictionary<string, RegionConfig> Regions;
    public static RegionConfig Current { get; set; }
    public static void Initialize(string regionCode);
    public static string GetDisplayName(string regionCode);
}
```

> 这是"专用服务器 + Redis + 数据库 + 分区"的配置模型，**Bluedivers 当前的 P2P 房主模式完全没用到**；若将来接官方服务器才需要。

## 6. 局域网房间发现

### `LanRoomInfo`（JSON 手写序列化，字段名稳定）

```csharp
public string RoomName;
public string HostIp;
public int    HostPort;        // 必须是房主 KCP 监听端口，否则回连不上
public int    PlayerCount;
public int    MaxPlayers;
public string MapName;
public bool   PasswordProtected;
public string Version;
public string[] PlayerNames;

public string ToJson();
public LanRoomInfo FromJson(string json);
```

### `LanBroadcaster`（房主侧）

```csharp
public LanBroadcaster(LanRoomInfo roomInfo);   // 只此一个 ctor
public Func<LanRoomInfo> RoomInfoProvider { get; set; }  // 每轮广播前重新取“最新房间信息”
public void Start();                           // 启动 AnnounceLoop + RespondLoop
public void Stop();
public void Dispose();
```

- `Start()` 后有两个后台循环：定时 `ANNOUNCE` 广播 + 应答成员的 `DISCOVER`。
- 人数等动态字段通过 `RoomInfoProvider`（返回同一个 `LanRoomInfo` 引用并就地改字段）刷新，项目即用这种方式。

### `LanDiscoverer`（成员侧）

```csharp
public LanDiscoverer(int listenPort = -1);      // -1 = 用 LanDiscoveryConfig.ListenPort
public string VersionFilter { get; set; }       // 只收指定版本房间
public bool   AutoExpire { get; set; }          // 自动清理过期房间
public void StartListening();
public Task Scan();                             // 发 DISCOVER，等待 1500ms 左右
public List<LanRoomInfo> GetRooms();            // 建议 Scan 完成后/轮询获取
public void PurgeExpired();
public void Stop();
public void Dispose();
```

内部字段（参考）：`ConcurrentDictionary<...> _rooms`、`UdpClient _udp`、`CancellationTokenSource _cts`、`bool _listening` —— 说明**发现器自己起后台线程**，`Stop()`/`Dispose()` 必须调，否则退出编辑器后线程残留。

## 7. 工具与安全

```csharp
public static class KCPTool
{
    public static Action<string, object[]> LogFunc;
    public static Action<KCPLogColor, string, object[]> ColorLogFunc;
    public static Action<string, object[]> WarnLogFunc;
    public static Action<string, object[]> ErrLogFunc;

    public static void Log(string msg, params object[] args);
    public static void ColorLog(KCPLogColor color, string msg, params object[] args);
    public static void WarnLog(string msg, params object[] args);
    public static void ErrLog(string msg, params object[] args);
    public static void ConsoleLog(string msg, KCPLogColor color);
    public static byte[] Compress(byte[] input);
    public static byte[] DeCompress(byte[] compressedData);
    public static ulong GetUTCStartMilliseconds();
}

public enum KCPLogColor { None, Red, Green, Blue, Cyan, Magenta, Yellow }

public static class ClientSecurity          // 客户端
{
    public static byte[] GenerateAesKey();
    public static byte[] EncryptAesKeyWithRsa(byte[] aesKey, string publicKeyXml);
}
public class ServerSecurity                 // 服务器（房主内嵌）
{
    public RSACryptoServiceProvider rsa;
    public string GetPublicKey();
    public byte[] DecryptAesKey(byte[] encryptedAesKey);
    public void StoreSessionKey(uint sessionId, byte[] aesKey);
    public bool TryGetSessionKey(uint sessionId, out byte[] aesKey);
    public void PurgeSessionKey(uint sid);
}
public static class EncryptionHelper
{
    public static byte[] Encrypt(byte[] data, byte[] aesKey);
    public static byte[] Decrypt(byte[] encryptedData, byte[] aesKey);
    public static bool CompareHashes(byte[] a, byte[] b);
}
```

- 握手流程由库内部驱动：服务器 `GetPublicKey()` → 客户端 `GenerateAesKey()` + `EncryptAesKeyWithRsa()` → 服务器 `DecryptAesKey()` + `StoreSessionKey()` → 之后会话用 `AesKey` + `EncryptionHelper` 加解密。
- 项目当前**没有**任何安全配置调用，属于"库自带但未显式启用/未自定义"的能力；若要替换日志输出（例如接到项目自己的日志系统），改 `KCPTool.LogFunc/ColorLogFunc` 等静态委托即可，`NetHostSvc` 里已经在用 `KCPTool.ColorLog`。
