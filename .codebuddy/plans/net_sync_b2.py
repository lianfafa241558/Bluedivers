# -*- coding: utf-8 -*-
"""
联机同步 batch2（2026-10-11 用户拍板）：
  E  容器类家具的远端重放：抽 IOOPartInventory 接口 + FriendOOPartInventory（盟友幽灵背包）
     + 一条 4048 OOPartSubmitNtf（提交个数/类型）⇒ 远端在幽灵背包上跑真实的 BeginPress/StepPress
  D  KeyScreen 的"玩家操作"同步：4049 KeyScreenInputNtf（按键状态）—— 让所有玩家看到同一个终端
     （非驱动端的输入来源换成远端按键，逻辑照跑 ⇒ 显示自然一致）

用法：python .codebuddy/plans/net_sync_b2.py [--apply]
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
        a = anchor.split('\n')
        n = new.split('\n')
        hits = [i for i in range(len(self.lines) - len(a) + 1)
                if self.lines[i:i + len(a)] == a]
        assert len(hits) == count, \
            '[锚点失配] %s key=%s 命中=%d 期望=%d' % (self.rel, key, len(hits), count)
        i = hits[0]
        self.lines[i:i + len(a)] = n
        self.dirty = True

    def replace_all(self, key, old, new, expect):
        """整行内的子串替换（用于 20 处 InputManager. 这类批量点）。"""
        n = 0
        for i, l in enumerate(self.lines):
            if old in l:
                self.lines[i] = l.replace(old, new)
                n += 1
        assert n == expect, '[批量替换数量不符] %s key=%s 实际=%d 期望=%d' % (self.rel, key, n, expect)
        self.dirty = n > 0

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


def new_file(rel, text):
    path = P(*rel.split('/'))
    assert not os.path.exists(path), '文件已存在：%s' % rel
    io.open(path, 'wb').write(text.encode('utf-8'))


FILES = {}


def F(rel):
    if rel not in FILES:
        FILES[rel] = File(rel)
    return FILES[rel]


# =====================================================================
# 新文件：IOOPartInventory / FriendOOPartInventory
# =====================================================================
NEW_IFACE = '''using System.Collections.Generic;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 欧帕兹背包的"可读写"抽象。
    ///
    /// <para>▍两个实现：本地玩家 = <see cref="PlayerOOPartInventory"/>；远端盟友 = <see cref="FriendOOPartInventory"/>
    /// （数据由联机消息临时注入）。</para>
    ///
    /// <para>▍为什么需要：容器类家具（<see cref="Furniture_OOPartDepositBase"/>：欧帕兹提交台 / 玩家火炮装填）的
    /// 效果写在 <c>IStepPress.StepPress</c> —— 是"从**操作者背包**里逐个扣出来"。远端的操作者是盟友实例，
    /// 身上原本没有任何背包组件 ⇒ 重放连第一步（<c>BeginPress</c> 读 <c>CurrentCount</c>）都过不去。
    /// 容器家具改为**面向接口**取背包后，远端挂上 <see cref="FriendOOPartInventory"/> 就能跑同一套逻辑。</para>
    /// </summary>
    public interface IOOPartInventory
    {
        /// <summary>当前携带总量。</summary>
        int CurrentCount { get; }

        /// <summary>指定类型是否已满。</summary>
        bool IsFull(OOPartEnum type);

        /// <summary>尝试加入携带，成功返回 true。</summary>
        bool TryAdd(OOPartEnum type, int count);

        /// <summary>取出指定数量，返回实际取出量。</summary>
        int Remove(OOPartEnum type, int count);

        /// <summary>获取某类型的携带量。</summary>
        int GetCount(OOPartEnum type);

        /// <summary>当前携带的全部条目。</summary>
        IEnumerable<KeyValuePair<OOPartEnum, int>> GetAll();
    }
}
'''

NEW_FRIEND_BAG = '''using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 远端盟友的"幽灵背包"（<see cref="IOOPartInventory"/> 的远端实现）：数据不来自玩家存档，
    /// 而是由联机消息**临时注入**、用完即弃。
    ///
    /// <para>▍用途：让"容器类家具的远端重放"成为可能 —— 桥收到 4048（提交了几个）后，
    /// 把它注入到这里，再对家具跑真实的 <c>BeginPress</c>/<c>StepPress</c>/<c>Operate</c>，
    /// 于是 <c>held</c> 与 <c>OnSubmit</c> 的表现（炮弹架要转成炮弹）在远端自然成立，
    /// 不需要再单开一条"结果状态"通道。</para>
    ///
    /// <para>▍挂载：盟友实例（<c>PlayerFriend.prefab</c>）上，由 <c>NetFriendBridge</c> 创建时补挂。</para>
    /// </summary>
    [AddComponentMenu("玩家/盟友幽灵背包(联机)")]
    public class FriendOOPartInventory : MonoBehaviour, IOOPartInventory
    {
        private readonly Dictionary<OOPartEnum, int> items = new();
        private int currentCount;

        public int CurrentCount => currentCount;

        /// <summary>【桥调用】注入本次要"被提交掉"的量（覆盖式：重放前先清）。</summary>
        public void Inject(OOPartEnum type, int count)
        {
            items.Clear();
            currentCount = 0;
            if (count <= 0) return;
            items[type] = count;
            currentCount = count;
        }

        /// <summary>清空（重放收尾 / 单位离场时调用）。</summary>
        public void Clear()
        {
            items.Clear();
            currentCount = 0;
        }

        // 幽灵背包不做容量限制：远端重放的是"已经发生过的提交"，被上限挡住反而会少扣
        public bool IsFull(OOPartEnum type) => false;

        public int GetCount(OOPartEnum type) => items.TryGetValue(type, out var c) ? c : 0;

        public IEnumerable<KeyValuePair<OOPartEnum, int>> GetAll() => items;

        public bool TryAdd(OOPartEnum type, int count)
        {
            if (count <= 0) return false;
            items[type] = GetCount(type) + count;
            currentCount += count;
            return true;
        }

        public int Remove(OOPartEnum type, int count)
        {
            if (count <= 0 || !items.TryGetValue(type, out var have) || have <= 0) return 0;

            int actual = Mathf.Min(count, have);
            if (have == actual) items.Remove(type);
            else items[type] = have - actual;
            currentCount -= actual;
            return actual;
        }
    }
}
'''

# =====================================================================
# 1. PlayerOOPartInventory 实现接口
# =====================================================================
POI = 'Assets/Scripts/06Gameplay/Player/PlayerOOPartInventory.cs'
if not F(POI).has('IOOPartInventory'):
    F(POI).patch(
        'poi.iface',
        '''public class PlayerOOPartInventory : MonoBehaviour
{''',
        '''public class PlayerOOPartInventory : MonoBehaviour, IOOPartInventory
{''')

# =====================================================================
# 2. Furniture_OOPartDepositBase：面向接口 + 提交上报
# =====================================================================
FOD = 'Assets/Scripts/06Gameplay/Interactable/Furniture_OOPartDepositBase.cs'
if not F(FOD).has('OnSubmitReported'):
    f = F(FOD)

    # 3 处：out PlayerOOPartInventory bag → out IOOPartInventory bag
    f.replace_all('fod.iface', 'out PlayerOOPartInventory bag', 'out IOOPartInventory bag', 3)

    f.patch(
        'fod.usings',
        '''using FPSGame.Attributes;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Gameplay;''',
        '''using System;
using FPSGame.Attributes;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Gameplay;''')

    f.patch(
        'fod.event',
        '''    /// <summary>每秒提交到容器的欧帕兹数量（恒定速率）</summary>
    public const int SubmitPerSecond = 5;''',
        '''    /// <summary>每秒提交到容器的欧帕兹数量（恒定速率）</summary>
    public const int SubmitPerSecond = 5;

    /// <summary>一次提交已产生效果（家具 / 本次提交个数 / 主类型 index）⇒ 09 侧联机桥据此在**远端**重放这次提交。
    /// <para>▍为什么要报：本类的效果写在 <c>IStepPress.StepPress</c>（逐个扣背包 + <see cref="OnSubmit"/>），
    /// 而远端重放只走 <c>Operate()</c>（只收尾）⇒ 不报个数的话远端 <c>held</c> 永远不动、<c>OnSubmit</c> 也不触发。</para></summary>
    public static event Action<Furniture_OOPartDepositBase, int, int> OnSubmitReported;''')

    f.patch(
        'fod.statefields',
        '''    // ---- 接管按压运行时状态 ----
    private bool _pressing;          // 是否正在长按转移中
    private GameObject _pressUser;   // 正在操作的玩家
    private int _need;               // 本次按下锁定的需转移个数
    private int _transferred;        // 本次已转移个数
    private float _stepAcc;          // 转移节拍累加器''',
        '''    // ---- 接管按压运行时状态 ----
    private bool _pressing;          // 是否正在长按转移中
    private GameObject _pressUser;   // 正在操作的玩家
    private int _need;               // 本次按下锁定的需转移个数
    private int _transferred;        // 本次已转移个数
    private float _stepAcc;          // 转移节拍累加器
    private OOPartEnum _lastSubmitType;  // 最近一次转移的类型（联机上报时当"主类型"）
    private bool _reported;          // 本次长按是否已经上报过（完成 + 松开两条路都调，防重复上报）''')

    f.patch(
        'fod.beginpress',
        '''        _pressing = true;
        _pressUser = unit;
        _transferred = 0;
        _stepAcc = 0;
        pressTime = 0;''',
        '''        _pressing = true;
        _pressUser = unit;
        _transferred = 0;
        _stepAcc = 0;
        _reported = false;
        pressTime = 0;''')

    f.patch(
        'fod.onsubmit.call',
        '''            if (RemoveAnyOne(bag, out OOPartEnum type))
            {
                held++;
                _transferred++;
                OnSubmit(_pressUser, type);
            }''',
        '''            if (RemoveAnyOne(bag, out OOPartEnum type))
            {
                held++;
                _transferred++;
                _lastSubmitType = type;
                OnSubmit(_pressUser, type);
            }''')

    f.patch(
        'fod.cancel',
        '''    void IStepPress.CancelPress()
    {
        GameObject user = _pressUser;
        int transferred = _transferred;
        ResetPressState();
        if (user != null)
            OnPressCancel(user, transferred);
    }''',
        '''    void IStepPress.CancelPress()
    {
        GameObject user = _pressUser;
        int transferred = _transferred;
        ReportSubmitted();          // ★ 松手也算"已提交了 transferred 个"（否则远端会漏掉这一批）
        ResetPressState();
        if (user != null)
            OnPressCancel(user, transferred);
    }

    /// <summary>把"本次提交了几个"报给联机桥（幂等：一次长按只报一次）。</summary>
    private void ReportSubmitted()
    {
        if (_reported || _transferred <= 0) return;
        _reported = true;
        OnSubmitReported?.Invoke(this, _transferred, (int)_lastSubmitType);
    }''')

    f.patch(
        'fod.operate',
        '''    public override void Operate()
    {
        base.Operate();

        // 正常转满收尾：仅复位按压状态，不触发"松开"钩子（完成由本方法收尾表达）
        ResetPressState();''',
        '''    public override void Operate()
    {
        // ★ 联机：转满收尾时报一次"本次提交了几个"（远端据此在盟友的幽灵背包上重放这次提交）
        ReportSubmitted();

        base.Operate();

        // 正常转满收尾：仅复位按压状态，不触发"松开"钩子（完成由本方法收尾表达）
        ResetPressState();''')

# =====================================================================
# 3. NetFriendBridge：盟友实例补挂幽灵背包
# =====================================================================
NFB = 'Assets/Scripts/09Manager/Global/NetFriendBridge.cs'
if not F(NFB).has('FriendOOPartInventory'):
    F(NFB).patch(
        'nfb.bag',
        '''            _friends[info.Sid] = fc;''',
        '''            // 盟友的"幽灵背包"：容器类家具（欧帕兹提交台 / 玩家火炮）的远端重放要有背包可扣
            // （见 FriendOOPartInventory / IOOPartInventory 的注释）
            if (go.GetComponent<FriendOOPartInventory>() == null) go.AddComponent<FriendOOPartInventory>();

            _friends[info.Sid] = fc;''')

# =====================================================================
# 4. CmdId：4048 提交 / 4049 终端输入
# =====================================================================
CID = 'Assets/Scripts/07NetGame/CmdId.cs'
if not F(CID).has('OOPartSubmitNtf'):
    F(CID).patch(
        'cmd.b2',
        '''        public const int PatrolSpawnNtf = 4047;''',
        '''        public const int PatrolSpawnNtf = 4047;

        /// <summary>双向：**容器类家具的一次提交已完成**（键 = 家具 <c>SyncId</c>；值 = 个数 + 主类型）。
        /// <para>▍为什么单独一条：这类家具的效果写在 <c>IStepPress.StepPress</c>（逐个扣背包 + <c>OnSubmit</c>），
        /// 而远端重放只走 <c>Operate()</c>（只收尾）⇒ 必须把"提交了几个"传过去，由远端在盟友的幽灵背包上重放。</para></summary>
        public const int OOPartSubmitNtf = 4048;

        /// <summary>双向：**玩家对按键终端(KeyScreen)的操作**（键 = 终端家具 <c>SyncId</c>；值 = 按键 + 类型）。
        /// <para>▍与 4046（阶段推进）分工：4046 保证"阶段权威、不分叉"；本条让"终端上正在发生什么"对所有端可见
        /// （非驱动端把本机键盘换成远端按键，逻辑照跑 ⇒ 显示自然一致）。</para></summary>
        public const int KeyScreenInputNtf = 4049;''')

# =====================================================================
# 5. SyncMsg：两条 DTO
# =====================================================================
SM = 'Assets/Scripts/07NetGame/Msg/SyncMsg.cs'
if not F(SM).has('KeyScreenInputMsg'):
    F(SM).patch(
        'msg.b2',
        '''        /// <summary>逐单位目标点（x,y,z 三元组，长度 = 单位数 × 3；Kind=1 时取第 0 组给所有单位）。</summary>
        [Key(4)] public float[] Targets;
    }
}''',
        '''        /// <summary>逐单位目标点（x,y,z 三元组，长度 = 单位数 × 3；Kind=1 时取第 0 组给所有单位）。</summary>
        [Key(4)] public float[] Targets;
    }

    /// <summary>容器类家具的一次提交（双向）。</summary>
    [MessagePackObject]
    public class OOPartSubmitMsg
    {
        /// <summary>提交者会话号（房主 = 0）⇒ 接收端据此解析"操作者"（盟友实例）并丢弃自己的回环。</summary>
        [Key(0)] public uint Sid;
        /// <summary>家具跨端稳定键（<c>Furniture_Attached.SyncId</c>）。</summary>
        [Key(1)] public int SyncId;
        /// <summary>本次提交个数。</summary>
        [Key(2)] public int Count;
        /// <summary>主类型（<c>OOPartEnum</c> 的 int；-1 = 未知）。</summary>
        [Key(3)] public int Type;
    }

    /// <summary>玩家对终端(KeyScreen)的一次输入（双向）。
    /// <para>▍Key = <c>InputState</c> 的 int；Kind：0 = 本帧按下、1 = 按住开始、2 = 松开。</para>
    /// <para>▍只传"按键"而不是传终端状态：非驱动端把本机键盘换成远端按键后，**跑的是同一套阶段逻辑**
    /// ⇒ UI/判定/光标位置全部自然一致，不用再抽一堆"刷新 UI"的接口。</para></summary>
    [MessagePackObject]
    public class KeyScreenInputMsg
    {
        [Key(0)] public uint Sid;
        [Key(1)] public int SyncId;
        [Key(2)] public int Key;
        [Key(3)] public int Kind;
    }
}''')

# =====================================================================
# 6. NetRoomFlow：两条通道
# =====================================================================
NRF = 'Assets/Scripts/07NetGame/NetRoomFlow.cs'
if not F(NRF).has('OnKeyScreenInput'):
    f = F(NRF)
    f.patch(
        'flow.event.b2',
        '''        /// <summary>【成员侧】房主广播的巡逻队生成（生成点 + 逐单位目标点）⇒ 本端复刻同一支（键 = 生成点）。</summary>
        public static event Action<PatrolSpawnMsg> OnPatrolSpawn;''',
        '''        /// <summary>【成员侧】房主广播的巡逻队生成（生成点 + 逐单位目标点）⇒ 本端复刻同一支（键 = 生成点）。</summary>
        public static event Action<PatrolSpawnMsg> OnPatrolSpawn;

        /// <summary>【双向】容器类家具的一次提交（个数 / 类型）⇒ 远端在盟友幽灵背包上重放。</summary>
        public static event Action<OOPartSubmitMsg> OnOOPartSubmit;

        /// <summary>【双向】玩家对终端(KeyScreen)的一次输入 ⇒ 非驱动端复现。</summary>
        public static event Action<KeyScreenInputMsg> OnKeyScreenInput;''')

    f.patch(
        'flow.reg.b2',
        '''            MessageCenter.Register<PatrolSpawnMsg>(CmdId.PatrolSpawnNtf, HandlePatrolSpawnNtf);''',
        '''            MessageCenter.Register<PatrolSpawnMsg>(CmdId.PatrolSpawnNtf, HandlePatrolSpawnNtf);
            MessageCenter.Register<OOPartSubmitMsg>(CmdId.OOPartSubmitNtf, HandleOOPartSubmitNtf);
            MessageCenter.Register<KeyScreenInputMsg>(CmdId.KeyScreenInputNtf, HandleKeyScreenInputNtf);''')

    f.patch(
        'flow.unreg.b2',
        '''            MessageCenter.Unregister(CmdId.PatrolSpawnNtf);''',
        '''            MessageCenter.Unregister(CmdId.PatrolSpawnNtf);
            MessageCenter.Unregister(CmdId.OOPartSubmitNtf);
            MessageCenter.Unregister(CmdId.KeyScreenInputNtf);''')

    f.patch(
        'flow.send.b2',
        '''        private void HandlePatrolSpawnNtf(PatrolSpawnMsg m)
        {
            if (m != null) OnPatrolSpawn?.Invoke(m);
        }''',
        '''        private void HandlePatrolSpawnNtf(PatrolSpawnMsg m)
        {
            if (m != null) OnPatrolSpawn?.Invoke(m);
        }

        /// <summary>容器类家具提交：成员上报房主 / 房主转发全体（走向由 09 侧的 NetFurnitureBridge 决定）。</summary>
        public void SendOOPartSubmit(int syncId, uint sid, int count, int type)
        {
            var msg = MessageCenter.Pack(CmdId.OOPartSubmitNtf,
                new OOPartSubmitMsg { SyncId = syncId, Sid = sid, Count = count, Type = type });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else NetSvc.Instance?.SendMsg(msg);
        }

        private void HandleOOPartSubmitNtf(OOPartSubmitMsg m)
        {
            if (m != null) OnOOPartSubmit?.Invoke(m);
        }

        /// <summary>终端输入：驱动端上报 / 房主转发全体（走向由 09 侧的 NetKeyScreenBridge 决定）。</summary>
        public void SendKeyScreenInput(int syncId, uint sid, int key, int kind)
        {
            var msg = MessageCenter.Pack(CmdId.KeyScreenInputNtf,
                new KeyScreenInputMsg { SyncId = syncId, Sid = sid, Key = key, Kind = kind });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else NetSvc.Instance?.SendMsg(msg);
        }

        private void HandleKeyScreenInputNtf(KeyScreenInputMsg m)
        {
            if (m != null) OnKeyScreenInput?.Invoke(m);
        }''')

# =====================================================================
# 7. NetFurnitureBridge：提交上报 / 远端重放
# =====================================================================
NFB2 = 'Assets/Scripts/09Manager/Battle/NetFurnitureBridge.cs'
if not F(NFB2).has('OnRemoteSubmit'):
    f = F(NFB2)
    f.patch(
        'fb.install.b2',
        '''            Furniture_Attached.OnStateChanged += OnLocalStateChanged;   // 家具"状态"变更（无交互驱动的那种）''',
        '''            Furniture_Attached.OnStateChanged += OnLocalStateChanged;   // 家具"状态"变更（无交互驱动的那种）
            Furniture_OOPartDepositBase.OnSubmitReported += OnLocalSubmitReported;   // 容器类家具的一次提交''')

    f.patch(
        'fb.uninstall.b2',
        '''            Furniture_Attached.OnStateChanged -= OnLocalStateChanged;''',
        '''            Furniture_Attached.OnStateChanged -= OnLocalStateChanged;
            Furniture_OOPartDepositBase.OnSubmitReported -= OnLocalSubmitReported;''')

    f.patch(
        'fb.submit.region',
        '''        /// <summary>日志用的一句话描述（Id / SyncId / 物体名）。</summary>''',
        '''        /// <summary>容器类家具提交完成（子类调 <c>OnSubmitReported</c>）：上报"提交了几个 + 主类型"。
        /// <para>▍远端收到后会<b>真跑一遍</b> BeginPress/StepPress（见 <see cref="OnRemoteSubmit"/>）——
        /// 因为这类家具的效果（<c>held</c> / <c>OnSubmit</c>）只在按压过程里产生，<c>Operate()</c> 重放不出来。</para></summary>
        static void OnLocalSubmitReported(Furniture_OOPartDepositBase f, int count, int type)
        {
            if (NetApplyGuard.Applying) return;          // 远端重放里又触发了一次：别再上报
            if (f == null || f.SyncId == 0 || count <= 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;    // 单机 / 不在房里：两端本来就一致

            FPSGame.Utils.NetSyncLog.SyncLog("同步/容器·发",
                $"{Describe(f, f)} 提交 {count} 个（type={type}）");
            flow.SendOOPartSubmit(f.SyncId, flow.IsHost ? 0u : flow.SelfSid, count, type);
        }

        /// <summary>日志用的一句话描述（Id / SyncId / 物体名）。</summary>''')

    f.patch(
        'fb.remote.region',
        '''        /// <summary>
        /// 把权威 <c>Sid</c> 解析成本端的 GameObject：<c>0</c> = 房主，其余 = 对应盟友实例。''',
        '''        /// <summary>容器类家具的一次提交（远端）：把个数注入盟友的**幽灵背包**，再跑同一套按压逻辑。
        /// <para>▍这样 <c>held</c> 与 <c>OnSubmit</c>（炮弹架要转成炮弹）在远端自然成立，
        /// 不需要再单开一条"结果状态"通道；<c>User</c> 必须是**远端单位**（同 4036 的口径）。</para></summary>
        static void OnRemoteSubmit(OOPartSubmitMsg m)
        {
            if (m == null || m.SyncId == 0 || m.Count <= 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost)
            {
                flow.SendOOPartSubmit(m.SyncId, m.Sid, m.Count, m.Type);   // 房主：转发全体
            }
            else if (m.Sid == flow.SelfSid)
            {
                return;                                                    // 自己发的那条绕回来了
            }

            var furn = Furniture_Attached.FindBySyncId(m.SyncId) as Furniture_OOPartDepositBase;
            if (furn == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("容器/收", $"sync={m.SyncId} **本端找不到这个容器**（或类型不符）⇒ 丢弃");
                return;
            }

            var user = ResolveRemoteUser(m.Sid);
            if (user == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("容器/收", $"{Describe(furn, furn)} 解析不到远端操作者 sid={m.Sid} ⇒ 丢弃");
                return;
            }

            var bag = user.GetComponent<FriendOOPartInventory>();
            if (bag == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("容器/收", $"{user.name} 上没有 FriendOOPartInventory ⇒ 无法重放（预制体应带）");
                return;
            }

            bag.Inject((FPSGame.GameContract.OOPartEnum)m.Type, m.Count);

            var step = (FPSGame.GameContract.IStepPress)furn;
            FPSGame.Utils.NetSyncLog.SyncLog("容器/收",
                $"{Describe(furn, furn)} 重放提交 {m.Count} 个（type={m.Type}）");

            NetApplyGuard.Begin();
            try
            {
                if (!step.BeginPress(user))
                {
                    FPSGame.Utils.NetSyncLog.Warn("容器/收", $"{Describe(furn, furn)} BeginPress 被拒 ⇒ 本次重放作废");
                    return;
                }

                // 给足节拍：StepPress 内部按 SubmitPerSecond 的节拍逐个扣（见 Furniture_OOPartDepositBase）
                step.StepPress(m.Count * (1f / Furniture_OOPartDepositBase.SubmitPerSecond) + 0.05f);
                furn.Handle(user);      // 收尾（同一条 Operate ⇒ held 已加、OnSubmit 已触发）
            }
            finally
            {
                NetApplyGuard.End();
                bag.Clear();            // 幽灵背包用完即弃
            }
        }

        /// <summary>把权威 <c>Sid</c> 解析成本端的 GameObject：<c>0</c> = 房主，其余 = 对应盟友实例。</summary>''')

# =====================================================================
# 8. KeyScreen：输入包装 + 远端输入 + 上报/应用
# =====================================================================
KS = 'Assets/Scripts/06Gameplay/Interactable/KeyScreen.cs'
if not F(KS).has('OnLocalInput'):
    f = F(KS)

    f.patch(
        'ks.input',
        '''    /// <summary>本端推进了一个阶段（**只由驱动端抛**）⇒ 09 侧 NetKeyScreenBridge 订阅后上报。
    /// <para>参数：(终端家具 SyncId, 新阶段)。SyncId = 0 表示本端终端没有身份源 ⇒ 不参与同步。</para></summary>
    public static event Action<int, int> OnLocalStageChanged;''',
        '''    /// <summary>本端推进了一个阶段（**只由驱动端抛**）⇒ 09 侧 NetKeyScreenBridge 订阅后上报。
    /// <para>参数：(终端家具 SyncId, 新阶段)。SyncId = 0 表示本端终端没有身份源 ⇒ 不参与同步。</para></summary>
    public static event Action<int, int> OnLocalStageChanged;

    /// <summary>本端读到了一次"玩家对终端的输入"（**只由驱动端读本机键盘时抛**）⇒ 09 侧桥上报。
    /// <para>参数：(终端 SyncId, <c>InputState</c> 的 int, 类型)。类型：0 = 本帧按下、1 = 按住开始、2 = 松开。</para></summary>
    public static event Action<int, int, int> OnLocalInput;

    /// <summary>远端玩家的"按住"状态（非驱动端用它代替本机键盘）。</summary>
    private static readonly HashSet<InputState> _remoteHeld = new HashSet<InputState>();

    /// <summary>远端玩家"本帧按下"的键（<see cref="LateUpdate"/> 里清空；非驱动端拿它当 <c>GetDown</c>）。</summary>
    private static readonly HashSet<InputState> _remoteDown = new HashSet<InputState>();

    /// <summary>本机"按住"缓存：识别"按住 / 松开"的边沿（只在驱动端有意义）。</summary>
    private static readonly HashSet<InputState> _localHeld = new HashSet<InputState>();

    /// <summary>非驱动端 = 成员端且"操作者不是我" ⇒ 只复现远端输入、不吃本机键盘
    /// <para>（否则两个人同时按会互相干扰）。单机 / 驱动端恒为 false ⇒ 行为与改造前一致。</para></summary>
    private bool UseRemoteInput => RemoteDriven && !IsStageDriver();

    /// <summary>持续按住（对应 <c>InputManager.Get</c>）。</summary>
    protected bool InputGet(InputState key)
    {
        if (UseRemoteInput) return _remoteHeld.Contains(key);

        bool now = InputManager.Get(key);
        bool was = _localHeld.Contains(key);
        if (now != was)
        {
            if (now) _localHeld.Add(key);
            else _localHeld.Remove(key);
            OnLocalInput?.Invoke(SyncId, (int)key, now ? 1 : 2);
        }
        return now;
    }

    /// <summary>本帧按下（对应 <c>InputManager.GetDown</c>）。</summary>
    protected bool InputGetDown(InputState key)
    {
        if (UseRemoteInput) return _remoteDown.Contains(key);

        bool now = InputManager.GetDown(key);
        if (now) OnLocalInput?.Invoke(SyncId, (int)key, 0);
        return now;
    }

    /// <summary>【联机】应用远端玩家的一次输入（由 09 侧 NetKeyScreenBridge 调用）。</summary>
    public void ApplyRemoteInput(int key, int kind)
    {
        var state = (InputState)key;
        switch (kind)
        {
            case 0: _remoteDown.Add(state); break;
            case 1: _remoteHeld.Add(state); _remoteDown.Add(state); break;   // 按下那一帧同时也是"本帧按下"
            case 2: _remoteHeld.Remove(state); break;
        }
    }

    /// <summary>清掉远端输入（换阶段 / 应用远端阶段时）：避免上一阶段的按键残留把下一阶段推走。</summary>
    private void ClearRemoteInput()
    {
        _remoteHeld.Clear();
        _remoteDown.Clear();
    }

    private void LateUpdate()
    {
        // 远端"本帧按下"只对下一帧的 Update 有效（同 InputManager.GetDown 的语义）
        if (_remoteDown.Count > 0) _remoteDown.Clear();
    }''')

    f.patch(
        'ks.apply.clear',
        '''    public void ApplyRemoteStage(int stage)
    {
        if (this == null) return;
        if (stage == nowStage) return;      // 已经在这一阶段：幂等
        SetStageCore(stage);
    }''',
        '''    public void ApplyRemoteStage(int stage)
    {
        if (this == null) return;
        if (stage == nowStage) return;      // 已经在这一阶段：幂等
        ClearRemoteInput();                 // 换阶段：清掉上一阶段残留的远端按键
        SetStageCore(stage);
    }''')

    f.patch(
        'ks.update.gate',
        '''        if (!FPSGame.Utils.Tool.In(nowStage,-1,procedure.Count)) return;
        // ⚠ 非驱动端只应用下发过来的阶段、不自己推进（两端各自的计时器/本机键盘输入必然分叉）
        if (!IsStageDriver()) return;
        var nowPro = nowProcedure;''',
        '''        if (!FPSGame.Utils.Tool.In(nowStage,-1,procedure.Count)) return;
        // ⚠ 非驱动端**也要跑这一段**（它要复现远端输入来刷新本地显示），但输入来源已经换成远端
        //   （见 InputGet / InputGetDown）⇒ 它不会推进阶段（SetStage 里还有驱动门兜底）。
        var nowPro = nowProcedure;''')

# =====================================================================
# 9. KeyScreenControl：20 处输入读取改走包装
# =====================================================================
KSC = 'Assets/Scripts/06Gameplay/Interactable/KeyScreenControl.cs'
if not F(KSC).has('InputGetDown('):
    f = F(KSC)
    f.replace_all('ksc.getdown', 'InputManager.GetDown(', 'InputGetDown(', 14)
    f.replace_all('ksc.get', 'InputManager.Get(', 'InputGet(', 6)

# =====================================================================
# 10. NetKeyScreenBridge：输入通道（上报 + 应用）
# =====================================================================
NKSB = 'Assets/Scripts/09Manager/Battle/NetKeyScreenBridge.cs'
if not F(NKSB).has('OnRemoteInput'):
    f = F(NKSB)
    f.patch(
        'nksb.install',
        '''            KeyScreen.OnLocalStageChanged += OnLocalStageChanged;
            NetRoomFlow.OnKeyScreenStage += OnRemoteStage;
            NetRoomFlow.OnHostLost += OnHostLost;''',
        '''            KeyScreen.OnLocalStageChanged += OnLocalStageChanged;
            KeyScreen.OnLocalInput += OnLocalInput;
            NetRoomFlow.OnKeyScreenStage += OnRemoteStage;
            NetRoomFlow.OnKeyScreenInput += OnRemoteInput;
            NetRoomFlow.OnHostLost += OnHostLost;''')

    f.patch(
        'nksb.uninstall',
        '''            KeyScreen.OnLocalStageChanged -= OnLocalStageChanged;
            NetRoomFlow.OnKeyScreenStage -= OnRemoteStage;
            NetRoomFlow.OnHostLost -= OnHostLost;''',
        '''            KeyScreen.OnLocalStageChanged -= OnLocalStageChanged;
            KeyScreen.OnLocalInput -= OnLocalInput;
            NetRoomFlow.OnKeyScreenStage -= OnRemoteStage;
            NetRoomFlow.OnKeyScreenInput -= OnRemoteInput;
            NetRoomFlow.OnHostLost -= OnHostLost;''')

    f.patch(
        'nksb.local.input',
        '''        #endregion

        #region 网络 -> 本端''',
        '''        /// <summary>【驱动端】把本机对终端的每一次按键传出去（非驱动端靠它复现整个终端）。
        /// <para>▍为什么只传按键而不传状态：非驱动端把输入换成远端按键后**跑的是同一套阶段逻辑**，
        /// UI / 判定 / 光标位置全部自然一致，不用再抽一堆"刷新 UI"的接口。</para></summary>
        static void OnLocalInput(int syncId, int key, int kind)
        {
            if (syncId == 0) return;
            if (NetApplyGuard.Applying) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;      // 单机 / 不在房里

            flow.SendKeyScreenInput(syncId, flow.IsHost ? 0u : flow.SelfSid, key, kind);
        }

        #endregion

        #region 网络 -> 本端''')

    f.patch(
        'nksb.remote.input',
        '''        #endregion
    }
}''',
        '''        /// <summary>【非驱动端】复现远端玩家的按键（房主转发全体；发起者按 Sid 丢自己那条）。</summary>
        static void OnRemoteInput(KeyScreenInputMsg m)
        {
            if (m == null || m.SyncId == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost) flow.SendKeyScreenInput(m.SyncId, m.Sid, m.Key, m.Kind);
            else if (m.Sid == flow.SelfSid) return;

            var furn = Furniture_Attached.FindBySyncId(m.SyncId);
            var screen = furn != null ? furn.GetComponentInChildren<KeyScreen>(true) : null;
            if (screen == null) return;          // 找不到终端：静默（阶段同步那边已经会告警一次）

            screen.ApplyRemoteInput(m.Key, m.Kind);
        }

        #endregion
    }
}''')

# =====================================================================
# 11. 补漏：两个新文件的 using（OOPartEnum 在 FPSGame.GameContract）
# =====================================================================
for _NEWBAG in ('Assets/Scripts/06Gameplay/Player/IOOPartInventory.cs',
                'Assets/Scripts/06Gameplay/Player/FriendOOPartInventory.cs'):
    _f = F(_NEWBAG)
    if not _f.has('using FPSGame.GameContract;'):
        _f.patch('bag.using',
                 'using System.Collections.Generic;',
                 'using System.Collections.Generic;\nusing FPSGame.GameContract;')

# =====================================================================
# 12. 补漏：RemoveAnyOne 的形参类型（它不是 `out ... bag` 形态，批量替换漏掉）
# =====================================================================
_f = F('Assets/Scripts/06Gameplay/Interactable/Furniture_OOPartDepositBase.cs')
if not _f.has('RemoveAnyOne(IOOPartInventory'):
    _f.patch('fod.removeanyone',
             '    private static bool RemoveAnyOne(PlayerOOPartInventory bag, out OOPartEnum type)',
             '    private static bool RemoveAnyOne(IOOPartInventory bag, out OOPartEnum type)')

# =====================================================================
# 两阶段提交
# =====================================================================
def main():
    apply = '--apply' in sys.argv

    plan_new = []
    if not os.path.exists(P('Assets/Scripts/06Gameplay/Player/IOOPartInventory.cs')):
        plan_new.append(('Assets/Scripts/06Gameplay/Player/IOOPartInventory.cs', NEW_IFACE))
    if not os.path.exists(P('Assets/Scripts/06Gameplay/Player/FriendOOPartInventory.cs')):
        plan_new.append(('Assets/Scripts/06Gameplay/Player/FriendOOPartInventory.cs', NEW_FRIEND_BAG))

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
