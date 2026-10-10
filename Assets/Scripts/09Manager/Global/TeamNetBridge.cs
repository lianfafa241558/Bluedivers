using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using FPSGame.GameData;
using FPSGame.Gameplay;
using FPSGame.Managers;
using FPSGame.Net;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 【网络 ↔ 局内玩家表】的桥 —— 本类是全工程**唯一**同时看得见
    /// <c>FPSGame.Net</c>（房间/资料）与 <see cref="TeamManager"/>（<see cref="PlayerData"/>）的地方。
    ///
    /// <para>▍各司其职（不要互相越界）：</para>
    /// <list type="bullet">
    ///   <item><b>02_Net（NetSvc/NetHostSvc/NetRoomFlow）</b>：持"**谁在场**"——连接、会话、房间参数、
    ///         成员列表与资料表；只发事件，**从不写 `TeamManager`**（它看不见 `PlayerData`）。</item>
    ///   <item><b>TeamManager（本层）</b>：持"**这一局谁上场 + 用什么角色/装备**"；只被本类写，自己不碰 socket。</item>
    ///   <item><b>本类</b>：订阅 <see cref="NetRoomFlow"/> 的事件做映射与开局复现；上行（资料/准备/战备/强化）
    ///         统一走 <see cref="NetRoomFlow"/> 的 Send*（房主权威：房主本地应用 + 广播，成员发给房主）。
    ///         <b>数据落点也在这里</b>（先把权威值写进 `TeamManager.players`，再可选地叫醒 UI）——
    ///         因为本类是常驻的，而 `BridgeSys`/`ArmamentWnd` 只在舰桥场景存在。</item>
    /// </list>
    ///
    /// <para>▍⚠ 两套下标的换算（关键，别混）：</para>
    /// <list type="bullet">
    ///   <item><b>网络下标</b> = 房主视角（`PlayerInfo[]` 里房主固定第 0 位）；</item>
    ///   <item><b>本地下标</b> = 自己视角（`TeamManager.players[0]` **永远是本机自己**，
    ///         因为 `ArmamentWnd` 是按"自己在最上面"写死的）；</item>
    ///   <item>跨端同步消息因此都带 <c>Sid</c>（权威标识），本类用 <see cref="IdOfSid"/> 换算成
    ///         `PlayerData.id`，再用 <see cref="TeamManager.IdToIndex"/> 得本地下标。</item>
    /// </list>
    ///
    /// <para>▍安装点：<c>GameRoot/NetRoot</c> 下（net 常驻组件统一收在这个一级子物体里）。
    /// <c>GameRootBase</c> 的 <c>Init</c>/<c>UnInit</c> 按 <c>I_GlobaManager</c> 扫「根 + 直接子物体」
    /// 两级、不递归 ⇒ 一级子物体安全，再深就会被静默漏掉。</para>
    /// </summary>
    public class TeamNetBridge : MonoBehaviour, I_GlobaManager
    {
        /// <summary>房主在 <c>PlayerData.id</c> 里的映射值（避开本机 UID：UID 恒为正数）。</summary>
        private const int HostDataId = -1;

        /// <summary>本地 id → 上一次的"已准备"，用于把名单同步里的变化转成 UI 事件。</summary>
        private readonly Dictionary<int, bool> _lastReady = new Dictionary<int, bool>();

        /// <summary>已处理过的本局局号（幂等键：KCP 重传 / 房主重复点按钮会重复到达同一个 <c>TaskConfirmNtf</c>）。
        /// <para>▍用 <c>MatchId</c> 而不是种子：下一局的 MatchId 必然不同 ⇒ **不需要"回大厅清键"**（同种子会撞车）。</para></summary>
        private int _handledMatchId;

        /// <summary>本局的 <c>Transition</c> 是否已经广播过（房主侧去重：一局只广播一次）。
        /// <para>▍复位点：<c>GameState</c> 一旦到 <c>Ready</c>（= 开始配置新一局）。</para></summary>
        private bool _transitionSent;

        public void Init()
        {
            NetRoomFlow.OnPlayerList += HandlePlayerList;
            NetRoomFlow.OnTaskConfirm += HandleTaskConfirm;
            NetRoomFlow.OnTransition += HandleTransition;
            NetRoomFlow.OnArmamentSync += HandleArmamentSync;
            NetRoomFlow.OnBoosterSync += HandleBoosterSync;
            NetRoomFlow.OnLoadoutSync += HandleLoadoutSync;
            NetRoomFlow.OnJoinResult += HandleJoinResult;
            GlobalEventBus.OnGameStateChange += HandleGameStateChange;
        }

        public void UnInit()
        {
            NetRoomFlow.OnPlayerList -= HandlePlayerList;
            NetRoomFlow.OnTaskConfirm -= HandleTaskConfirm;
            NetRoomFlow.OnTransition -= HandleTransition;
            NetRoomFlow.OnArmamentSync -= HandleArmamentSync;
            NetRoomFlow.OnBoosterSync -= HandleBoosterSync;
            NetRoomFlow.OnLoadoutSync -= HandleLoadoutSync;
            NetRoomFlow.OnJoinResult -= HandleJoinResult;
            GlobalEventBus.OnGameStateChange -= HandleGameStateChange;
            _lastReady.Clear();
        }

        /// <summary>
        /// 入房结果：成功就**把本机资料（含角色）上报一次**。
        ///
        /// <para>▍为什么必须做：资料原先只在"换角色"时上报（<c>BridgeRoleManager.SetPlayerRole</c>），
        /// 入房后双方资料都停留在**占位资料**（只有名字、`RoleName` 为空）⇒
        /// 盟友实体虽然建出来了，但 <c>NetFriendBridge.AttachRoleModel</c> 要求 `RoleName` 非空
        /// ⇒ **没有身体模型，表现就是"双方看不见彼此"**。</para>
        /// </summary>
        private void HandleJoinResult(bool ok, string reason)
        {
            if (!ok) return;

            var flow = NetRoomFlow.Instance;
            if (flow != null && flow.IsHost) return;   // 房主的资料由 Ready 那条路推（见 HandleGameStateChange）
            SendSelfProfile();
            SendSelfLoadout();   // ★ 入房时把**本机配置**（武器改装 + 载具改装）也报一次
        }

        // ==================== 名单同步（网络 → TeamManager） ====================

        /// <summary>
        /// 房主广播的名单 + 资料 ⇒ 重建本地队伍表。
        /// <para>顺序规则：**本机自己在 0**，其余按房主的顺序（这样 `ArmamentWnd` 的"自己在最上面"成立，
        /// 而房主视角的下标只在网络消息里出现，靠 Sid 换算）。</para>
        /// </summary>
        private void HandlePlayerList(PlayerInfo[] infos, PlayerProfile[] profiles)
        {
            var team = TeamManager.Instance;
            if (team == null || infos == null || infos.Length == 0) return;

            // ★ 开局后名册冻结：**只更新资料字段，不重建列表**。
            //   战斗中"掉线/退房"同样会触发房主广播名单（不挑场合），而重建会重排 players 下标 ——
            //   但 ArmamentWnd 的 armamentRoot.GetChild(i) ↔ players[i]、
            //   以及 task.BattleData 的长度（开局按 players.Count 建）都依赖"顺序与数量不变"。
            if (FPSGame.Data.BattleState.IsStartBattle)
            {
                ApplyListFieldsOnly(team, infos, profiles);
                return;
            }

            var order = new List<PlayerData>(infos.Length);

            // ① 自己永远占 0（本地存档是"我"的真相来源，远端资料不覆盖自己）
            if (team.Self != null) order.Add(team.Self);

            // ② 其余按房主顺序
            for (int i = 0; i < infos.Length; ++i)
            {
                var info = infos[i];
                if (info == null) continue;

                if (IsSelfEntry(info)) continue;   // 自己已经加过

                var profile = profiles != null && i < profiles.Length ? profiles[i] : null;
                int id = IdOfSid(info.Sid);

                var data = team.players.Find(item => item != null && item.id == id);
                if (data == null)
                {
                    data = new PlayerData { id = id, isBot = false, isEmpty = false };
                }
                data.isEmpty = false;
                ApplyInfo(data, info, profile);
                order.Add(data);
            }

            team.ReplacePlayers(order);   // ★ 先落表：Reindex 会把每个 PlayerData.index 归位

            // ★ 名册变化 ⇒ 叫醒战备界面：有人退出时**那一行必须消失**，名册重排后各行身份也可能换人。
            //   （桥常驻、窗口只在舰桥场景 ⇒ 这里只发通知，窗口自己判"我显示过没有"）
            BridgeSys.Instance?.ReceiveRosterChanged();

            // ★ 再通知 UI —— 顺序不能颠倒：新成员的 PlayerData 刚建出来时 index 还是默认 0
            //   （= 自己那一行！），先通知会让 ArmamentWnd 去点亮别人的行；Reindex 之后 index 才是真下标。
            for (int i = 0; i < infos.Length; ++i)
            {
                var info = infos[i];
                if (info == null) continue;

                // 每一条都通知，**包括房主自己**：转场要求"所有玩家都就绪"，房主与成员同一口径（2026-10-07 用户口径）。
                // 房主那份的 IsReady 现在来自它自己的点击（`NetHostSvc._hostReady`），不再写死 true。
                int localIndex = LocalIndexOf(team, info.Sid);   // 认得"两端各自的自己"（自己不在表里时靠 sid 认）
                if (localIndex < 0) continue;

                var profile = profiles != null && i < profiles.Length ? profiles[i] : null;
                NotifyReadyChanged(team.players[localIndex], info, profile);
            }

            // ★ 有人进/出 ⇒ 让场景里已存在的载具重刷一次外观（新玩家进房那一刻，房主那份配置可能刚到；
            //   载具侧自带"无变化跳过"，名单每次同步都调也不会白烧）
            VehicleCustomState.RefreshAll();
        }

        /// <summary>
        /// 局内名单同步的"只更新字段"分支：按 <c>id</c> 找到既有 <see cref="PlayerData"/> 改资料，
        /// **不动顺序、不动数量**（局内不新增玩家 —— 既有"禁止进人"，也有房主侧的名册冻结）。
        /// </summary>
        private void ApplyListFieldsOnly(TeamManager team, PlayerInfo[] infos, PlayerProfile[] profiles)
        {
            for (int i = 0; i < infos.Length; ++i)
            {
                var info = infos[i];
                if (info == null || IsSelfEntry(info)) continue;

                var data = team.players.Find(item => item != null && item.id == IdOfSid(info.Sid));
                if (data == null) continue;   // 局内不新增玩家

                var profile = profiles != null && i < profiles.Length ? profiles[i] : null;
                ApplyInfo(data, info, profile);
                NotifyReadyChanged(data, info, profile);
            }
        }

        /// <summary>把一条网络信息（+可选资料）写进本地 <see cref="PlayerData"/>。</summary>
        private static void ApplyInfo(PlayerData data, PlayerInfo info, PlayerProfile profile)
        {
            if (data == null) return;

            if (!string.IsNullOrEmpty(info.PlayerName)) data.name = info.PlayerName;

            if (profile == null) return;   // 只有名单（旧版本房主 / 成员还没上报资料）⇒ 保留已有角色信息

            if (!string.IsNullOrEmpty(profile.Name)) data.name = profile.Name;
            // ⚠ 角色名空 = 占位资料：不能覆盖，否则 ArmamentWnd/GameEndWnd 会拿空串去 CreatPrefab
            if (!string.IsNullOrEmpty(profile.RoleName))
            {
                data.roleName = profile.RoleName;
                data.roleLevel = profile.RoleLevel;
                data.roleExp = profile.RoleExp;
                if (profile.Weapons != null && profile.Weapons.Length > 0) data.weapons = profile.Weapons;
                if (profile.Upgrades != null && profile.Upgrades.Length > 0) data.Upgrades = profile.Upgrades;
            }
            if (profile.Airdrop != null && profile.Airdrop.Length == 4) data.airdrop = profile.Airdrop;
            data.boosterId = profile.BoosterId;
        }

        /// <summary>准备状态变化 ⇒ 通知舰桥 UI（没有 BridgeSys 的场景静默跳过）。</summary>
        private void NotifyReadyChanged(PlayerData data, PlayerInfo info, PlayerProfile profile)
        {
            if (data == null) return;

            bool ready = profile != null ? profile.IsReady : (info != null && info.IsReady);
            data.isReady = ready;      // ★ 先落数据：窗口后开时按它渲染（下面那条去重只挡"重复通知 UI"）

            bool last;
            if (_lastReady.TryGetValue(data.id, out last) && last == ready) return;

            _lastReady[data.id] = ready;
            var bridge = BridgeSys.Instance;
            if (bridge != null) bridge.ReceivePlayerReady(data.index, ready);
        }

        // ==================== 开局（房主广播 → 成员复现同一局） ====================

        /// <summary>
        /// 成员侧：按房主广播的**落本局配置**（进 Ready）—— ⚠ **不加载场景**。
        ///
        /// <para>▍阶段位置（用户 2026-10-06 口径）：Bridge 选任务 →（本方法收到 <c>TaskConfirmNtf</c>）Ready
        /// → 所有人就位 → Armament（舰桥选战备）→ **Transition 才加载战斗场景**
        /// （由 <see cref="HandleTransition"/> 或本机 <c>ArmamentWnd</c> 那条链驱动）。</para>
        ///
        /// <para>▍顺序（关键，别调换）：</para>
        /// <list type="number">
        ///   <item><b>幂等</b>：同一 <c>MatchId</c> 只处理一次（KCP 重传 / 房主重复点按钮）；
        ///         新成员入房时房主会**补发**同一条 <c>TaskConfirmNtf</c> ⇒ 也靠这个幂等键挡住重复；</item>
        ///   <item><b>校验</b>：地图必须本地存在；任务内容**以房主下发的 <c>Cfg</c> 为准**
        ///         （跨窗口时本地表的下标不可靠）；<c>TaskFingerprint</c> 仅作诊断告警，
        ///         只在房主没下发内容（旧版）时才恢复"不一致就中止"的硬校验；</item>
        ///   <item><b>种子与配置</b>：<c>TeamManager.SetSeed</c>（喂 <c>RandomUtils</c> 静态流）
        ///         + <c>SetTask(…, ntf.Seed)</c>（喂 <c>TaskState.Seed</c> → <c>BattleRandom</c> / 地形流 / 谜题流）；
        ///         ⚠ <c>SetTask</c> 末尾会把 <c>GameState</c> 推到 <c>Ready</c>，与房主一致；
        ///         至此**双方都还在舰桥**，名单也还没冻结（Ready/Armament 期间仍可进人）。</item>
        /// </list>
        /// <para>▍⚠ 名单不需要在这里落表：入房与每次变更都会走 <see cref="HandlePlayerList"/>
        /// （<c>ReplacePlayers</c>，**不碰种子** —— 种子只在确认本局配置时定一次）。</para>
        /// </summary>
        private void HandleTaskConfirm(TaskConfirmNtf ntf)
        {
            if (ntf == null) return;

            var flow = NetRoomFlow.Instance;
            if (flow != null && flow.IsHost) return;   // 房主本地已经在 ConfirmTask 里开过了

            var team = TeamManager.Instance;
            var task = TaskManager.Instance;
            if (team == null || task == null) return;

            // ① 幂等：同一局只处理一次（房主会给"选完任务之后才入房"的新人补发这条，同一个人也可能重复收到）
            if (ntf.MatchId != 0 && ntf.MatchId == _handledMatchId) return;
            _handledMatchId = ntf.MatchId;

            // ★ 本局配置**以内容为准**（2026-10-06）：房主随消息下发了"选中项 TaskCfg"的内容 ⇒
            //   成员**不再依赖本地任务表的下标**。那张表是各端本地按时间窗口生成的
            //   （跨窗口 / 刚刷过表时同一个 TaskIndex 可能指向另一个任务 —— 这正是之前
            //   "任务表不同步，已中止开局"的成因）。⇒ 只要本地有这张**地图**（加载场景要用），
            //   任务内容就完全复现；"即使这个任务在任务表上已经不存在了"也照样能打
            //   （见 TaskManager.SetTask 的 remoteCfg 分支）。
            bool hasCfg = ntf.Cfg != null && ntf.Cfg.Main >= 0;

            // ② 配置校验：地图必须本地存在；任务**下标**只在"没下发内容"时才要求合法
            bool badConfig = task.MapData == null
                             || string.IsNullOrEmpty(ntf.MapName)
                             || !task.MapData.ContainsKey(ntf.MapName)
                             || (!hasCfg && (ntf.TaskIndex < 0 || ntf.TaskIndex >= task.TaskCount));
            if (badConfig)
            {
                Debug.LogError($"[TeamNetBridge] 收到开局广播但配置对不上：地图={ntf.MapName} 任务={ntf.TaskIndex}");
                return;
            }

            // ②b 任务表指纹 —— ⚠ 已从"硬校验"**降级为诊断告警**：
            //   下发内容后，两端表不同**不影响本局**（成员按内容复现），所以不能再中止开局，
            //   否则后进者 / 跨窗口的玩家会被永久拒之门外。
            //   仅当房主**没**下发内容（旧版房主）时，才保留原来的硬校验。
            if (ntf.TaskFingerprint != 0)
            {
                int localFingerprint = task.TaskFingerprint(ntf.MapName, ntf.TaskIndex);
                if (localFingerprint != ntf.TaskFingerprint)
                {
                    if (!hasCfg)
                    {
                        Debug.LogError($"[TeamNetBridge] 任务表不同步，已中止开局！本机指纹 {localFingerprint} ≠ " +
                                       $"房主 {ntf.TaskFingerprint}（地图 {ntf.MapName} 任务 {ntf.TaskIndex}）" +
                                       " ⇒ 请重启游戏后重新入房");
                        return;
                    }
                    Debug.LogWarning($"[TeamNetBridge] ⚠ 本地任务表与房主不同（本机 {localFingerprint} ≠ " +
                                     $"房主 {ntf.TaskFingerprint}，地图 {ntf.MapName} 任务 {ntf.TaskIndex}），" +
                                     "已按房主下发的配置**内容**复现本局（不影响联机）");
                }
            }

            // ③ ⚠ **先收界面、再 SetTask**（与房主的顺序一致，别调换）：`SetTask` 会把 `GameState` 推到
            //     `Ready`，而大厅那张 GameState 状态表在 Ready 时调 `BridgeWnd.DisplayTask()` 播任务面板动画；
            //     若此刻本端还开着选图/房间列表（WindowState = UI ⇒ BridgeWnd 隐藏），那次调用会因为
            //     "animator 所在物体未激活"**静默失败**，任务面板就停在 Idle（2026-10-06 打包版实测）。
            WindowRegistry.SetState(WndType.SelectMap, false);   // 收起选图界面（房主那边是自己 Close 的）

            // ③b 种子 + 本局配置（两条随机流共用同一个整数种子）
            team.SetSeed(ntf.Seed);
            int difficulty = Mathf.Clamp(ntf.Difficulty, 0, Tool.EnumLenght<DifficultyEnum>() - 1);
            task.SetTask(ntf.MapName, ntf.TaskIndex, (DifficultyEnum)difficulty,
                ntf.ExtraDiff ?? new int[4], ntf.PlayMode, ntf.Seed, ntf.Cfg);   // ★ 内容优先（见 ② / SetTask）

            // ③c ★ 房主**可能已经进到 Armament**（后进房的人）：SetTask 只会把本机推到 Ready
            //     ⇒ 按房主随消息带下来的阶段补一步，否则新人在"等人"、其他人已经在"配战备"（2026-10-10 用户报）。
            var hostPhase = (GameStateEnum)ntf.Phase;
            if (hostPhase == GameStateEnum.Armament && GameRoot.GameState != GameStateEnum.Armament)
            {
                GameRoot.GameState = GameStateEnum.Armament;
                Debug.Log("[TeamNetBridge] 房主已在 Armament ⇒ 本机立刻跟到同一阶段（不再停在 Ready 干等）");
            }

            // ⚠ 到此为止：**不要**在这里 AsyncLoadScene。加载归 Transition ——
            //   房主广播 TransitionNtf → HandleTransition 把本机 GameState 推到 Transition
            //   → 各自大厅既有的 GameStateController(state:8) → TransSceneController.StartLoad()。
            //   （旧实现是"收到配置就加载" ⇒ 成员在 Ready 阶段就跳进战场、房主还留在舰桥，见 2026-10-06 的修正）
            Debug.Log($"[TeamNetBridge] 已确认本局配置：局号{ntf.MatchId} {ntf.MapName} 难度{difficulty} " +
                      $"任务{ntf.TaskIndex} 种子{ntf.Seed} —— 仍在舰桥 Ready，等所有人就位/准备");
        }

        // ==================== 阶段：Transition（进战斗） ====================

        /// <summary>
        /// 阶段变化（房主/成员共用入口，三件事各自判归属）：
        /// <list type="number">
        ///   <item><c>→ Ready</c>（选完任务）：复位"本局已广播 Transition"标记；**房主**顺带推一次自己的资料
        ///         （<c>HostProfile</c> 为空时成员端看房主是"没有身体"的盟友）；</item>
        ///   <item><c>→ Armament</c>（所有人就位，仅房主）：**什么都不做** —— 收人窗口一直开到
        ///         <c>Transition</c>（2026-10-10 用户口径：Armament 阶段要能进人）。
        ///         ⚠ 别在这里关闸/冻名单：关闸会让晚来的人被回"人员已就位"；冻名单会让
        ///         <c>ArmamentWnd</c> 的"全员就绪"判定拿不到准备状态；</item>
        ///   <item><c>→ Transition</c>（Armament 结束、开始加载战斗，仅房主）：广播 <see cref="NetHostSvc.NotifyTransition"/>。</item>
        /// </list>
        /// <para>▍为什么 Transition 要房主广播：<c>ArmamentWnd</c> 全员就绪后是**每端各自**把 GameState
        /// 推到 Transition（动画事件 <c>FinishReady()</c>）；房主补一发权威通知，避免某一端卡在舰桥。
        /// 只有房主广播，且每局只广播一次（<see cref="_transitionSent"/>，GameState 回到 Ready 时复位）。</para>
        /// </summary>
        private void HandleGameStateChange(GameStateEnum exit, GameStateEnum entry)
        {
            var flow = NetRoomFlow.Instance;

            // ★ 房主：把"本房现在处于哪个阶段"同步进 NetHostSvc —— 它随"本局配置"下发，
            //   后进房的人凭它立刻切到同一阶段（否则会在 Ready 干等，而其他人已经在配战备）。
            if (flow != null && flow.IsHost) NetHostSvc.Instance?.SetRoomPhase((int)entry);

            if (entry == GameStateEnum.Ready)
            {
                _transitionSent = false;   // 新一局开始配置 ⇒ 允许再广播一次
                // 房主：把自己的资料（角色/武器/强化）也推一次 ⇒ 写进 HostProfile 并广播，
                // 成员端才能给房主建出"有身体"的盟友（否则只有名字占位资料）。
                if (flow != null && flow.IsHost)
                {
                    SendSelfProfile();
                    SendSelfLoadout();   // ★ 同上：房主自己的配置也要有（成员端按它装模组/渲染载具）
                }
                return;
            }

            if (flow == null || !flow.IsHost) return;   // 下面两件事都只有房主做

            if (entry == GameStateEnum.Armament)
            {
                // ★ **不再关闸**（2026-10-10 用户口径：Armament 阶段其他玩家要能进来）。
                //   原来在这里 NetHostSvc.CloseJoin() ⇒ 一进 Armament 入房就被回"人员已就位"，
                //   晚来的人只能干看着；而真正该拦的是"本局已开始"—— 那道闸在 NotifyTransition
                //   （_rosterFrozen，过渡到战斗场景时冻结名单 + 广播 InGame）里，已经够用。
                //   ▍晚来的人为什么能跟得上：房主入房时**补发** TaskConfirmNtf（含 Phase = Armament）
                //   ⇒ TeamNetBridge.HandleTaskConfirm 会把他本机也推到 Armament（见那条注释），
                //   名单继续照常广播 ⇒ ArmamentWnd 能拿到"谁已就绪"。
                return;
            }

            if (entry != GameStateEnum.Transition || _transitionSent) return;

            _transitionSent = true;
            NetHostSvc.Instance?.NotifyTransition();   // 广播 + 冻结名单
        }

        /// <summary>
        /// 成员侧：房主说"进 Transition" ⇒ 本机也把 <c>GameState</c> 推到 <c>Transition</c>。
        ///
        /// <para>▍⚠ **不要**在这里自己调 <c>AsyncLoadScene</c>：大厅里的 <c>GameStateController</c>
        /// （<c>state: 8</c> = Transition）已经挂了 <c>TransSceneController.StartLoad()</c>
        /// （转场音乐 + 加载 + <c>BattleManager.Creat(true)</c>）⇒ 自己再调一次就是**双加载**。</para>
        /// <para>▍本机若已通过 <c>ArmamentWnd</c> 的全员就绪动画走到 Transition，这次赋值会被
        /// <c>GameRoot.GameState</c> setter 的"值没变就不发事件"判掉，天然幂等。</para>
        /// </summary>
        private void HandleTransition(int matchId)
        {
            var flow = NetRoomFlow.Instance;
            if (flow != null && flow.IsHost) return;                    // 房主自己是源头
            if (GameRoot.GameState == GameStateEnum.Transition) return; // 已经走到了（ArmamentWnd 那条路）

            Debug.Log($"[TeamNetBridge] 房主通知进入战斗（局号 {matchId}）⇒ 本机切 Transition（加载走大厅既有链）");
            GameRoot.GameState = GameStateEnum.Transition;
        }

        // ==================== 上行（战备 / 强化 → 权威转发 → 本地 UI） ====================

        private void HandleArmamentSync(uint sid, int networkIndex, int airdropId, int slotIndex)
        {
            var team = TeamManager.Instance;
            if (team == null) return;

            int localIndex = LocalIndexOf(team, sid);
            if (localIndex < 0)
            {
                Debug.LogWarning($"[TeamNetBridge] 收到战备同步但找不到玩家 sid={sid}（忽略）");
                return;
            }

            // ★ 先落数据、再叫 UI：桥是常驻的，而战备界面（BridgeSys/ArmamentWnd）只在舰桥场景
            //   ⇒ 数据落点必须在这里，否则"不在舰桥时收到的同步"会被静默丢掉
            //   （战斗中 BattleManager/空投都读 players[i].airdrop）。
            var data = team.players[localIndex];
            if (data != null && slotIndex >= 0)
            {
                if (data.airdrop == null || data.airdrop.Length != 4) data.airdrop = new int[4];
                if (slotIndex < data.airdrop.Length) data.airdrop[slotIndex] = airdropId;
            }

            // 界面刷新（不在舰桥时 Instance 为 null，静默跳过——数据已经落好了）
            BridgeSys.Instance?.ReceivePlayerSelectArmament(localIndex, airdropId, slotIndex);

            if (IsMySid(sid)) SendSelfProfile();   // 自己改的 ⇒ 把最新资料推给房主，供后来者/结算读
        }

        private void HandleBoosterSync(uint sid, int networkIndex, int boosterId)
        {
            var team = TeamManager.Instance;
            if (team == null) return;

            int localIndex = LocalIndexOf(team, sid);
            if (localIndex < 0) return;

            var data = team.players[localIndex];
            if (data != null) data.boosterId = boosterId;   // ★ 同 HandleArmamentSync：数据落点归桥

            BridgeSys.Instance?.ReceivePlayerSelectTeamEnhance(localIndex, boosterId);

            if (IsMySid(sid)) SendSelfProfile();
        }

        // ==================== 对外的小工具（BridgeRoleManager 等改完本地资料后调用） ====================

        /// <summary>
        /// 【上行】把本机 <c>TeamManager.Self</c> 打包成资料发给房主（房主则写进 `HostProfile` 并广播）。
        /// 角色切换 / 战备 / 强化变化后都应调一次。
        /// </summary>
        public static void SendSelfProfile()
        {
            var flow = NetRoomFlow.Instance;
            var team = TeamManager.Instance;
            if (flow == null || team == null || team.Self == null) return;
            flow.SendProfile(ToProfile(team.Self));
        }

        /// <summary><see cref="PlayerData"/> → 网络资料。</summary>
        private static PlayerProfile ToProfile(PlayerData data)
        {
            if (data == null) return null;
            return new PlayerProfile
            {
                Name = data.name,
                RoleName = data.roleName,
                RoleLevel = data.roleLevel,
                RoleExp = data.roleExp,
                Weapons = data.weapons,
                Upgrades = data.Upgrades,
                Airdrop = data.airdrop,
                BoosterId = data.boosterId,
            };
        }

        // ==================== 配置（武器改装：档位 + 模组；载具改装）====================

        /// <summary>
        /// 【上行】把本机**配置**（武器改装档位/模组 + 载具改装）发给房主（房主则落表 + 广播全体）。
        /// <para>▍调用点两处：① 入房成功 / 房主进 Ready（见 <see cref="HandleJoinResult"/>、
        /// <see cref="HandleGameStateChange"/>）；② <c>SelectRoleWnd</c> / <c>VehicleWnd</c> 关窗且确实改过时。</para>
        /// <para>▍实现上**每次都从本机存档重算**（而不是读 <c>TeamManager.Self</c>）：
        /// 那两个窗口是直接改存档对象的，Self 上那份可能还是旧值。</para>
        /// </summary>
        public static void SendSelfLoadout()
        {
            var flow = NetRoomFlow.Instance;
            var team = TeamManager.Instance;
            if (flow == null || team == null || team.Self == null) return;

            RefreshSelfWeaponConfig(team.Self);
            SyncLocalSid();
            flow.SendLoadout(ToLoadout(team.Self));
        }

        /// <summary>按存档重算"自己那份"武器选择 / 改装 / 模组（窗口改完存档后调，保持 Self 与存档一致）。</summary>
        private static void RefreshSelfWeaponConfig(PlayerData self)
        {
            if (self == null || string.IsNullOrEmpty(self.roleName)) return;
            var arch = ArchivesData_SO.Current;
            if (arch == null) return;

            self.weapons = arch.GetWeaponSelect(self.roleName);
            self.Upgrades = arch.GetWeaponUpgrade(self.roleName);
            self.weaponModules = arch.GetWeaponModules(self.roleName);
        }

        /// <summary><see cref="PlayerData"/> → 配置消息（载具部分直接从存档读全量）。</summary>
        private static PlayerLoadoutMsg ToLoadout(PlayerData data)
        {
            if (data == null) return null;
            return new PlayerLoadoutMsg
            {
                Upgrades = data.Upgrades,
                Modules = data.weaponModules,
                Vehicles = BuildVehicleDtos(),
            };
        }

        /// <summary>把存档里的全部载具改装打包成 DTO（按 vehicleName 键控，不依赖保存顺序）。</summary>
        private static VehicleCustomDto[] BuildVehicleDtos()
        {
            var arch = ArchivesData_SO.Current;
            if (arch == null) return null;

            var list = new List<VehicleCustomDto>();
            arch.VehicleCustomDic.ForEach((name, v) =>
            {
                if (string.IsNullOrEmpty(name) || v == null) return;
                list.Add(new VehicleCustomDto
                {
                    VehicleName = name,
                    LeftWeaponIndex = v.leftWeaponIndex,
                    RightWeaponIndex = v.rightWeaponIndex,
                    SkinIndex = v.skinIndex,
                    BlendIndex = v.blendIndex,
                    BlendScale = v.blendScale.RawFloat,
                });
            });
            return list.Count > 0 ? list.ToArray() : null;
        }

        /// <summary>网络载具配置 → 存档对象（供 <see cref="VehicleCustomState"/> 的分 sid 表使用）。</summary>
        private static Dictionary<string, ArchivesData_SO.ArchVehicleData> ToVehicleMap(VehicleCustomDto[] dtos)
        {
            if (dtos == null || dtos.Length == 0) return null;

            var map = new Dictionary<string, ArchivesData_SO.ArchVehicleData>(dtos.Length);
            for (int i = 0; i < dtos.Length; ++i)
            {
                var d = dtos[i];
                if (d == null || string.IsNullOrEmpty(d.VehicleName)) continue;
                map[d.VehicleName] = new ArchivesData_SO.ArchVehicleData
                {
                    leftWeaponIndex = d.LeftWeaponIndex,
                    rightWeaponIndex = d.RightWeaponIndex,
                    skinIndex = d.SkinIndex,
                    blendIndex = d.BlendIndex,
                    blendScale = d.BlendScale,
                };
            }
            return map.Count > 0 ? map : null;
        }

        /// <summary>本机会话 sid（房主 = 0）→ 决定 <see cref="VehicleCustomState.TryGet"/> 走本机存档还是同步表。</summary>
        private static void SyncLocalSid()
        {
            var flow = NetRoomFlow.Instance;
            VehicleCustomState.SetLocalSid(flow != null && !flow.IsHost ? flow.SelfSid : 0u);
        }

        /// <summary>
        /// 【下行】收到配置（房主广播，含房主自己那条）：落表 + 写进对应 <see cref="PlayerData"/> + 让盟友重装武器。
        /// <para>▍载具配置走 <see cref="VehicleCustomState"/>（按 sid）：渲染侧（<c>BattleApplyVehicleData</c>）
        /// 在"有人上车"时按驾驶者 sid 取 —— 载具外观因此跟着持有者走。</para>
        /// </summary>
        private void HandleLoadoutSync(PlayerLoadoutMsg m)
        {
            if (m == null) return;

            SyncLocalSid();
            VehicleCustomState.Set(m.Sid, ToVehicleMap(m.Vehicles));   // 载具配置：按 sid 落"数据自持"表
            // ★ 配置到了 ⇒ 立刻让场景里已存在的载具按新配置重刷（进战斗场景后配置才到、或中途改配置都靠它）
            VehicleCustomState.RefreshAll();

            var team = TeamManager.Instance;
            if (team == null) return;

            int localIndex = LocalIndexOf(team, m.Sid);
            if (localIndex < 0) return;

            var data = team.players[localIndex];
            if (data != null)
            {
                if (m.Upgrades != null && m.Upgrades.Length > 0) data.Upgrades = m.Upgrades;
                if (m.Modules != null && m.Modules.Length > 0) data.weaponModules = m.Modules;
            }

            // 改装变了 ⇒ 让盟友实体按新配置重装武器（角色没变时 AttachRoleWeapons 不会自己触发）
            if (!IsMySid(m.Sid)) NetFriendBridge.Instance?.RefreshLoadout(m.Sid, m);
        }

        /// <summary>sid → 本地 <c>PlayerData.id</c>（房主 0 ⇒ <see cref="HostDataId"/>；成员 ⇒ -(sid+1)）。</summary>
        private static int IdOfSid(uint sid)
        {
            return sid == 0u ? HostDataId : -((int)sid + 1);
        }

        /// <summary>sid → 本地下标。⚠ 两端各有一个"自己"的特例：房主的 sid 恒为 0；**成员自己**在表里占
        /// <c>players[0]</c>，但它的 <c>id</c> 是本地存档 UID（名单重建时"自己"不进表）⇒ 用 <see cref="IdOfSid"/>
        /// 永远落空。漏掉这个特例的后果：成员收到房主转发回来的**自己**的战备/强化同步时找不到人
        /// （`收到战备同步但找不到玩家 sid=…（忽略）`），表现为"自己选的战备自己看不见，房主却看得见"（2026-10-07 打包端实测）。</summary>
        private static int LocalIndexOf(TeamManager team, uint sid)
        {
            if (team == null) return -1;

            // 判"自己"的口径与 IsMySid 一致：房主看 sid==0，成员看 sid==自己的会话号
            var flow = NetRoomFlow.Instance;
            if (flow != null && (flow.IsHost ? sid == 0u : (sid != 0u && sid == flow.SelfSid))) return team.SelfIndex;

            return team.IdToIndex(IdOfSid(sid));
        }

        /// <summary>这条名单项是不是"我自己"。</summary>
        private static bool IsSelfEntry(PlayerInfo info)
        {
            var flow = NetRoomFlow.Instance;
            if (info == null || flow == null) return false;

            if (flow.IsHost) return info.IsHost;                       // 房主：名单里的房主条目 = 我
            if (info.IsHost) return false;                             // 成员：房主不是我
            return info.Sid != 0u && info.Sid == flow.SelfSid;         // 成员：sid 匹配 = 我
        }

        /// <summary>这条同步消息是不是"我自己发的"（决定要不要回推资料）。</summary>
        private static bool IsMySid(uint sid)
        {
            var flow = NetRoomFlow.Instance;
            if (flow == null) return false;
            return flow.IsHost ? sid == 0u : (sid != 0u && sid == flow.SelfSid);
        }
    }
}
