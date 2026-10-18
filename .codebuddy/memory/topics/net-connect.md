# 主题 · 联机 · 跨机「搜不到 / 连不上」排查口径 + 版本门槛

> 由 `net-flow.md` §8 拆出（2026-10-18）。传输层与库侧诊断见 `net.md`。

## 8. 跨机「搜不到 / 连不上」排查口径（2026-10-13 定稿）
按序查，**不要查 `HostIp` 的值对不对**（它由库填 = 收包 source IP，跨机天然可达；理由见 `net.md §3.0`）：
1. 房主端有没有 `房主服务器已启动，监听端口 <port>`（`NetHostSvc.cs:277`）+ 频道 `开房失败…端口未释放`；
2. 成员端 `[NetRoomFlow] 回连房主 <ip>:<port>`（`NetRoomFlow.cs:398`）—— 只看**有没有**这一行、端口对不对；
3. **Windows 防火墙入站放行**（跨机第一大原因：首次弹窗必须勾「专用网络→允许」；打包版每次重弹）；路由器/AP 的**客户端隔离**（展会/公司网常见）；
4. 关掉虚拟网卡（VMware / ZeroTier / Hyper-V vEthernet / Radiance 等）—— 广播出口网卡选错的最大来源；
5. 列表里**同一房间出现两行** = 房主多网卡（广播与 DISCOVER 应答的出口网卡不同）⇒ 现在由 `ServerListPanel.QueryRooms` 按「房主合成名 + 端口」去重并打 `LogWarning` 留证。
兜底永远是**手动直连 `IP:端口`**（绕开整个发现层，见 `ServerListPanel.ActivateDirect`）。

### 8.1 版本门槛（2026-10-13 用户口径）
- **不用库的 `VersionFilter`**（它会让不同版本直接**看不见**房间，全工程至今 0 设置）。改为**照常可见**、只在**入房那一刻**拦：`NetRoomFlow.Join` 开头调 `RoomMeta.VersionBlockTip(room)`，非 null ⇒ `FinishJoin(false, tip)`。
- 文案两句（用户点名）：对方更旧 ⇒「对方版本不同，无法联机」；自己更旧 ⇒「请更新版本后再联机」；两句都把版本号打出来便于现场对账。
- 比较口径：`RoomMeta.CompareVersion` 按 `'.'` 切段比数值、缺段补 0；**任一段不是纯数字 / 对面没带 Version ⇒ `Unknown` = 放行**（别误拦老房主与手动直连）。
- 快速匹配侧：`ServerListPanel.FindJoinableRoom` 会**跳过**版本不兼容的房间（可看不可连）。
- ⚠ 版本号来自 `Application.version` = `ProjectSettings.bundleVersion`（现 **0.0.1**）⇒ 内测要区分版本必须**改 bundleVersion 再各出一份包**。
