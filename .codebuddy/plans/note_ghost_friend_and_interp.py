# -*- coding: utf-8 -*-
"""向 2026-10-06 记忆日志追加：强杀客户端 ghost 盟友 + 盟友闪现位移（插值缓冲）两个修复。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-06.md"

NOTE = """

## ✅ 修「成员强杀后房主端 friend 不消失」+「friend 快速闪现位移」

### ① ghost 盟友：强杀客户端，房主收不到任何通知
- 事实：KCP/UDP 无连接；成员直接关掉进程后房主侧收不到"我走了"，库内会话超时 `TimeoutMs` **默认 15000ms** 也要等 15 秒；
  而 `PingReqMsg/PingRspMsg`（`CmdId.PingReq/Rsp = 1001/1002`）**定义了却从没被任何代码使用**（没人心跳）⇒
  舰桥阶段（位姿同步只在战斗里跑）两端长时间零业务报文时，健康连接还可能被库的超时检查误杀。
- 修法：
  1. `NetSvc`：每 `heartbeatInterval=2s` 发 `PingReq`（`SendHeartbeat()`，`Time.unscaledDeltaTime` 计时、`IsConnected` 才发）；
     注册 `PingRsp` → 算 `RttMs`（Rsp 只回 Id+ServerTime，靠本地记的发送时刻相减）。
  2. `NetHostSvc`：注册 `PingReq` → 回 `PingRsp`（只对已入房的 sid）；`Update()` 出队每条消息时刷新
     `_lastSeen[sid]`（主线程，无需锁）；`EvictIdleMembers()`（`memberIdleTimeout=8s`）⇒ 超时按"离开"走
     `HandleMemberLeft`（清表 + 广播名单 ⇒ 各端桥清掉盟友实体）+ `HostSession.CloseSession()`（库公开 API）。
  3. 顺手补 `_poses.Remove(sid)`：原来成员离开只清 `_players/_profiles`，**位姿表不清** ⇒ "幽灵位姿"会一直跟着每批快照下发。
- 已核对：库自带 `EnableTimeoutCheck=True` / `TimeoutMs=15000` / `_lastRecvTimeUtcTicks` + `_heartbeatLock`（有超时与保活机制），
  所以我们的心跳是"双向都需要"的那块：既让库不误杀，又让房主 8 秒内就能判定强退。

### ② 闪现位移：`FriendController` 的插值缓冲只有两个点
- 真因：`RenderDelay(0.15s)` **远大于**快照间隔（房主下行 15Hz = 67ms）⇒ 渲染时刻永远落在最新一段的**左端之前**，
  插值参数被夹到 0 ⇒ 渲染位置＝上一包的位置，每个包到达时**整段跳一次**（用户描述的"快速的闪现位移"）。
- 数值仿真（同一时间轴：8m/s、15Hz 包、60fps 帧，见对话）：旧算法**每帧最大位移 0.533m**（正好一包距离）、新算法 **0.133m**（= 8/60）；
  平均都是 0.132 ⇒ 只改抖动、不改总量（无漂移）。
- 修法：改成**多点缓冲**（`BufferSize=6`，`_bufPos/_bufYaw/_bufTime` + `Push/ShiftOne/UpdateRenderPose`）：
  先丢"整段都在渲染时刻之前"的老点，再在 `[t0,t1]` 内 `Lerp`/`LerpAngle`；断流则停在最后已知位姿；容量 `Mathf.Max(4, BufferSize)`。
- ⚠ 另一处必须改：吸附判定原来和**当前 transform** 比距离 ⇒ 渲染位置本来滞后 1~3m，跑动会被误判成传送、频繁清缓冲
  （越清越闪）⇒ 改为和**上一个快照**比。
- 验证：离线编译 `02_Net/06_Gameplay/09_Managers/10_UI` 0 错误；Unity 重编译 Console 0 error；反射断言
  `SendHeartbeat/OnPingRsp/RttMs`、`OnPingReq/EvictIdleMembers/memberIdleTimeout/_lastSeen`、`BufferSize/UpdateRenderPose/_bufPos` 全在位。
- ⏳ 待双人实测：① 强杀成员客户端 ⇒ 房主 8 秒内 `[NetHostSvc] 成员 sid=… 已 8s 无任何消息` + `[NetFriendBridge] 移除盟友实例`；
  ② 战斗中盟友移动连续、无跳变。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
