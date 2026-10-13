# KCPNet 源码分析报告

> 生成时间：2026-10-13
> 调研对象：`e:\KCPNet`（只读调研，未修改任何源文件）
> 面向读者：只持有 `KCPNet.dll`、看不到源码的会话。**每条结论均附「文件:行」或「类型.方法」证据。**
> 使用场景：Unity 2022.3.62f3 / Standalone / IL2CPP / netstandard2.0；房主权威 + 局域网 P2P。

---

## 0. 版本

| 项 | 值 | 证据 |
|---|---|---|
| 库程序集版本 | **1.0.0.0**（`AssemblyVersion` / `AssemblyFileVersion` / `InformationalVersion` 全为 1.0.0.0，由 SDK 自动生成） | `obj/Debug/netstandard2.0/KCPNet.AssemblyInfo.cs:15-20` |
| 项目未显式指定 `<Version>` | csproj 只有 `TargetFramework`，无 `Version`/`PackageId` | `KCPNet.csproj:3-5` |
| Kcp 依赖 | **2.0.0** | `KCPNet.csproj:21` |
| 其他依赖 | log4net 3.0.2（**实际未被调用**，只有注释残留）、MessagePack 3.1.8 | `KCPNet.csproj:22-23`；`KCPNet.cs:85,676,682` |
| 条件编译 | csproj 里 `DefineConstants=SERVER;DEBUG;TRACE`（Debug）/`SERVER;TRACE`（Release），但**库源码里没有任何 `#if SERVER`**，该常量是死配置 | `KCPNet.csproj:11,17`；全库 grep `#if` 无命中 |
| 目标框架 | netstandard2.0 | `KCPNet.csproj:4` |
| 源码里没有版本号/文件头/变更记录 | 无 `CHANGELOG`、无 git 信息可读 | 根目录只有 csproj/sln + 源码 |

> ⚠️ **重要提醒**：库源码在 2026-10-10 做过一次**服务端会话生命周期重构**（Q3 的幂等/回收部分）。手上这版 DLL 如果是更早构建的，Q3 的结论可能不适用。
> **可自检**：运行时若日志出现 `会话ID:{0}（身份 {1}）添加到会话字典` 则是新版；若只有 `会话ID:{0}添加到会话字典` 则是旧版（`KCPNet.cs:232` vs 旧实现）。

---

## 1. 结论速查表

| 问题 | 结论一句话 | 证据(文件:行) |
|---|---|---|
| Q1 | **库从不给 `HostIp` 赋值**；`LanBroadcaster` 原样发送该字段，成员端 `LanDiscoverer` 用**收包 source 地址无条件覆盖**它；无任何网卡挑选/Loopback 过滤逻辑 | `LanBroadcaster.cs:95,147`；`LanDiscoverer.cs:154-156`；`LanRoomInfo.cs:24,81,136` |
| Q2 | 仅"受限广播到 255.255.255.255:29800 + 单播应答"这一种发现方式；**无跨子网/单播发现 API、无网卡枚举、无 IP 列表备用 API**；`Setup()` 可改 6 项，`Magic/AnnounceType/DiscoverType` 是 `const` | `LanDiscoverer.cs:110-113`；`LanBroadcaster.cs:97-98,143-151`；`LanDiscoveryConfig.cs:25-59,71-86` |
| Q3 | 有幂等复用（按 `cid:`/`ep:` 身份键），**只有握手会建会话**，未知 sid 数据包一律丢弃；`ClientId` 必须在 `ConnectServer` 前设置，库不重置它；未完成握手的会话 8s 回收 | `KCPNet.cs:98-114,162-235,239-256,301-339`；`NetConfig.cs:47` |
| Q4 | `NetConfig` 全是 **`const`（编译期内联，无法运行时改）**，无配置文件/环境变量/Setup 入口；心跳魔数 `KCPHB1` 是 `private static readonly` 不可改，间隔/超时是**每会话**属性可改可关 | `NetConfig.cs:12-47`；`KCPSession.cs:59,65,69,74` |
| Q5 | **无 relay / 无房间列表服务端 / 无 NAT 穿透**；RSA→AES 不是"假设局域网"而是**整段被注释停用了**（明文）；公网直连可行但踩 3 个坑：`IPAddress.Parse` 不支持域名、客户端严格校验回包源地址、握手不重传 | `KCPNet.cs:540,618,571-572`；`KCPNet.cs:287-292,624`；`Config/ServerRegionConfig.cs:6-52` |

---

## 2. Q1 详细：`LanRoomInfo.HostIp` 由谁填？

### 2.1 `HostIp` 的全部写入点（全库 + 示例）

| # | 位置 | 谁写 | 写了什么 |
|---|---|---|---|
| 1 | `LanRoomInfo.cs:24` | 字段初始化 | `public string HostIp = "";` —— 默认空串 |
| 2 | `LanRoomInfo.cs:136` | `LanRoomInfo.FromJson` | 反序列化时**从报文里读**（成员端解析广播时写入） |
| 3 | `LanDiscoverer.cs:156` | `LanDiscoverer.ListenLoop` | `info.HostIp = actualIp;` —— **用收包的 source 地址覆盖** |
| 4 | `NetTmp/LanTest/Program.cs:23` | 测试代码 | 手动赋值（仅测试） |

**结论：`LanBroadcaster` 里没有任何 `HostIp` 写入点。** 它只做 `info.ToJson()`（`LanBroadcaster.cs:95` 周期广播 / `:147` 单播应答），`ToJson` 写的是 `WriteString(sb, "hostIp", HostIp)`（`LanRoomInfo.cs:81`），即**原样透传调用方给的对象**。

所以"提问方代码里不给 `HostIp` 赋值"**成立且比这更强**：不是"提问方不赋值"，而是**整个库（含参考示例 `NetHostSvc`）都不赋值**。参考示例房主的构造是这样（注意没有 `HostIp`）：

```csharp
// NetTmp/Client/NetHostSvc.cs:118-127
RoomInfo = new LanRoomInfo
{
    RoomName = roomName,
    HostPort = hostPort,          // ← 只填端口
    PlayerCount = 1,
    MaxPlayers = MaxPlayers,
    MapName = mapName,
    PasswordProtected = !string.IsNullOrEmpty(_roomPassword),
    Version = Application.version,
};                                 // ← 没有 HostIp，广播出去就是 "hostIp":""
```

> 补充：`NetTmp/**` 被显式排除出库编译（`KCPNet.csproj:28`），所以 `NetSvc/NetHostSvc` **不在 DLL 里**，是 Unity 工程自己的代码。上表第 1~3 项才是库的行为。

### 2.2 `LanBroadcaster` 按什么规则挑本机 IP？

**它不挑。** 全库没有任何网卡枚举/IP 挑选代码：

- grep `NetworkInterface` / `Dns.` / `GetHostAddresses` / `GetHostEntry` / `AddressFamily` → **零命中**。
- 唯一的地址相关操作是 `Bind(new IPEndPoint(IPAddress.Any, ...))`（`LanBroadcaster.cs:68`、`LanDiscoverer.cs:89`）和 `SendAsync(..., new IPEndPoint(LanDiscoveryConfig.BroadcastAddress, ...))`（`LanBroadcaster.cs:97-98`）。

因此：

- **多网卡 / 虚拟网卡（VMware、VirtualBox、Hyper-V vEthernet、ZeroTier）会不会挑到 127.0.0.1 或虚拟网卡地址？**
  库不做选择，**出口网卡完全由 OS 路由表决定**（限广播 `255.255.255.255` 走哪块网卡是内核决定的）。所以**理论上可能出现在虚拟网卡或 Loopback 上**，取决于目标机器的路由度量值。
- **有没有排除 Loopback 的逻辑？**
  **没有**。全库唯一的 Loopback 引用在测试工程（`NetTmp/LanTest/Program.cs:266,281,319`），不在库里。
- ⚠️ 一旦出口是 ZeroTier/VPN/Hyper-V 这类虚拟网卡，成员拿到的 `HostIp` 就会是 `25.x.x.x` / `172.x.x.x` 这类地址，`StartAsClient` 会静默连不上（UDP 无错误返回，只在 `ConnectServer` 超时后才报失败）——**这是"连不上"现象的一个高度可疑来源**。

### 2.3 成员侧：用包里的值，还是 source 地址？

**无条件用 source 地址覆盖。** 这是硬编码行为：

```csharp
// Services/Lan/LanDiscoverer.cs:149-165
if (type == LanDiscoveryConfig.AnnounceType)
{
    var info = LanRoomInfo.FromJson(payload);
    if (info == null) continue;

    // 以实际发送方地址作为可靠 HostIp（覆盖广播里可能不准的值）
    string actualIp = result.RemoteEndPoint.Address.ToString();
    info.HostIp = actualIp;                       // ← 无条件覆盖

    if (!string.IsNullOrEmpty(VersionFilter) && info.Version != VersionFilter)
        continue;

    string key = actualIp + ":" + info.HostPort;  // ← 去重键也用 source ip
    _rooms[key] = new RoomEntry { Info = info, LastSeenTicks = DateTime.UtcNow.Ticks };
}
```

后果 1：**报文里的 `hostIp` 字段在成员侧完全无效**（可从 DLL 侧忽略它）。
后果 2：`RespondLoop` 的单播应答也是从同一个 `Bind(IPAddress.Any)` 的 socket 发出的（`LanBroadcaster.cs:150`），**应答的 source 地址可能和广播的 source 地址不是同一块网卡**。而房间去重键是 `sourceIp:HostPort`（`LanDiscoverer.cs:163`），所以同一房间可能以两个不同 key 出现 → 列表里看到"重复房间"，其中一个连不通。

### 2.4 调用方能否指定/覆盖？库会不会覆盖掉？

| 侧 | 能不能指定 | 库会不会覆盖 |
|---|---|---|
| 房主侧 | **能**。`LanRoomInfo.HostIp` 是 public 字段，随便赋值，`LanBroadcaster` 原样发出（`LanBroadcaster.cs:95`） | **不会** |
| 成员侧 | **广播通道上不能**。`ListenLoop` 每次都覆盖（`:156`） | **会**，每次收到 ANNOUNCE 都覆盖一次 |

**唯一可行的覆盖方式**（因为 `GetRooms()` 返回的是**缓存对象本身的引用**，不是深拷贝）：

```csharp
// Services/Lan/LanDiscoverer.cs:194-198 —— 返回的是 _rooms 里存的同一个对象引用
public List<LanRoomInfo> GetRooms()
{
    if (AutoExpire) PurgeExpired();
    return _rooms.Values.Select(e => e.Info).ToList();   // Select 取 .Info，非拷贝
}
```

所以在 `GetRooms()` 之后、`StartAsClient()` 之前手工改 `room.HostIp` **有效且不会被库撤销**；但只要下一条 ANNOUNCE 到达（房主每 2s 一条，见 `LanDiscoveryConfig.cs:50`），同一个 key 会被重新 `FromJson` + 覆盖 —— `_rooms[key] = new RoomEntry{...}` 会替换成**新对象**（`LanDiscoverer.cs:164-165`），持有的旧引用不再更新，但再次 `GetRooms()` 就会拿到新的、被覆盖的值。
**结论：这个覆盖手段脆弱，约每 2 秒失效一次。**

### 2.5 平台开关

- **没有任何 `UNITY_*` 或平台条件编译**。全库 `#if` 零命中。
- 唯一的平台分支是 Windows 专用的 socket `IOControl` hack（抑制强制关闭时的 10054 错误），且用**运行时判断**而不是条件编译：`KCPNet.cs:80-83`、`KCPNet.cs:536-539`。

---

## 3. Q2 详细：发现层能力边界

### 3.1 广播地址 / 端口 / TTL / 子网

| 项 | 值 | 证据 |
|---|---|---|
| 广播地址 | `IPAddress.Broadcast` = `255.255.255.255`（受限广播） | `LanDiscoveryConfig.cs:44` |
| 广播端口 | `NetConfig.LanBroadcastPort` = **29800** | `LanDiscoveryConfig.cs:38`；`NetConfig.cs:18` |
| 监听端口 | 默认 = 广播端口 29800（成员发现端） | `LanDiscoveryConfig.cs:47` |
| 房主周期 | 2000 ms | `LanDiscoveryConfig.cs:50`；`LanBroadcaster.cs:108` |
| TTL | `BroadcastTtl = 1`，通过 **`SocketOptionName.MulticastTimeToLive`** 设置 | `LanDiscoveryConfig.cs:59`；`LanBroadcaster.cs:67`；`LanDiscoverer.cs:88` |
| 子网处理 | **完全没有**。不做掩码计算、不做定向广播（如 `192.168.1.255`）、不枚举网卡 | 全库 grep 无掩码/网卡相关代码 |

⚠️ **TTL 是个坑**：代码设的是 `MulticastTimeToLive`（`IP_MULTICAST_TTL`），而报文是**单播/广播**而非组播。这个选项对 `255.255.255.255` 的生存期是否生效**取决于 OS 实现**，源码层面无法保证"不跨路由器"。要真正 TTL=1，应设 `SocketOptionName.IpTimeToLive`（`IP_TTL`）——库没这么做。

### 3.2 跨子网 / 单播 / 定向发现

- **跨子网：不支持。** 受限广播 `255.255.255.255` 不被路由器转发（OS 行为），且库不做任何补救。
- **单播/定向发现：没有 API。** `Scan()` 硬编码发到 `BroadcastAddress:BroadcastPort`：

```csharp
// Services/Lan/LanDiscoverer.cs:105-119
public async Task Scan()
{
    EnsureListening();
    try
    {
        string packet = LanDiscoveryConfig.BuildPacket(LanDiscoveryConfig.DiscoverType, "");
        byte[] bytes = Encoding.UTF8.GetBytes(packet);
        var target = new IPEndPoint(LanDiscoveryConfig.BroadcastAddress, LanDiscoveryConfig.BroadcastPort);
        await _udp.SendAsync(bytes, bytes.Length, target);   // ← 只能播，不能点对点
    }
    // ...
}
```

- **但协议是可手工复用的**：房主的 `RespondLoop` 对**任何**收到 `"KCPLAN:DISCOVER\n"` 的来源单播回房间信息（`LanBroadcaster.cs:143-151`）。所以完全可以自己 `UdpClient.Send` 一份 `"KCPLAN:DISCOVER\n"` 到某个公网/固定 IP:29800 来"定向发现"，不需要改库。

### 3.3 "给我一个 IP 列表直接去连"的备用 API

**库不提供。** 唯一入口是 `KCPNet.StartAsClient(ip, port)`（`KCPNet.cs:532`），自己写循环即可：

```csharp
// 参考示例里已有的"直连已知地址"写法（不进广播流程）
// NetTmp/Client/NetSvc.cs:76-80
public void ConnectDefaultServer()
{
    Disconnect();
    ConnectTo((host) => host.StartAsClient(SRV_IP, SRV_PORT),
              (kcp) => kcp.ConnectServer(200, 5000));
}
```

### 3.4 `LanDiscoveryConfig.Setup(...)` 能改什么

```csharp
// Services/Lan/LanDiscoveryConfig.cs:71-86
public static void Setup(
    int? broadcastPort = null, int? listenPort = null, IPAddress broadcastAddress = null,
    int? announceIntervalMs = null, int? discoveryTimeoutMs = null, int? roomExpireMs = null)
```

| 参数 | 可改 | 说明 |
|---|---|---|
| `broadcastPort` | ✅ | 会同时把 `ListenPort` 设为同值（`else if (broadcastPort.HasValue)`，`:81`） |
| `listenPort` | ✅ | 同机测试时用（`new LanDiscoverer(29801)`） |
| `broadcastAddress` | ✅ | 跨网段唯一手段：改成 `192.168.1.255` 之类 |
| `announceIntervalMs` | ✅ | |
| `discoveryTimeoutMs` | ✅（但**是死参数**） | **全库只有赋值、没有任何读取点** → 设了没用 |
| `roomExpireMs` | ✅ | `LanDiscoverer.cs:184` 真正使用 |
| `BroadcastTtl` | ⚠️ 不在 Setup 里，但它是 `public static int` → 可直接赋值 | `LanDiscoveryConfig.cs:59` |
| `Magic` / `AnnounceType` / `DiscoverType` | ❌ `const`，编译期内联 | `LanDiscoveryConfig.cs:25,28,31` |

> 另一个死配置：`NetConfig.LanSelfBroadcastPort = 29801`（`NetConfig.cs:12`）**全库无任何引用**，只是文档里让你"手工传 29801"。

---

## 4. Q3 详细：会话身份与去重（"幽灵会话"）

### 4.1 `BuildIdentityKey` / `ParseClientId`

```csharp
// KCPNet.cs:98-114
private static string BuildIdentityKey(string clientId, IPEndPoint ep)
{
    return string.IsNullOrEmpty(clientId) ? "ep:" + EndpointKey(ep)   // ep:{ip}:{port}
                                          : "cid:" + clientId;
}
private static string ParseClientId(string message)
{
    if (string.IsNullOrEmpty(message)) return null;
    int colon = message.IndexOf(':');
    if (colon < 0) return null;                                     // 无冒号 → null → 退化为 ep:
    string cid = message.Substring(colon + 1).Trim();
    if (cid.Length == 0) return null;                               // "REQUEST_CONNECT:" → 也退化为 ep:
    if (cid.Length > 64) cid = cid.Substring(0, 64);                // 防御性截断
    return cid;
}
```

- **clientId 从哪来**：只能从**两个纯文本 UDP 报文**里来 —— 客户端发的 `"REQUEST_CONNECT:<ClientId>"`（`KCPNet.cs:571`）和 `"DISCONNECT:<ClientId>"`（`KCPNet.cs:701`）。它是**明文、无签名、可伪造**的。
- **什么时候可用**：仅在这两条报文到达服务器时。KCP 数据包**不带 clientId**（第一个 4 字节是 conv=sid，`KCPNet.cs:315`）。
- **为空时怎么退化**：键变成 `ep:{ip}:{port}`；`ep` 取的是 `result.RemoteEndPoint`（UDP 源地址）。此时客户端换本地端口 = 变成"另一个人"。
- 注意：`REQUEST_PUBLIC_KEY` 也被当成握手触发词（`KCPNet.cs:301`），若收到不带 `:<cid>` 的它，同样退化为 `ep:`。

### 4.2 是否有幂等复用？什么条件会新建会话？

**有复用。** 现在只有一条建会话路径 —— 握手（`KCPNet.cs:162-235`）：

```csharp
// KCPNet.cs:164-196（节选）
string key = BuildIdentityKey(clientId, remote);
lock (sessionDic)
{
    if (clientKeyDic.TryGetValue(key, out uint mapped))
    {
        if (sessionDic.TryGetValue(mapped, out existing) && existing != null)
            reuseSid = mapped;                 // ← 复用：同一 sid，直接返回
        else { clientKeyDic.Remove(key); sidToKey.Remove(mapped); }   // 脏映射清理
    }
}
if (reuseSid != 0)
{
    IPEndPoint cur = existing.mremotePoint;
    if (cur == null || !cur.Equals(remote))
        existing.mremotePoint = remote;        // ← 仅更新远端地址（换端口重连）
    return reuseSid;                           // ← 不新建、不关闭
}
```

**会新建会话的条件（只有 2 个）**：
1. 该身份键**从未出现过**；
2. 该身份键的映射还在、但对应会话对象已被回收（此时先清脏映射再新建）。

**明确不会新建的情况**：
- **重复 / 重传的 `REQUEST_CONNECT`** → 命中 `reuseSid` 分支，回同一个 sid（`KCPNet.cs:301-313`）。
- **KCP 分包 / 数据包 / ACK** → 一律按 conv 查表，查不到就**丢弃**：

```csharp
// KCPNet.cs:323-339
T session = null;
lock (sessionDic) { sessionDic.TryGetValue(sid, out session); }
if (session != null)
{
    EstablishSession(sid, session);     // 首个包 → 握手完成，恢复正常超时
    session.ReciveData(result.Buffer);
}
else
{
    DropUnknownSid(sid, result.RemoteEndPoint);   // 未知 sid：只丢弃（限流告警），不建会话
}
```

- **库自己重传**：KCP 数据段的重传由 `Kcp.Update` 内部负责（`KCPSession.cs:351`），**不会触发建会话**。但 ⚠️ **握手的 `REQUEST_CONNECT` 文本包没有任何重传** —— 它是一次性 `udp.SendAsync`（`KCPNet.cs:572` → `:725`）。丢了就整轮失败，要自己再调一次 `ConnectServer`。

### 4.3 关于"sid 在几个固定值之间循环"

这正好解释了机理，也供 DLL-only 的读者自查：

- **服务器会话 sid 在"未知 sid 数据包"路径上是直接采用客户端包里的 conv 值的**（`KCPNet.cs:315` 取 `BitConverter.ToUInt32(result.Buffer, 0)`，`session.InitSession(sid, ...)`）。所以"sid 固定集合" = **那几个客户端 KCP 会话的 conv 值**，不是服务器 `GenerateUniqueSessionID` 产出的（后者是 `uuidPart ^ counter`，见 `KCPNet.cs:494-506`，几乎不会重复）。
- 旧版实现里，"未知 sid"分支会**兜底建会话**（键退化成 `ep:`），并且当同一 `ep:` 键挂着别的 sid 时会 `CloseSession()` + 新建 → 两个 conv 在同一键上互相替换，形成「新建 + 关旧」风暴、断开回调 ~5 次/秒、`clientKeyDic` 只有 2 条（`cid:` + `ep:`）且不增长。**与"同一对端反复新建会话 + 关旧会话，sid 在几个固定值之间循环"完全吻合。**
- 当前源码已修掉这条路径（`KCPNet.cs:323-339` 丢弃未知 sid；`HandleConnectRequest` 只复用不替换）。

### 4.4 `ClientId` 的正确用法

```csharp
// KCPNet.cs:523
public string ClientId { get; set; } = Guid.NewGuid().ToString("N");
```

| 问题 | 答案 | 证据 |
|---|---|---|
| **必须**在什么时机设置？ | `StartAsClient` **不用**它（`KCPNet.cs:532-544` 只建 socket）。必须在**调用 `ConnectServer` 之前**设好，因为它是发送时才读的 | `KCPNet.cs:571`（握手）、`KCPNet.cs:701`（道别） |
| 重连要不要复用同一个值？ | **要**。复用同值才能让服务器在换端口后认出"同一人"（`cid:` 键与端点无关）。不复用 = 每次新身份 = 旧会话只能等超时 | `KCPNet.cs:98-103`、`NetSvc.cs:35,182` |
| 库内部会不会重置它？ | **不会**。只在属性初始化时赋一次随机 GUID，之后没有任何写入 | 全库 grep `ClientId`：仅 `:523` 初始化 + `:571,701` 读取 |
| 注意点 | 它是**实例属性**：每次 `new KCPNet<T,K>()` 都会得到一个新的随机 GUID。若每次重连都 new 一个而不显式赋值，身份就变了 | `KCPNet.cs:523` |

### 4.5 未完成握手的会话回收

**有，且是双层的：**

1. **握手期短超时**：握手新建会话时先设 `HandshakeTimeoutMs`，并记入 `_pendingHandshake`：

```csharp
// KCPNet.cs:207-231（节选）
T created = new T();
created.InitSession(newSid, SendUDPMsg, remote, null);   // 暂不加密
created.OnSessionClose = OnServerSessionClose;
created.TimeoutMs = NetConfig.HandshakeTimeoutMs;        // ← 8 秒
lock (sessionDic) { sessionDic[newSid] = created; _pendingHandshake.Add(newSid); }
OnSessionConnected?.Invoke(newSid);
```

收到该会话**第一个数据包（含心跳）**时解除：`EstablishSession`（`KCPNet.cs:239-256`）把 `TimeoutMs` 恢复为 `DefaultTimeoutMs`。

2. **常态超时**：`KCPSession.UpdateAsync` 每轮检查 `now - _lastRecvTime > TimeoutMs` 就 `CloseSession()`（`KCPSession.cs:317-332`），`InitSession` 里已用 `RefreshLastRecvTime()` 设了计时基准（`KCPSession.cs:150`）。

3. **主动离开**：`DISCONNECT:<ClientId>` 立即回收（`KCPNet.cs:294-299` → `CloseSessionByKey`，带**端点校验**，忽略旧端口迟到的道别，`:118-141`）。

> **行为变化提醒（对 DLL-only 读者重要）**：`OnSessionConnected` 现在在**握手成功那一刻**触发（以前是首个数据包到达时）；`OnSessionDisconnected` 也可能因"握手占位会话 8s 超时"而触发一次。

---

## 5. Q4 详细：常量与配置

### 5.1 `NetConfig` 字段属性

```csharp
// Services/NetConfig.cs —— 全部 const
public const int LanSelfBroadcastPort = 29801;   // :12  ← 死配置，全库无引用
public const int LanBroadcastPort     = 29800;   // :18
public const int HostGamePort         = 17666;   // :24
public const int HeartbeatIntervalMs  = 3000;    // :31
public const int DefaultTimeoutMs     = 15000;   // :38
public const int HandshakeTimeoutMs   = 8000;    // :47
```

| 问题 | 答案 |
|---|---|
| `const` 还是 `static readonly`？ | **全是 `const`** |
| 外部写入点？ | **无**（`const` 编译期不可写） |
| 配置文件读取？ | **无**（全库无 `ConfigurationManager` / `.config` 读取；只有 `log4net.config` 作为 CopyToOutput 内容，且 log4net 未被调用） |
| 环境变量？ | **无**（grep `Environment.GetEnvironmentVariable` 零命中） |
| 静态 Setup 入口？ | **无**。`NetConfig` 没有任何 `Setup` 方法 |

⚠️ **`const` 的致命副作用（务必转告 DLL-only 的读者）**：`const` 会被**内联进调用方的 IL**。Unity 代码里写 `NetConfig.HostGamePort`，编译时就把 `17666` 固化进**调用方程序集**了。所以：

- 只替换 `KCPNet.dll` 而**不重新编译 Unity 代码**，调用方仍用旧值；
- 反之若要改端口，必须**同时重编库和调用方**。
- **正确做法**：不要在业务代码里用 `NetConfig.*` 当变量用；端口自己定义为可配置字段（`NetHostSvc.hostPort` 就是这么做的，`NetHostSvc.cs:51` 是 `[SerializeField] private int`）。

### 5.2 心跳裸字节协议

| 项 | 值 | 可调？ | 证据 |
|---|---|---|---|
| 魔数 | `{0x4B,0x43,0x50,0x48,0x42,0x31}` = `"KCPHB1"` | ❌ `private static readonly`，**编译期硬编码，不可改**（改了不兼容） | `KCPSession.cs:59` |
| 发送间隔 | `KCPSession.HeartbeatIntervalMs`（默认 `NetConfig.HeartbeatIntervalMs`=3000） | ✅ **每会话**可改；`<= 0` **可关闭** | `KCPSession.cs:74,98` |
| 超时判定字段 | `_lastRecvTimeUtcTicks`（UtcTicks），每次 `ReciveData` 刷新 | ❌ 私有字段，不可直接改 | `KCPSession.cs:54,126-132,225` |
| 超时阈值 | `KCPSession.TimeoutMs`（默认 15000），`SetTimeout(ms)` 辅助 | ✅ 每会话可改 | `KCPSession.cs:65,78-81` |
| 超时开关 | `KCPSession.EnableTimeoutCheck`（默认 true） | ✅ 可关 | `KCPSession.cs:69,318` |

心跳判定发生在**解密之后、解压之前**，命中就直接 return，不进业务层：

```csharp
// KCPSession.cs:183-194（节选）
byte[] currentAesKey = this.AesKey;
if (currentAesKey != null) localBuffer = EncryptionHelper.Decrypt(localBuffer, currentAesKey);
if (IsHeartbeat(localBuffer)) return;     // "KCPHB1" → 只刷新活跃，不分发
localBuffer = KCPTool.DeCompress(localBuffer);
T msg = Deserialize(localBuffer);
```

### 5.3 官方推荐的配置方式

源码里**没有**全局 Setup。实际推荐（也是唯一可行）的两种写法：

```csharp
// ① 服务端：遍历会话逐个设
foreach (var s in host.GetSessions()) { s.TimeoutMs = 15000; s.HeartbeatIntervalMs = 3000; }
// ② 客户端：直接设唯一那条会话
client.clientSession.TimeoutMs = 15000;
```

（库自己的回归测试就是这么做的：`NetTmp/LanTest/Program.cs:324-325,352-353`）

发现层则用 `LanDiscoveryConfig.Setup(...)`（`LanDiscoveryConfig.cs:71`）。

---

## 6. Q5 详细：非局域网 / 公网路线

### 6.1 专用服务器模式 / `ServerRegionConfig`？

**有一个类，但它不是"模式"，是一张静态数据表，且被核心完全无视。**

```csharp
// Config/ServerRegionConfig.cs:6-52（节选）
namespace KCPNet.Config
{
    public static class ServerRegionConfig
    {
        public static readonly Dictionary<string, RegionConfig> Regions = new Dictionary<string, RegionConfig>()
        {
            ["east-china"] = new RegionConfig {
                ServerIP = "124.222.xxx.xxx",  ServerPort = 8888,
                DataBaseAddress = "Server=sqlserver.com;Database=RTS_EastChina;User Id=sa;Password=xxx;",
                RedisEndPoint = "redis.xxx.com:6379", RedisPassword = "xxx",
                MaxPlayers = 1000, /* ... */ },
            /* ["north-china"] = ... */
        };
        public static RegionConfig Current { get; private set; }
        public static void Initialize(string regionCode) { /* ... */ }
```

事实：
- 它**编译进 DLL**（`Config/` 未排除），但**没有任何 KCPNet 核心代码引用它**（grep `RegionConfig` 只在本文件内命中）。
- 内容是**占位符**（`124.222.xxx.xxx`、`sqlserver.com`、`Password=xxx`）。**这些东西会被打进客户端 DLL**，属于信息泄漏隐患。
- 库里**没有**任何服务器逻辑（无房间管理、无匹配、无玩家账号），`ServerIP/ServerPort` 也只是数据。

### 6.2 RSA→AES 握手是否隐含假设局域网？

**不是"假设局域网"，而是整段被注释停用了 —— 现在是纯明文。**

```csharp
// KCPNet.cs:287-292
// ════════════════════════════════════════════════════════════
// 简化握手（局域网 P2P，暂不加密）：
//   客户端发 "REQUEST_CONNECT" → 服务器分配（或复用）会话ID返回。
//   建立会话时 AesKey=null，KCPSession 会自动跳过加解密（智能降级）。
// 如需恢复 AES 加密，把下面被注释的公钥交换逻辑恢复即可。
// ════════════════════════════════════════════════════════════
```

- 服务端建会话时 `InitSession(..., null)`（`KCPNet.cs:210`），客户端同样 `InitSession(sid, SendUDPMsg, remotePoint, null)`（`KCPNet.cs:640`）。
- 客户端接收端也明说不再收公钥：`// 简化握手：不再接收公钥/交换 AES，直接读取服务器分配的会话ID`（`KCPNet.cs:624`）。
- **但基类是支持加密的**：`AesKey` 属性（`KCPSession.cs:47-51`）、发送前加密（`KCPSession.cs:255-261`）、接收后解密（`KCPSession.cs:183-187`），且 `EncryptionHelper` 是 AES-CBC + HMACSHA256 的完整实现（`EncryptionHelper.cs:9-43,45-106`）。`ServerSecurity`（RSA-2048，`ServerSecurity.cs:13-17`）和 `ClientSecurity`（`ClientSecurity.cs:10-31`）都还在，只是**握手流程没接上**。
- 结论：**加密能力齐备但未接线**；`AesKey == null` 时自动降级为明文（`KCPSession.cs:184`）。对公网直连是**必须补的**。

### 6.3 relay / 服务器端房间列表 / NAT 穿透

| 能力 | 是否存在 | 证据 |
|---|---|---|
| Relay / 中继转发 | ❌ 无 | 全库 grep `Relay` 零命中 |
| 服务器端房间列表 | ❌ 无 | `LanRoomInfo.Source`（`0=局域网, 1=服务器列表`）**只被序列化，全库无任何读取/消费点**（`LanRoomInfo.cs:69,103,164`；grep `.Source` 无逻辑引用） |
| NAT 穿透 / STUN / TURN / 打洞 | ❌ 无 | grep `NAT|Punch|HolePunch|Traversal` 零命中 |
| 心跳保活穿 NAT | ✅ 有（3s 间隔，可维持 UDP 映射） | `KCPSession.cs:96-122` |

即：**这个库的发现与连接能力本质就是"同一个广播域内的 UDP 广播 + 直连"**。

### 6.4 公网 IP 直连：可行，但有 4 个隐藏假设

流程上**没有任何"必须先走广播握手"的前置条件** —— 广播和 KCP 是两套完全独立的东西（广播走 29800 的裸 UDP 文本协议，KCP 走 17666）：

```csharp
// KCPNet.cs:532-544（客户端启动）
public void StartAsClient(string ip, int port)
{
    udp = new UdpClient(0);                              // 随机本地端口
    // ...
    remotePoint = new IPEndPoint(IPAddress.Parse(ip), port);   // ← 坑 1
    Task.Run(ClientRecive, ct);
}
// KCPNet.cs:571-572（握手）
byte[] requestConnectMessage = Encoding.UTF8.GetBytes("REQUEST_CONNECT:" + ClientId);
SendUDPMsg(requestConnectMessage, remotePoint);
```

| # | 隐藏假设 / 前置条件 | 证据 | 影响 |
|---|---|---|---|
| 1 | **`IPAddress.Parse(ip)`：只接受 IP 字面量** | `KCPNet.cs:540` | 传域名/`"localhost"` 直接抛 `FormatException`。必须先自己 `Dns.GetHostAddresses` |
| 2 | **客户端严格校验回包源地址** | `KCPNet.cs:618`：`if (!Equals(remotePoint, result.RemoteEndPoint)) { WarnLog; continue; }` | 多宿主/NAT/多 IP 服务器上，只要回包 source 与目标 ip:port 不完全一致（**含端口**），所有回包被静默丢弃 → 永远连不上 |
| 3 | **握手不重传** | `KCPNet.cs:572` 只发一次；`ConnectServer` 只轮询本地 `clientSession`（`:576-593`） | 公网丢包率下 `REQUEST_CONNECT` 丢失即整轮失败，必须业务层自己重试 `ConnectServer` |
| 4 | **无加密、无认证** | `KCPNet.cs:210,640` 全传 `null` | 明文 + `ClientId` 可伪造 → 公网上任何人可冒充身份、可被封包嗅探 |

另外两个次要假设：
- 服务端 `StartAsServer("0.0.0.0", port)` 是可以直接用于公网的（`KCPNet.cs:78` 用 `IPAddress.Parse` 后 `new UdpClient(IPEndPoint)`）。
- **一个 `KCPNet<T,K>` 实例只能是"服务器"或"客户端"之一**（`StartAsServer` 与 `StartAsClient` 共用同一个 `udp` 字段，`KCPNet.cs:39`）。P2P 双方互为客户端/服务器时，必须在**同一进程内起两个实例**（各占一个端口），库不提供双角色模式。

**示例代码可抄的地方**（都在 `NetTmp/`，不在 DLL 里，因为 `KCPNet.csproj:28` 排除了）：
- 直连：`NetTmp/Client/NetSvc.cs:76-80`（`ConnectDefaultServer`）
- 发现→回连：`NetTmp/Client/NetSvc.cs:88-107`（`ConnectToRoom` → `StartAsClient(room.HostIp, room.HostPort)`）
- 开房+广播：`NetTmp/Client/NetHostSvc.cs:97-132`（`StartHost`）
- 端到端测试（可直接跑）：`NetTmp/LanTest/Program.cs`，命令 `cd NetTmp/LanTest && dotnet run`

---

## 附录 A：关键代码片段

### A1. `HostIp` 无写入点 + 发送端原样透传（Q1）

```csharp
// Services/Lan/LanBroadcaster.cs:92-99
var info = RoomInfoProvider?.Invoke();
if (info != null)
{
    string packet = LanDiscoveryConfig.BuildPacket(LanDiscoveryConfig.AnnounceType, info.ToJson());
    byte[] bytes = Encoding.UTF8.GetBytes(packet);
    var target = new IPEndPoint(LanDiscoveryConfig.BroadcastAddress, LanDiscoveryConfig.BroadcastPort);
    await _udp.SendAsync(bytes, bytes.Length, target);
}   // ← 全程不碰 info.HostIp
```

### A2. 成员端无条件覆盖（Q1）

```csharp
// Services/Lan/LanDiscoverer.cs:154-165
// 以实际发送方地址作为可靠 HostIp（覆盖广播里可能不准的值）
string actualIp = result.RemoteEndPoint.Address.ToString();
info.HostIp = actualIp;
if (!string.IsNullOrEmpty(VersionFilter) && info.Version != VersionFilter) continue;
string key = actualIp + ":" + info.HostPort;
_rooms[key] = new RoomEntry { Info = info, LastSeenTicks = DateTime.UtcNow.Ticks };
```

### A3. 身份键与 clientId 解析（Q3）

```csharp
// KCPNet.cs:98-113
private static string BuildIdentityKey(string clientId, IPEndPoint ep)
{
    return string.IsNullOrEmpty(clientId) ? "ep:" + EndpointKey(ep) : "cid:" + clientId;
}
private static string ParseClientId(string message)
{
    if (string.IsNullOrEmpty(message)) return null;
    int colon = message.IndexOf(':');
    if (colon < 0) return null;
    string cid = message.Substring(colon + 1).Trim();
    if (cid.Length == 0) return null;
    if (cid.Length > 64) cid = cid.Substring(0, 64);
    return cid;
}
```

### A4. 服务端路由：复用 / 建会话 / 丢弃未知 sid（Q3）

```csharp
// KCPNet.cs:301-313
if (message.StartsWith("REQUEST_CONNECT") || message.StartsWith("REQUEST_PUBLIC_KEY"))
{
    string clientId = ParseClientId(message);
    uint sessionId = HandleConnectRequest(clientId, result.RemoteEndPoint);
    byte[] sessionIdBytes = BitConverter.GetBytes(sessionId);
    byte[] responseMessage = new byte[4 + sessionIdBytes.Length];
    Array.Copy(new byte[4], responseMessage, 4);
    Array.Copy(sessionIdBytes, 0, responseMessage, 4, sessionIdBytes.Length);
    SendUDPMsg(responseMessage, result.RemoteEndPoint);   // [0,0,0,0][sid]
    continue;
}
// KCPNet.cs:323-339
T session = null;
lock (sessionDic) { sessionDic.TryGetValue(sid, out session); }
if (session != null) { EstablishSession(sid, session); session.ReciveData(result.Buffer); }
else { DropUnknownSid(sid, result.RemoteEndPoint); }   // 不新建
```

### A5. 心跳与超时（Q4）

```csharp
// KCPSession.cs:59,74,65,69
private static readonly byte[] HeartbeatMarker = new byte[] { 0x4B,0x43,0x50,0x48,0x42,0x31 }; // "KCPHB1"
public long TimeoutMs { get; set; } = NetConfig.DefaultTimeoutMs;          // 15000
public bool EnableTimeoutCheck { get; set; } = true;
public int  HeartbeatIntervalMs { get; set; } = NetConfig.HeartbeatIntervalMs; // 3000, <=0 关闭

// KCPSession.cs:325-331
long elapsedMs = (now.Ticks - lastRecv) / TimeSpan.TicksPerMillisecond;
if (elapsedMs > TimeoutMs)
{
    KCPTool.WarnLog($"会话 {m_sid} 连接超时（{elapsedMs}ms 无数据），关闭会话");
    CloseSession();
    break;
}
```

### A6. 公网直连的 3 个坑（Q5）

```csharp
// KCPNet.cs:540  坑1：只接受 IP 字面量
remotePoint = new IPEndPoint(IPAddress.Parse(ip), port);

// KCPNet.cs:616-622  坑2：严格校验回包源（含端口）
result = await udp.ReceiveAsync();
if (!Equals(remotePoint, result.RemoteEndPoint))
{
    KCPTool.WarnLog("客户端UDP接收的目标数据是非法的");
    continue;
}

// KCPNet.cs:571-572  坑3：握手只发一次，无重传
byte[] requestConnectMessage = Encoding.UTF8.GetBytes("REQUEST_CONNECT:" + ClientId);
SendUDPMsg(requestConnectMessage, remotePoint);   // → udp.SendAsync，fire-and-forget (:725)
```

---

## 附录 B：没找到 / 不确定的部分

1. **`255.255.255.255` 具体从哪块网卡出去** —— 源码里**没有**任何网卡选择逻辑，完全交给 OS 路由表。同理"Windows 是否在所有网卡上发限广播"也是 OS 行为，**源码层面无法判定**。要确定实际行为，只能在 Unity 里打印 `result.RemoteEndPoint`（或抓包）。
2. **`SocketOptionName.MulticastTimeToLive` 对广播报文是否生效** —— 这是 OS 实现细节。源码只表明它设的是 `IP_MULTICAST_TTL`，而非 `IP_TTL`。**不能从源码断言"不跨路由器"**。
3. **手上那版 DLL 是否包含 2026-10-10 的服务端会话重构** —— 无法从源码判断。自检办法见 §0 的警告（对比日志文案）。另：由于 `NetConfig` 是 `const`，DLL 与调用方存在版本耦合，也建议同时核对。
4. **`KCPNet<T,K>` 的 `_serializer` 字段**：声明了但**从未使用**（编译器 `CS0169`，`KCPNet.cs:38`）。所以**无法注入序列化器**——必须通过继承 `KCPSession<T>` 重写 `Serializer` 属性提供（`KCPSession.cs:23`）。若"自己实现 `IKCPMsgSerializer`"，只能挂到 session 子类上，`KCPNet` 本身没有入口。
5. **`log4net` 依赖**：`KCPNet.csproj:22` 引用了 log4net 3.0.2，但库内**没有任何实际调用**（只有 `KCPNet.cs:85,676,682` 的注释）。日志全靠 `KCPTool.LogFunc/ColorLogFunc/WarnLogFunc/ErrLogFunc` 这四个静态委托（`KCPTool.cs:26-29`），不设就走 `Console`。`log4net.config` 只是被复制到输出目录。
6. **其他确认为"死代码/死配置"的项**（已逐个 grep 验证）：
   - `KCPNet.SERVER_RESTART_CODE`（`KCPNet.cs:65`）、`OnServerRestartDetected` 事件（`:524`，编译器 `CS0067`）—— 从未被触发。
   - `NetConfig.LanSelfBroadcastPort`（`NetConfig.cs:12`）—— 无引用。
   - `LanDiscoveryConfig.DiscoveryTimeoutMs`（`LanDiscoveryConfig.cs:53`）—— 只有赋值、无读取。
   - `ServerSecurity.TryGetSessionKey`（`ServerSecurity.cs:66`）—— 无引用；`PurgeSessionKey` 仅在 `KCPNet.cs:381` 被调用。
   - `KCPSession.ModifyConnected`（`KCPSession.cs:431`）—— 无引用。
7. **没有找到任何 README / 文档 / 示例 md** —— 根目录只有 csproj/sln + 源码；唯一的"使用说明"是代码注释和 `NetTmp/Client/NetDemo.cs` 里的长篇注释（`NetDemo.cs:122-178`）。

---

## 附录 C：让这库支持"公网直连 + NAT 穿透"的最小改动建议

按**改动量从小到大**排列，前 4 条是"公网直连"级别，第 5-6 条才是真正的 NAT 穿透。

### C1. 支持域名（1 行级）
`KCPNet.cs:540` 把 `IPAddress.Parse(ip)` 换成：

```csharp
remotePoint = new IPEndPoint(IPAddress.TryParse(ip, out var a)
    ? a : Dns.GetHostAddresses(ip).First(x => x.AddressFamily == AddressFamily.InterNetwork), port);
```

（`netstandard2.0` 下 `Dns.GetHostAddresses` 可用；注意 IL2CPP 需联网权限。）

### C2. 放宽回包源地址校验（1 行级，公网必需）
`KCPNet.cs:618` 改成只比 IP（或维护一个"可接受的源 IP 集合"），别比端口：

```csharp
if (!result.RemoteEndPoint.Address.Equals(remotePoint.Address)) { /* ... */ continue; }
```

理由：多宿主服务器/NAT 环境下回包 source 端口常与目标端口不同，当前实现会**静默丢光所有回包**。

### C3. 握手加确认重传（小改动）
`ConnectServer`（`KCPNet.cs:571-592`）在轮询循环里每 ~300ms 重发一次 `REQUEST_CONNECT:<ClientId>`。**服务端已经是幂等的**（`HandleConnectRequest`，`KCPNet.cs:162`），重发只会复用同一 sid，安全无副作用。

### C4. 启用加密（中等，代码已现成，只需接线）
服务端在 `HandleConnectRequest` 里恢复 `ServerSecurity.GetPublicKey()`（`ServerSecurity.cs:32`）的公钥下发分支，客户端用 `ClientSecurity.EncryptAesKeyWithRsa`（`ClientSecurity.cs:24`）上传 AES key，服务端 `DecryptAesKey`（`ServerSecurity.cs:40`）后把 key 传进 `InitSession(..., aesKey)`（`KCPNet.cs:210` / `:640` 的 `null` 换成真 key）。`EncryptionHelper` 与 `KCPSession` 的加解密分支**无需改动**（`KCPSession.cs:183-187,255-261`）。

> 注：公网绝对不要留明文，且 `ClientId` 需要配一个签名/口令，否则可被冒充。

### C5. 双向角色（P2P 直连的前提，中等偏大）
当前 `KCPNet<T,K>` 一个实例只能是一种角色（`udp` 字段复用，`KCPNet.cs:39`）。两个公网节点要互发，最小做法是**同进程起两个实例**：`A=StartAsServer(myPort)` + `B=StartAsClient(peerIp, peerPort)`，各自绑定不同端口。若想单实例双角色，需要把 `udp` 拆成 `serverUdp`/`clientUdp` 并让路由按"源端点"分流 —— 属于结构性改动。

### C6. 打洞 + 中继（真正的公网 P2P，大改动）
库**完全没有**这部分，需要新增：

- 一个**轻量 rendezvous/信令服务**（可复用 `ServerRegionConfig` 的那套字段作为配置载体，`ServerRegionConfig.cs:74-89`）：双方注册 → 互换各自的**公网 ip:port**（即服务端看到的 `RemoteEndPoint`）。
- 双方拿到对端公网 endpoint 后，**同时**向对方发包（C5 的双向角色 + C2 的放宽校验）以打通 NAT 映射；靠现有 3s 心跳（`KCPSession.cs:96-122`）维持映射。
- 对称型 NAT 下打洞会失败 → 需要 **relay**：库内加一个"转发 socket"（收到某 sid 的包就原样转发给另一个 endpoint）。这部分没有任何现成代码，得从零写。
- 房间列表：`LanRoomInfo.Source == 1` 的语义（`LanRoomInfo.cs:69`）目前是空转的，需要由信令服务真正实现"返回 `List<LanRoomInfo>`"。

---

## 附录 D：给 Unity 项目方的行动建议

**Q1 是当前最可能的故障源。** 库不填 `HostIp`、成员端又无条件用包源地址覆盖，所以"成员连不上"时请优先在成员侧打印 `GetRooms()` 里每个 `room.HostIp:HostPort`：

- 如果它不是房主真正的可达局域网 IP（而是 ZeroTier / `172.x` / `127.0.0.1`），那就与库的会话逻辑无关，是**广播出口网卡**的问题；
- 对策：不依赖广播得到的 `HostIp`，改为在业务层显式配置/输入房主 IP 后直接 `StartAsClient(ip, 17666)`。

**"sid 在几个固定值之间循环 + 反复新建/关旧会话"**：确认 DLL 版本（§0 自检），旧版需替换为包含 `HandleConnectRequest` / `DropUnknownSid` 的新版 DLL。
