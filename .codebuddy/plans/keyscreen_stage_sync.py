# -*- coding: utf-8 -*-
"""
联机 · 终端(KeyScreen)阶段同步 + 撤离选点(#4) + 巡逻队/炼油注释(#2)

用法：python .codebuddy/plans/keyscreen_stage_sync.py            # 只校验（dry run）
      python .codebuddy/plans/keyscreen_stage_sync.py --apply    # 校验通过才落盘

设计（按用户 2026-10-11 拍板）：
  #1 KeyScreen.SetStage 广播（驱动端上报 / 其余端只应用）  → 落码
  #2 巡逻队没有房主权威                                     → 先跳过，只加 TODO 注释
  #3 炼油的就地随机                                        → 不动
  #4 MissionEvacuate 选点：按触发者位置；无触发者按房主位置  → 落码

特性：
  ① 行尾(BOM/CRLF/LF)自适应：按"逻辑行"匹配，写回时还原原行尾与 BOM
  ② 每个锚点必须命中**恰好 1 次**，任一不符则整体不写入（两阶段提交）
  ③ 幂等：整组补丁以 key 判定，已存在则跳过（可重复执行）
"""
import io
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def P(*parts):
    return os.path.join(ROOT, *parts)


class File:
    def __init__(self, rel):
        self.rel = rel
        self.path = P(*rel.split('/'))
        raw = io.open(self.path, 'rb').read()
        self.bom = raw[:3] == b'\xef\xbb\xbf'
        if self.bom:
            raw = raw[3:]
        self.crlf = b'\r\n' in raw
        self.lines = raw.decode('utf-8').replace('\r\n', '\n').split('\n')
        self.orig = '\n'.join(self.lines)
        self.dirty = False

    def has(self, key):
        return key in self.orig

    def patch(self, key, anchor, new, count=1):
        """把 anchor 这段连续行替换成 new；命中数必须 == count。"""
        a = anchor.split('\n')
        n = new.split('\n')
        hits = [i for i in range(len(self.lines) - len(a) + 1)
                if self.lines[i:i + len(a)] == a]
        assert len(hits) == count, \
            '[锚点失配] %s key=%s 命中=%d 期望=%d' % (self.rel, key, len(hits), count)
        i = hits[0]
        self.lines[i:i + len(a)] = n
        self.dirty = True

    def write(self):
        if not self.dirty:
            return False
        text = '\n'.join(self.lines)
        if self.crlf:
            text = text.replace('\n', '\r\n')
        raw = text.encode('utf-8')
        if self.bom:
            raw = b'\xef\xbb\xbf' + raw
        io.open(self.path, 'wb').write(raw)
        return True


def new_file(rel, text, crlf=False, bom=False):
    """新建文件（默认 LF/noBOM，与同目录多数文件一致）。"""
    path = P(*rel.split('/'))
    assert not os.path.exists(path), '文件已存在：%s' % rel
    if crlf:
        text = text.replace('\n', '\r\n')
    raw = text.encode('utf-8')
    if bom:
        raw = b'\xef\xbb\xbf' + raw
    io.open(path, 'wb').write(raw)


# =====================================================================
# 补丁定义
# =====================================================================
FILES = {}


def F(rel):
    if rel not in FILES:
        FILES[rel] = File(rel)
    return FILES[rel]


KEY = 'KeyScreenStageNtf'      # 整组补丁的幂等键（#1）
KEY4 = 'GetHostPos'            # #4 的幂等键

# ---------------------------------------------------------------- 1. CmdId
if not F('Assets/Scripts/07NetGame/CmdId.cs').has(KEY):
    F('Assets/Scripts/07NetGame/CmdId.cs').patch(
        'CmdId',
        '''        public const int SceneDestructibleNtf = 4043;''',
        '''        public const int SceneDestructibleNtf = 4043;

        /// <summary>双向：**按键终端(KeyScreen)的阶段推进**（键 = 终端家具的 <c>Furniture_Attached.SyncId</c>）。
        /// <para>▍为什么单独一条：阶段推进原本纯本地（<c>OnUpdateStage</c> 只在本地抛），而任务的**中间阶段标志**
        /// （黑盒子的 <c>startDefense</c>、炼油的 Start/Repair、撤离的 Wait、<c>KeyScreenContGo</c> 的显隐）
        /// 全靠它 ⇒ 两端阶段一分叉，后续任务行为就各走各的（2026-10-11 用户实测）。只传"推进到第几阶段"。</para>
        /// <para>▍驱动口径：只有**驱动端**（操作者所在那台 / 无人阶段时房主）上报，其余端只应用 ——
        /// 判定唯一收口在 <c>KeyScreen.IsStageDriver</c>，桥是 09 的 <c>NetKeyScreenBridge</c>。</para></summary>
        public const int KeyScreenStageNtf = 4046;''')

# ------------------------------------------------------------ 2. SyncMsg DTO
if not F('Assets/Scripts/07NetGame/Msg/SyncMsg.cs').has('KeyScreenStageMsg'):
    F('Assets/Scripts/07NetGame/Msg/SyncMsg.cs').patch(
        'SyncMsg',
        '''        /// <summary>波序（丢过期包 / 确认这一波还在）。</summary>
        [Key(0)] public int WaveIndex;
        [Key(1)] public float X;
        [Key(2)] public float Y;
        [Key(3)] public float Z;
    }
}''',
        '''        /// <summary>波序（丢过期包 / 确认这一波还在）。</summary>
        [Key(0)] public int WaveIndex;
        [Key(1)] public float X;
        [Key(2)] public float Y;
        [Key(3)] public float Z;
    }

    /// <summary>按键终端(KeyScreen)阶段推进（双向）。<c>SyncId</c> = 终端家具的 <c>Furniture_Attached.SyncId</c>
    /// （与 4036 同口径 ⇒ 两端日志可直接对照）。
    /// <para>只传"推进到第几阶段"：阶段本身的语义（UI 显隐 / 谜题初始化 / 任务标志）由各端跑同一条本地
    /// <c>SetStage</c> 逻辑，不需要过网。</para></summary>
    [MessagePackObject]
    public class KeyScreenStageMsg
    {
        /// <summary>权威标识：推进者的会话号（房主 = 0）⇒ 接收端据此丢掉"自己发出去的那条回环"。</summary>
        [Key(0)] public uint Sid;
        /// <summary>终端家具的跨端稳定键。</summary>
        [Key(1)] public int SyncId;
        /// <summary>目标阶段号。</summary>
        [Key(2)] public int Stage;
    }
}''')

# --------------------------------------------------------- 3. NetRoomFlow（4 处）
if not F('Assets/Scripts/07NetGame/NetRoomFlow.cs').has(KEY):
    f = F('Assets/Scripts/07NetGame/NetRoomFlow.cs')
    f.patch(
        'flow.event',
        '''        /// <summary>【成员侧】房主下发的追击波次中心（~2Hz）⇒ 本端只跟随，不自己算"最近玩家"。</summary>
        public static event Action<WaveCenterMsg> OnWaveCenter;''',
        '''        /// <summary>【成员侧】房主下发的追击波次中心（~2Hz）⇒ 本端只跟随，不自己算"最近玩家"。</summary>
        public static event Action<WaveCenterMsg> OnWaveCenter;

        /// <summary>【双向】按键终端(KeyScreen)阶段推进（键 = 终端家具 SyncId）⇒ 其余端只应用、不自行推进。</summary>
        public static event Action<KeyScreenStageMsg> OnKeyScreenStage;''')

    f.patch(
        'flow.register',
        '''            MessageCenter.Register<SceneDestructibleMsg>(CmdId.SceneDestructibleNtf, HandleSceneDestructibleNtf);''',
        '''            MessageCenter.Register<SceneDestructibleMsg>(CmdId.SceneDestructibleNtf, HandleSceneDestructibleNtf);
            // 按键终端阶段推进（驱动端上报 / 房主转发全体）
            MessageCenter.Register<KeyScreenStageMsg>(CmdId.KeyScreenStageNtf, HandleKeyScreenStageNtf);''')

    f.patch(
        'flow.unregister',
        '''            MessageCenter.Unregister(CmdId.SceneDestructibleNtf);''',
        '''            MessageCenter.Unregister(CmdId.SceneDestructibleNtf);
            MessageCenter.Unregister(CmdId.KeyScreenStageNtf);''')

    f.patch(
        'flow.send',
        '''        private void HandleFurnitureOperateNtf(FurnitureOperateMsg m)
        {
            if (m != null) OnFurnitureOperate?.Invoke(m);
        }''',
        '''        private void HandleFurnitureOperateNtf(FurnitureOperateMsg m)
        {
            if (m != null) OnFurnitureOperate?.Invoke(m);
        }

        /// <summary>终端阶段推进：驱动端上报 / 房主转发全体（走向由 09 侧的 NetKeyScreenBridge 决定）。</summary>
        public void SendKeyScreenStage(int syncId, uint sid, int stage)
        {
            var msg = MessageCenter.Pack(CmdId.KeyScreenStageNtf,
                new KeyScreenStageMsg { SyncId = syncId, Sid = sid, Stage = stage });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else NetSvc.Instance?.SendMsg(msg);
        }

        private void HandleKeyScreenStageNtf(KeyScreenStageMsg m)
        {
            if (m != null) OnKeyScreenStage?.Invoke(m);
        }''')

# ------------------------------------------------------ 4. KeyScreen（CRLF）
if not F('Assets/Scripts/06Gameplay/Interactable/KeyScreen.cs').has('OnLocalStageChanged'):
    f = F('Assets/Scripts/06Gameplay/Interactable/KeyScreen.cs')
    f.patch(
        'ks.using',
        '''using static FPSGame.WndTools.WndRootTool;
using System.Linq;
using UnityEngine.Events;

using FPSGame.Utils;
using FPSGame.Attributes;''',
        '''using static FPSGame.WndTools.WndRootTool;
using System;
using System.Linq;
using UnityEngine.Events;

using FPSGame.Core.Interface;   // IsValidMono（接口引用判空）
using FPSGame.Game;          // ActorsManager（驱动端判定要认"本机玩家"）
using FPSGame.Utils;
using FPSGame.Attributes;''')

    f.patch(
        'ks.fields',
        '''    public event UnityAction<int> OnUpdateStage;
    public UnityEvent OnComple;''',
        '''    public event UnityAction<int> OnUpdateStage;
    public UnityEvent OnComple;

    /// <summary>成员端 = true：终端阶段**不自行推进**，只应用驱动端下发的阶段（同 MissionBase.RemoteDriven 口径）。
    /// <para>由 09 侧 NetKeyScreenBridge 在 Install / 房主掉线时置位复位。</para></summary>
    public static bool RemoteDriven;

    /// <summary>本端推进了一个阶段（**只由驱动端抛**）⇒ 09 侧 NetKeyScreenBridge 订阅后上报。
    /// <para>参数：(终端家具 SyncId, 新阶段)。SyncId = 0 表示本端终端没有身份源 ⇒ 不参与同步。</para></summary>
    public static event Action<int, int> OnLocalStageChanged;''')

    f.patch(
        'ks.setstage',
        '''    public void SetStage(int stage)
    {
        if (this == null) return;
        nowStage = stage;''',
        '''    /// <summary>本终端家具的跨端稳定键（= <c>Furniture_Attached.SyncId</c>；没有身份源时 0 = 不参与同步）。</summary>
    public int SyncId => furn ? furn.SyncId : 0;

    /// <summary>
    /// 本端是不是"阶段驱动端"：有人操作 ⇒ 只有操作者所在那台驱动；无人操作（Load/Wait 这类自动阶段）⇒ 房主驱动。
    /// <para>▍单机（<see cref="RemoteDriven"/> = false）恒为 true ⇒ 行为与改造前完全一致。</para>
    /// <para>⚠ 非驱动端调用 <see cref="SetStage"/> 会被丢弃（等同步过来）：两端各推各的必然分叉。</para>
    /// </summary>
    private bool IsStageDriver()
    {
        if (owner != null)
        {
            var self = ActorsManager.Player;
            return self.IsValidMono() && self.gameObject == owner;
        }
        return !RemoteDriven;   // 无人操作：房主驱动（单机时 RemoteDriven 也是 false）
    }

    /// <summary>推进阶段（**驱动端**的唯一入口）：非驱动端调用会被丢弃。</summary>
    public void SetStage(int stage)
    {
        if (this == null) return;
        if (!IsStageDriver()) return;

        int syncId = SyncId;
        SetStageCore(stage);
        OnLocalStageChanged?.Invoke(syncId, stage);
    }

    /// <summary>【联机】应用远端下发的阶段：跳过驱动门（远端已判定过），且**不抛上报事件** ⇒ 不回环。</summary>
    public void ApplyRemoteStage(int stage)
    {
        if (this == null) return;
        if (stage == nowStage) return;      // 已经在这一阶段：幂等
        SetStageCore(stage);
    }

    /// <summary>阶段推进的实际逻辑（设值 + 派发 <see cref="OnUpdateStage"/> + 阶段 UI 初始化）。</summary>
    private void SetStageCore(int stage)
    {
        nowStage = stage;''')

    f.patch(
        'ks.update',
        '''        if (!FPSGame.Utils.Tool.In(nowStage,-1,procedure.Count)) return;
        var nowPro = nowProcedure;''',
        '''        if (!FPSGame.Utils.Tool.In(nowStage,-1,procedure.Count)) return;
        // ⚠ 非驱动端只应用下发过来的阶段、不自己推进（两端各自的计时器/本机键盘输入必然分叉）
        if (!IsStageDriver()) return;
        var nowPro = nowProcedure;''')

# --------------------- 4b. KeyScreen：更新那条"没有同步"的过时注释（#1 之后不再成立）
if not F('Assets/Scripts/06Gameplay/Interactable/KeyScreen.cs').has('驱动端上报、其余端应用'):
    F('Assets/Scripts/06Gameplay/Interactable/KeyScreen.cs').patch(
        'ks.diag',
        '''        // 联机诊断：阶段推进**目前没有任何同步**（只有"家具被操作"走 4036）⇒ 两端阶段会分叉，
        // 而任务的中间阶段标志（如 MissionBlackBox.startDefense）正是靠这条事件来的。''',
        '''        // 联机诊断：阶段推进的同步 = 驱动端上报、其余端应用（见 09 侧 NetKeyScreenBridge 与 IsStageDriver）。
        // 本行两端都会打 ⇒ 对照两端 "→ s/N" 就能看出是在自己推（驱动端）还是收来的（应用端）。''')

# ------- 4c. 修正（2026-10-11）：IsValidMono 的扩展在 FPSGame.Core.Interface（首次落盘写成了 FPSGame.Core）
if not F('Assets/Scripts/06Gameplay/Interactable/KeyScreen.cs').has('using FPSGame.Core.Interface;'):
    F('Assets/Scripts/06Gameplay/Interactable/KeyScreen.cs').patch(
        'ks.coreif',
        '''using FPSGame.Core;          // IsValidMono（接口引用判空）''',
        '''using FPSGame.Core.Interface;   // IsValidMono（接口引用判空）''')

# ------------------------------------- 5. NetFurnitureBridge：改用共享回环门
if not F('Assets/Scripts/09Manager/Battle/NetFurnitureBridge.cs').has('NetApplyGuard'):
    f = F('Assets/Scripts/09Manager/Battle/NetFurnitureBridge.cs')
    f.patch(
        'fb.field',
        '''        /// <summary>回环门：true = 正在"重放远端交互"。此时家具 <c>Operate()</c> 会再派发一次
        /// <c>FurnitureOperate</c> ⇒ 必须挡住它被当成"本机操作"再上报一次。</summary>
        static bool _applyingRemote;
''',
        '''''')

    f.patch(
        'fb.install',
        '''            if (installed) return;
            installed = true;
            _applyingRemote = false;
            _friendBridge = null;''',
        '''            if (installed) return;
            installed = true;
            _friendBridge = null;''')

    f.patch(
        'fb.uninstall',
        '''            if (!installed) return;
            installed = false;

            GlobalEventBus.OnFurnitureOperate -= OnLocalOperate;
            NetRoomFlow.OnFurnitureOperate -= OnRemoteOperate;
            _friendBridge = null;''',
        '''            if (!installed) return;
            installed = false;

            GlobalEventBus.OnFurnitureOperate -= OnLocalOperate;
            NetRoomFlow.OnFurnitureOperate -= OnRemoteOperate;
            _friendBridge = null;''')

    f.patch(
        'fb.onlocal',
        '''            if (_applyingRemote)
            {
                // 远端重放触发的（重放时家具又派发了一次事件）⇒ 本地已经执行过，别再上报''',
        '''            if (NetApplyGuard.Applying)
            {
                // 远端重放触发的（重放时家具又派发了一次事件）⇒ 本地已经执行过，别再上报''')

    f.patch(
        'fb.onremote',
        '''            _applyingRemote = true;
            try
            {
                furn.ApplyRemoteOperate(remoteUser);
            }
            finally
            {
                _applyingRemote = false;
            }''',
        '''            NetApplyGuard.Begin();
            try
            {
                furn.ApplyRemoteOperate(remoteUser);
            }
            finally
            {
                NetApplyGuard.End();
            }''')

    # 类注释里补一句"门是共享的"
    f.patch(
        'fb.doc',
        '''        /// <summary>sid → 盟友实例 的查询来源（常驻 GameRoot 上），惰性解析并缓存。</summary>
        static NetFriendBridge _friendBridge;''',
        '''        /// <summary>sid → 盟友实例 的查询来源（常驻 GameRoot 上），惰性解析并缓存。</summary>
        static NetFriendBridge _friendBridge;

        // ⚠ 回环门用的是**跨桥共享**的 NetApplyGuard（不是本类私有字段）：应用终端阶段时也会走到
        //   furn.Operate() ⇒ 会派发本类订阅的 FurnitureOperate ⇒ 各持一道门挡不住那条回环。''')

# --------------------------------------------------- 6. WaveManager（挂桥 + TODO）
if not F('Assets/Scripts/09Manager/Battle/WaveManager.cs').has('NetKeyScreenBridge'):
    f = F('Assets/Scripts/09Manager/Battle/WaveManager.cs')
    f.patch(
        'wm.install',
        '''            NetActionBridge.Install();        // 标记点位 / 呼叫凯伊''',
        '''            NetActionBridge.Install();        // 标记点位 / 呼叫凯伊
            NetKeyScreenBridge.Install();     // 按键终端阶段推进（驱动端上报 / 其余端只应用）''')

    f.patch(
        'wm.uninstall',
        '''            NetActionBridge.Uninstall();''',
        '''            NetActionBridge.Uninstall();
            NetKeyScreenBridge.Uninstall();''')

    f.patch(
        'wm.patrol',
        '''        public List<GameObject> CreatPatrol(Vector3 pos)
        {''',
        '''        public List<GameObject> CreatPatrol(Vector3 pos)
        {
            // ⚠ TODO（2026-10-11 用户拍板"先跳过"，仅留注释）：巡逻队**没有房主权威** —— 成员端调到这里会
            //   本地造单位，而 Actor.Awake 会顺手分配 NetId ⇒ 与房主下发的号段撞号（移错位 / 错杀）。
            //   修法 = 同 CreatWave 的闸门（成员端直接 return，等房主的生成事件），两处要一起改。''')

# ------------------------------------------- 7. MissionEvacuateMobile（#4 选点）
if not F('Assets/Scripts/06Gameplay/Mission/Evacuate/MissionEvacuateMobile.cs').has(KEY4):
    F('Assets/Scripts/06Gameplay/Mission/Evacuate/MissionEvacuateMobile.cs').patch(
        'ev.getpos',
        '''        /// <summary>
        /// 取发起撤离的玩家位置：优先用发起交互的玩家，其次用离当前撤离点最近的玩家，
        /// 最后退回当前撤离点(快速模式没有玩家近距离交互时)。
        /// </summary>
        private Vector3 GetEvacuateRequestPos(GameObject user)
        {
            if (user) return user.transform.position;

            IActor nearest = null;
            float best = float.MaxValue;
            foreach (var player in ActorsManager.Players)
            {
                if (!player.IsValidMono()) continue;
                float d = Vector3.Distance(player.Pos, areaPoint);
                if (d < best)
                {
                    best = d;
                    nearest = player;
                }
            }
            return nearest != null ? nearest.Pos : areaPoint;
        }''',
        '''        /// <summary>
        /// 取"用来挑撤离点"的位置：**触发者优先**（发起撤离的那个人在哪，就按他的位置挑最近的一处）；
        /// 没有触发者（快速模式）则一律按**房主**位置。
        /// <para>▍为什么要换（2026-10-11）：原来第二档是"离当前撤离点最近的玩家" —— 那是**本机视角**的判定，
        /// 两端的玩家集合/位姿都不同 ⇒ 各挑各的撤离点，随后 area/areaPoint、运输机落点、通知凯伊的点全分叉。
        /// 换成"同一个触发者 / 房主"后，两端的输入位置同源 ⇒ 选出同一处。</para>
        /// </summary>
        private Vector3 GetEvacuateRequestPos(GameObject user)
        {
            if (user) return user.transform.position;
            return GetHostPos();
        }

        /// <summary>房主的位置：本机是房主 ⇒ 本机玩家；成员 ⇒ 房主的盟友实例（<see cref="FriendController.NetSid"/> == 0）。
        /// <para>都没找到（房主掉线 / 盟友实例还没建出来）⇒ 退回本机玩家位置。</para></summary>
        private Vector3 GetHostPos()
        {
            var players = ActorsManager.Players;
            for (int i = 0; i < players.Count; ++i)
            {
                var p = players[i];
                if (!p.IsValidMono()) continue;
                if (p.gameObject.TryGetComponent(out FriendController friend) && friend.NetSid == 0u) return p.Pos;
            }
            var self = ActorsManager.Player;
            return self.IsValidMono() ? self.Pos : areaPoint;
        }''')

# ------------------------------------- 8. MissionOilRefining（#2 只留 TODO 注释）
if not F('Assets/Scripts/06Gameplay/Mission/Main/MissionOilRefining.cs').has('先跳过'):
    F('Assets/Scripts/06Gameplay/Mission/Main/MissionOilRefining.cs').patch(
        'oil.repair',
        '''        private void EnterRepair()
        {
            errorPipes.Clear();''',
        '''        private void EnterRepair()
        {
            // ⚠ TODO（2026-10-11 用户拍板"先跳过"，仅留注释）：本段在**成员端也会执行**（任务 Tick 不受
            //   RemoteDriven 门控），而 errorTimes / 挑哪几根管 / 巡逻队落点都吃各端自己的 BattleRandom 消费位
            //   ⇒ 两端坏的管不同、巡逻队也各刷一支。根治 = ①坏管集合由房主下发 ②巡逻队走房主权威
            //   （见 WaveManager.CreatPatrol 的 TODO）。
            errorPipes.Clear();''')

# =====================================================================
# 新建文件
# =====================================================================
NEW_GUARD = '''namespace FPSGame.Managers
{
    /// <summary>
    /// 联机同步桥的"正在应用远端消息"共享门（计数式，可嵌套）。
    ///
    /// <para>▍为什么要共享（而不是每个桥各自持一个 bool）：应用远端消息时常常会**派发本地的玩法事件**
    /// （家具 <c>Operate()</c> → <c>GlobalEventBus.FurnitureOperate</c>），而本端的桥也订阅了同一个事件（用于上报）
    /// ⇒ 不加门就会把"远端来的"当成"本机产生的"再发一次（回环）。跨桥的那条回环单一扇门挡不住 ——
    /// 典型例子：应用终端阶段（<c>NetKeyScreenBridge</c>）会走到 <c>furn.Operate()</c>（<c>NetFurnitureBridge</c> 的订阅点）。</para>
    ///
    /// <para>▍用法：<c>Begin(); try { ...应用... } finally { End(); }</c>。计数式 ⇒ 桥里再调桥也安全。</para>
    /// </summary>
    internal static class NetApplyGuard
    {
        static int _depth;

        /// <summary>true = 当前正在应用远端消息（此时本地事件不许再上行）。</summary>
        internal static bool Applying => _depth > 0;

        internal static void Begin() => ++_depth;

        internal static void End()
        {
            if (_depth > 0) --_depth;
        }
    }
}
'''

NEW_BRIDGE = '''using FPSGame.Gameplay;
using FPSGame.Net;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · 按键终端(KeyScreen)阶段同步桥（09）：**一处收口终端的阶段推进**。
    ///
    /// <para>▍为什么需要：阶段推进原本纯本地（<c>KeyScreen.OnUpdateStage</c> 只在本地抛），而任务的
    /// **中间阶段标志**全靠它 —— 黑盒子的 <c>startDefense</c>、炼油的 Start/Repair、撤离的 Wait、
    /// <c>KeyScreenContGo</c> 的显隐与语音。两端阶段一分叉，就是"客机进了坚守、房主没进"（2026-10-11 用户实测）。</para>
    ///
    /// <para>▍驱动口径（判定唯一收口在 <c>KeyScreen.IsStageDriver</c>）：**只有驱动端上报** —— 有人操作时 =
    /// 操作者所在那台；无人操作（Load/Wait 自动阶段） = 房主。其余端只应用，不自行推进。</para>
    ///
    /// <para>▍键 = 终端家具的 <c>Furniture_Attached.SyncId</c>（与 4036 同口径 ⇒ 两端日志可直接对照）。</para>
    ///
    /// <para>▍每局一份，由 <see cref="WaveManager"/> Install/Uninstall（同 <see cref="NetFurnitureBridge"/> 模式）。</para>
    /// </summary>
    public static class NetKeyScreenBridge
    {
        static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;

            // 成员端：终端阶段**不自行推进**，等驱动端（操作者那台 / 房主）下发
            var flow = NetRoomFlow.Instance;
            KeyScreen.RemoteDriven = flow != null && flow.SelfSid != 0u;

            KeyScreen.OnLocalStageChanged += OnLocalStageChanged;
            NetRoomFlow.OnKeyScreenStage += OnRemoteStage;
            NetRoomFlow.OnHostLost += OnHostLost;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            KeyScreen.OnLocalStageChanged -= OnLocalStageChanged;
            NetRoomFlow.OnKeyScreenStage -= OnRemoteStage;
            NetRoomFlow.OnHostLost -= OnHostLost;

            // ⚠ 必须复位：否则单机 / 下一局的终端永远不推进（同 MissionBase.RemoteDriven 那个坑）
            KeyScreen.RemoteDriven = false;
        }

        /// <summary>房主掉了 ⇒ 本端接管推进（否则这一局的终端就永久停在当前阶段）。</summary>
        static void OnHostLost()
        {
            KeyScreen.RemoteDriven = false;
        }

        #region 本端 -> 网络

        /// <summary>⚠ 只会被**驱动端**调到（<c>KeyScreen.SetStage</c> 里已经判过驱动门）。</summary>
        static void OnLocalStageChanged(int syncId, int stage)
        {
            if (syncId == 0) return;                 // 没有稳定键（终端上没身份源）：不参与同步
            if (NetApplyGuard.Applying) return;      // 正在应用远端：别再上报（防回环）

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            FPSGame.Utils.NetSyncLog.SyncLog("同步/终端/发",
                $"sync={syncId} → 阶段 {stage}（我是{(flow.IsHost ? "房主" : "成员")}）");
            flow.SendKeyScreenStage(syncId, flow.IsHost ? 0u : flow.SelfSid, stage);
        }

        #endregion

        #region 网络 -> 本端

        static void OnRemoteStage(KeyScreenStageMsg m)
        {
            if (m == null || m.SyncId == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost)
            {
                // 房主：转发全体（含发起者；发起者按 Sid 丢掉自己那条）—— 权威 Sid 原样带上
                flow.SendKeyScreenStage(m.SyncId, m.Sid, m.Stage);
            }
            else if (m.Sid == flow.SelfSid)
            {
                return;                              // 自己发的那条绕回来了：本地已经推进过
            }

            var furn = Furniture_Attached.FindBySyncId(m.SyncId);
            var screen = furn != null ? furn.GetComponentInChildren<KeyScreen>(true) : null;
            if (screen == null)
            {
                // ⚠ 不静默：要么两端 SyncId 不一致，要么本端压根没这个终端（同 NetFurnitureBridge 的排查口径）
                FPSGame.Utils.NetSyncLog.Warn("同步/终端/收",
                    $"sync={m.SyncId} 阶段{m.Stage}（发起 sid={m.Sid}）**本端找不到这个终端** ⇒ 丢弃");
                return;
            }

            FPSGame.Utils.NetSyncLog.SyncLog("同步/终端/收",
                $"{furn.name} → 阶段 {m.Stage}（发起 sid={m.Sid}）");

            // 共享门：应用阶段可能顺带触发 furn.Operate() ⇒ 会派发 FurnitureOperate（4036）⇒ 挡住它的上报
            NetApplyGuard.Begin();
            try
            {
                screen.ApplyRemoteStage(m.Stage);
            }
            finally
            {
                NetApplyGuard.End();
            }
        }

        #endregion
    }
}
'''


# =====================================================================
# 两阶段提交
# =====================================================================
def main():
    apply = '--apply' in sys.argv

    # 新建文件的存在性校验（幂等：已存在就跳过）
    plan_new = []
    if not os.path.exists(P('Assets/Scripts/09Manager/Battle/NetApplyGuard.cs')):
        plan_new.append(('Assets/Scripts/09Manager/Battle/NetApplyGuard.cs', NEW_GUARD))
    if not os.path.exists(P('Assets/Scripts/09Manager/Battle/NetKeyScreenBridge.cs')):
        plan_new.append(('Assets/Scripts/09Manager/Battle/NetKeyScreenBridge.cs', NEW_BRIDGE))

    changed = [f.rel for f in FILES.values() if f.dirty]
    print('=== 待写入（已存在文件）：%d ===' % len(changed))
    for rel in changed:
        print('  M ' + rel)
    print('=== 待新建：%d ===' % len(plan_new))
    for rel, _ in plan_new:
        print('  A ' + rel)

    if not changed and not plan_new:
        print('没有需要改的东西（已打过补丁）。')
        return

    if not apply:
        print('\n[dry run] 校验全部通过。加 --apply 落盘。')
        return

    for rel, text in plan_new:
        new_file(rel, text)
    n = 0
    for f in FILES.values():
        if f.write():
            n += 1
    print('\n已落盘：改 %d 个文件、新建 %d 个文件。' % (n, len(plan_new)))


if __name__ == '__main__':
    main()
