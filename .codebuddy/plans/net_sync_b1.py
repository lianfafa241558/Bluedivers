# -*- coding: utf-8 -*-
"""
联机同步 batch1（2026-10-11 用户拍板）：
  A  巡逻队 = 房主权威 + 广播生成事件（4047），成员端复刻（不再本地造 ⇒ 不与 NetId 号段撞号）
  B  MissionOilRefining 的"修坏管"改纯函数种子（errorTimes / 挑坏管；巡逻落点随 A 的广播走，不用再随机）
  C  4036 追加 int/string 状态字段；Furniture_Pipe 状态同步 + 远端重放跳过"改地形高度图"
  四3 Furniture_Attached.ComputeSyncId 加一条可开关的键日志
  五1 MissionLidarStation 的 RemoveListener → AddListener（雷达站任务原本永远完不成）
  五2 Furniture_General.Operate 加注释（不调 base.Operate ⇒ 无 _Operate 的配置点了没反应）
  五4 MissionBroadcast / MissionDefensive 成对退订

用法：python .codebuddy/plans/net_sync_b1.py [--apply]
  （不带 --apply 只校验；两阶段提交：任一锚点不唯一则整体不写；可重复执行）
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

    def drop_from(self, prefix, count):
        """按**行首前缀**定位（避开中文引号），删除含该行起 count 行。"""
        hits = [i for i, l in enumerate(self.lines) if l.startswith(prefix)]
        assert len(hits) == 1, '[定位失配] %s prefix=%s 命中=%d' % (self.rel, prefix, len(hits))
        i = hits[0]
        del self.lines[i:i + count]
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
# 1. CmdId：4047 巡逻队生成
# =====================================================================
CID = 'Assets/Scripts/07NetGame/CmdId.cs'
if not F(CID).has('PatrolSpawnNtf'):
    F(CID).patch(
        'cmd.patrol',
        '''        public const int KeyScreenStageNtf = 4046;''',
        '''        public const int KeyScreenStageNtf = 4046;

        /// <summary>房主 -> 全体：**巡逻队生成**（生成点 + 逐单位目标点）。
        /// <para>▍为什么需要：巡逻队（任务的维修巡逻 / 据点的压力巡逻）原本是**本地生成**的 ⇒ 成员端自己也会造一批，
        /// 而 <c>Actor.Awake</c> 会给新单位分配 <c>NetId</c>（口径是"房主分配 + 两端创建顺序一致"）
        /// ⇒ 成员端自造的单位与房主下发的号段**撞号**（移动指令打到别的怪身上、死亡错杀）。</para>
        /// <para>▍口径：房主是唯一生成方（<c>WaveManager.CreatPatrol</c> 有成员闸门）；成员收到本条后按**同一生成点**复刻
        /// —— 构成不用传（<c>CreatPatrol</c> 按落点位置派生随机流 ⇒ 同落点同构成），只有逐单位目标点必须由房主下发。</para></summary>
        public const int PatrolSpawnNtf = 4047;''')

# =====================================================================
# 2. SyncMsg：PatrolSpawnMsg + FurnitureOperateMsg 追加状态字段
# =====================================================================
SM = 'Assets/Scripts/07NetGame/Msg/SyncMsg.cs'
if not F(SM).has('PatrolSpawnMsg'):
    F(SM).patch(
        'msg.patrol',
        '''        /// <summary>目标阶段号。</summary>
        [Key(2)] public int Stage;
    }
}''',
        '''        /// <summary>目标阶段号。</summary>
        [Key(2)] public int Stage;
    }

    /// <summary>巡逻队生成（房主 -> 全体）：生成点 + 逐单位目标点。
    /// <para>▍构成不传：<c>WaveManager.CreatPatrol</c> 按**落点位置**派生随机流 ⇒ 同落点 ⇒ 同构成（两端一致）。
    /// 逐单位目标点必须传：它由房主侧的几何 / 玩家位置算出，成员端算不出同一个值。</para></summary>
    [MessagePackObject]
    public class PatrolSpawnMsg
    {
        /// <summary>生成点（房主权威）。</summary>
        [Key(0)] public float X;
        [Key(1)] public float Y;
        [Key(2)] public float Z;
        /// <summary>目标点类型：0 = <c>PatrolPos</c>（据点巡逻，逐单位不同），1 = <c>HomePoint</c>（任务维修巡逻，全体同一处）。</summary>
        [Key(3)] public int Kind;
        /// <summary>逐单位目标点（x,y,z 三元组，长度 = 单位数 × 3；Kind=1 时取第 0 组给所有单位）。</summary>
        [Key(4)] public float[] Targets;
    }
}''')

if not F(SM).has('public string StateKey;'):
    F(SM).patch(
        'msg.furnstate',
        '''    [MessagePackObject]
    public class FurnitureOperateMsg
    {
        /// <summary>权威标识：操作者会话号（房主 = 0）。远端据此解析"操作者"（Friend 实例）。</summary>
        [Key(0)] public uint Sid;
        [Key(1)] public int SyncId;
    }''',
        '''    [MessagePackObject]
    public class FurnitureOperateMsg
    {
        /// <summary>权威标识：操作者会话号（房主 = 0）。远端据此解析"操作者"（Friend 实例）。</summary>
        [Key(0)] public uint Sid;
        [Key(1)] public int SyncId;

        /// <summary>状态的 int 编码（可选，2026-10-11 追加）。接收端**优先**用 <see cref="StateKey"/> 自己解析，
        /// 这个只在对方没带 Key 时兜底。默认 0 ⇒ 旧发送端 = "无状态负载"。</summary>
        [Key(2)] public int State;
        /// <summary>状态的原始键（可选，如管道的 <c>PipeWait</c>/<c>PipeError</c>）。
        /// <para>▍非空 = 这是一条**状态**同步（不是操作重放）：接收端走 <c>ApplyRemoteState</c>，**不调用</b> <c>Operate()</c>。
        /// 用于"改了状态但没有人交互"的场合（任务把管道打成 PipeError）。</para></summary>
        [Key(3)] public string StateKey;
    }''')

# =====================================================================
# 3. NetRoomFlow：事件 / 注册 / 注销 / Send / Handle + 家具 Send 扩参
# =====================================================================
NRF = 'Assets/Scripts/07NetGame/NetRoomFlow.cs'
if not F(NRF).has('PatrolSpawnNtf'):
    f = F(NRF)
    f.patch(
        'flow.event.patrol',
        '''        /// <summary>【双向】按键终端(KeyScreen)阶段推进（键 = 终端家具 SyncId）⇒ 其余端只应用、不自行推进。</summary>
        public static event Action<KeyScreenStageMsg> OnKeyScreenStage;''',
        '''        /// <summary>【双向】按键终端(KeyScreen)阶段推进（键 = 终端家具 SyncId）⇒ 其余端只应用、不自行推进。</summary>
        public static event Action<KeyScreenStageMsg> OnKeyScreenStage;

        /// <summary>【成员侧】房主广播的巡逻队生成（生成点 + 逐单位目标点）⇒ 本端复刻同一支（键 = 生成点）。</summary>
        public static event Action<PatrolSpawnMsg> OnPatrolSpawn;''')

    f.patch(
        'flow.reg.patrol',
        '''            MessageCenter.Register<KeyScreenStageMsg>(CmdId.KeyScreenStageNtf, HandleKeyScreenStageNtf);''',
        '''            MessageCenter.Register<KeyScreenStageMsg>(CmdId.KeyScreenStageNtf, HandleKeyScreenStageNtf);
            // 巡逻队生成（房主权威；成员复刻）
            MessageCenter.Register<PatrolSpawnMsg>(CmdId.PatrolSpawnNtf, HandlePatrolSpawnNtf);''')

    f.patch(
        'flow.unreg.patrol',
        '''            MessageCenter.Unregister(CmdId.KeyScreenStageNtf);''',
        '''            MessageCenter.Unregister(CmdId.KeyScreenStageNtf);
            MessageCenter.Unregister(CmdId.PatrolSpawnNtf);''')

    f.patch(
        'flow.send.patrol',
        '''        private void HandleKeyScreenStageNtf(KeyScreenStageMsg m)
        {
            if (m != null) OnKeyScreenStage?.Invoke(m);
        }''',
        '''        private void HandleKeyScreenStageNtf(KeyScreenStageMsg m)
        {
            if (m != null) OnKeyScreenStage?.Invoke(m);
        }

        /// <summary>巡逻队生成：**只有房主发**（房主权威的生成事件）。</summary>
        public void SendPatrolSpawn(PatrolSpawnMsg msg)
        {
            if (!IsHost || msg == null) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.PatrolSpawnNtf, msg));
        }

        private void HandlePatrolSpawnNtf(PatrolSpawnMsg m)
        {
            if (m != null) OnPatrolSpawn?.Invoke(m);
        }''')

    f.patch(
        'flow.send.furnstate',
        '''        /// <summary>家具交互：成员上报房主 / 房主转发全体（由 09 侧的 NetFurnitureBridge 决定走向）。</summary>
        public void SendFurnitureOperate(int syncId, uint sid)
        {
            var msg = MessageCenter.Pack(CmdId.FurnitureOperateNtf,
                new FurnitureOperateMsg { SyncId = syncId, Sid = sid });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else NetSvc.Instance?.SendMsg(msg);
        }''',
        '''        /// <summary>家具交互：成员上报房主 / 房主转发全体（由 09 侧的 NetFurnitureBridge 决定走向）。
        /// <para>▍<paramref name="stateKey"/> 非空 = 这是一条**状态**同步（不是交互重放）：接收端走
        /// <c>Furniture_Attached.ApplyRemoteState</c> 而不是 <c>Operate()</c> —— 用于"改了状态但没人交互"
        /// （如任务把管道打成 <c>PipeError</c>）。<paramref name="state"/> 只是编码，接收端可用 Key 自行解析。</para></summary>
        public void SendFurnitureOperate(int syncId, uint sid, int state = 0, string stateKey = null)
        {
            var msg = MessageCenter.Pack(CmdId.FurnitureOperateNtf,
                new FurnitureOperateMsg { SyncId = syncId, Sid = sid, State = state, StateKey = stateKey });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else NetSvc.Instance?.SendMsg(msg);
        }''')

# =====================================================================
# 4. WaveManager：CreatPatrol 成员闸门 + 复刻入口 + 挂巡逻桥
# =====================================================================
WM = 'Assets/Scripts/09Manager/Battle/WaveManager.cs'
if not F(WM).has('applyingRemotePatrol'):
    f = F(WM)
    f.patch(
        'wm.install.patrol',
        '''            NetKeyScreenBridge.Install();     // 按键终端阶段推进（驱动端上报 / 其余端只应用）''',
        '''            NetKeyScreenBridge.Install();     // 按键终端阶段推进（驱动端上报 / 其余端只应用）
            NetPatrolBridge.Install();        // 巡逻队生成（房主权威；成员复刻）''')

    f.patch(
        'wm.uninstall.patrol',
        '''            NetKeyScreenBridge.Uninstall();''',
        '''            NetKeyScreenBridge.Uninstall();
            NetPatrolBridge.Uninstall();''')

    # 删掉上一轮留的 TODO 三行（按行首前缀定位，避开中文引号）
    f.drop_from('            // ⚠ TODO（2026-10-11 用户拍板', 3)

    f.patch(
        'wm.creatpatrol.gate',
        '''        public List<GameObject> CreatPatrol(Vector3 pos)
        {''',
        '''        /// <summary>正在"复刻房主广播的巡逻队"（见 <see cref="CreatPatrolRemote"/>）：跳过本端的成员闸门。</summary>
        bool applyingRemotePatrol;

        public List<GameObject> CreatPatrol(Vector3 pos)
        {
            // ★ 联机：巡逻队是**房主权威**（2026-10-11）—— 成员端不在本地造：`Actor.Awake` 会给新单位分配 NetId，
            //   而 NetId 的口径是"房主分配 + 两端创建顺序一致" ⇒ 成员端自造就会与房主下发的号段撞号（移错位/错杀）。
            //   成员端等房主广播（4047）后由 NetPatrolBridge 调 CreatPatrolRemote 复刻（同 CreatWave 的 applyingRemoteWave 模式）。
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            bool online = flow != null && (flow.IsHost || flow.SelfSid != 0u);
            if (!applyingRemotePatrol && online && !flow.IsHost) return new List<GameObject>();
''')

    f.patch(
        'wm.creatpatrol.remote',
        '''            var re = new List<GameObject>();
            var temp = Patrol.WeightTake(100, patrolRandom);
            temp.ForEach(item => re.Add(CreatUnit(item, pos, 5, false)));
            return re;
        }''',
        '''            var re = new List<GameObject>();
            var temp = Patrol.WeightTake(100, patrolRandom);
            temp.ForEach(item => re.Add(CreatUnit(item, pos, 5, false)));
            return re;
        }

        /// <summary>【联机】按房主广播复刻一支巡逻队（跳过成员闸门）。返回本端新建的单位，**顺序与房主一致**
        /// （构成由落点派生流决定 ⇒ 同落点同顺序），供 <c>NetPatrolBridge</c> 逐单位设目标点。</summary>
        public List<GameObject> CreatPatrolRemote(Vector3 pos)
        {
            applyingRemotePatrol = true;
            try { return CreatPatrol(pos); }
            finally { applyingRemotePatrol = false; }
        }''')

# =====================================================================
# 5. NetPatrolBridge（新，09）
# =====================================================================
NEW_PATROL = '''using System.Collections.Generic;
using FPSGame.AI;
using FPSGame.Gameplay;
using FPSGame.Net;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>巡逻队目标点语义（与 4047 的 <c>Kind</c> 一致）。</summary>
    public enum PatrolTargetKind
    {
        /// <summary>逐单位目标点（<c>EnemyController.PatrolPos</c>，据点压力巡逻）。</summary>
        PatrolPos = 0,
        /// <summary>全体同一处（<c>EnemyController.HomePoint</c>，任务维修巡逻）。</summary>
        HomePoint = 1,
    }

    /// <summary>
    /// 联机 · 巡逻队生成同步桥（09）：**房主权威** —— 房主造，成员按同一生成点复刻。
    ///
    /// <para>▍为什么必须房主权威：巡逻队也是敌人（<c>Actor.Type == Enemy</c>）⇒ <c>Awake</c> 会从
    /// <c>ActorsManager._enemyNetIdSeq</c> 领 NetId。NetId 的前提是"房主分配 + 两端创建顺序一致"；
    /// 成员端若能自己造，号段立刻错位 ⇒ 之后房主下发的 <c>EnemyMoveSync</c>/<c>EnemyDiedSync</c> 会作用到**别的单位**上
    /// （怪自己走开、或者凭空死掉）。</para>
    ///
    /// <para>▍为什么成员端"复刻"而不是"收位置"：敌人是各端本地模拟的（房主只下发移动意图/生死），
    /// 成员端必须有这份实体。构成不用传 —— <c>WaveManager.CreatPatrol</c> 已按**落点位置**派生随机流，
    /// 同落点 ⇒ 同构成、同顺序。唯一必须传的是**逐单位目标点**（它由房主侧的几何/玩家位置算出）。</para>
    ///
    /// <para>▍入口两个：09 自己的 <c>PatrolContriller</c> 直接调 <see cref="Report"/>；
    /// 06 的任务（看不见 09）经 <c>BattleEventBus.ReportPatrolSpawn</c> 中转（见 <see cref="OnLocalReport"/>）。</para>
    ///
    /// <para>▍每局一份，由 <see cref="WaveManager"/> Install/Uninstall。</para>
    /// </summary>
    public static class NetPatrolBridge
    {
        static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;

            NetRoomFlow.OnPatrolSpawn += OnRemoteSpawn;
            BattleEventBus.OnPatrolSpawnReport += OnLocalReport;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            NetRoomFlow.OnPatrolSpawn -= OnRemoteSpawn;
            BattleEventBus.OnPatrolSpawnReport -= OnLocalReport;
        }

        #region 本端 -> 网络

        /// <summary>【房主】报告一支巡逻队已生成（成员端据此复刻）。非房主 / 单机调用直接返回。</summary>
        public static void Report(Vector3 spawnPos, IList<Vector3> targets, PatrolTargetKind kind)
        {
            if (targets == null || targets.Count == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.IsHost) return;

            var arr = new float[targets.Count * 3];
            for (int i = 0; i < targets.Count; ++i)
            {
                arr[i * 3] = targets[i].x;
                arr[i * 3 + 1] = targets[i].y;
                arr[i * 3 + 2] = targets[i].z;
            }

            FPSGame.Utils.NetSyncLog.SyncLog("巡逻/发",
                $"生成点={spawnPos:F1} 单位={targets.Count} kind={kind}");

            flow.SendPatrolSpawn(new PatrolSpawnMsg
            {
                X = spawnPos.x, Y = spawnPos.y, Z = spawnPos.z,
                Kind = (int)kind,
                Targets = arr,
            });
        }

        /// <summary>06 侧（任务）的报告入口：任务看不见 09 ⇒ 经 <c>BattleEventBus</c> 中转到这里统一走房主权威。</summary>
        static void OnLocalReport(Vector3 spawnPos, Vector3 target, bool home)
            => Report(spawnPos, new[] { target }, home ? PatrolTargetKind.HomePoint : PatrolTargetKind.PatrolPos);

        #endregion

        #region 网络 -> 本端

        static void OnRemoteSpawn(PatrolSpawnMsg m)
        {
            if (m == null) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || flow.IsHost) return;      // 房主自己那份上面已经建过（不做本地自派发）

            var battle = BattleManager.Instance;
            var wave = battle != null ? battle.WaveCont : null;
            if (wave == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("巡逻/收", "本端没有 WaveManager（不在战斗场景？）⇒ 这支巡逻队被丢弃");
                return;
            }

            var spawnPos = new Vector3(m.X, m.Y, m.Z);
            var units = wave.CreatPatrolRemote(spawnPos);
            if (units == null || units.Count == 0)
            {
                FPSGame.Utils.NetSyncLog.Warn("巡逻/收", $"生成点={spawnPos:F1} 复刻出 0 个单位（构成对不上？）");
                return;
            }

            var kind = (PatrolTargetKind)m.Kind;
            var arr = m.Targets;
            int groups = (arr != null) ? arr.Length / 3 : 0;

            for (int i = 0; i < units.Count; ++i)
            {
                var go = units[i];
                if (go == null || !go.TryGetComponent(out EnemyController c)) continue;

                Vector3 target;
                if (groups <= 0) target = spawnPos;
                else
                {
                    // HomePoint：全体同一处（取第 0 组）；PatrolPos：逐单位（不足时退回第 0 组，别越界）
                    int gi = (kind == PatrolTargetKind.HomePoint || i >= groups) ? 0 : i;
                    target = new Vector3(arr[gi * 3], arr[gi * 3 + 1], arr[gi * 3 + 2]);
                }

                if (kind == PatrolTargetKind.HomePoint) c.HomePoint = target;
                else c.PatrolPos = target;
            }

            FPSGame.Utils.NetSyncLog.SyncLog("巡逻/收",
                $"生成点={spawnPos:F1} 单位={units.Count} kind={kind}");
        }

        #endregion
    }
}
'''

# =====================================================================
# 6. NetFurnitureBridge：状态上报 / 状态应用
# =====================================================================
NFB = 'Assets/Scripts/09Manager/Battle/NetFurnitureBridge.cs'
if not F(NFB).has('OnLocalStateChanged'):
    f = F(NFB)
    f.patch(
        'fb.install.state',
        '''            GlobalEventBus.OnFurnitureOperate += OnLocalOperate;
            NetRoomFlow.OnFurnitureOperate += OnRemoteOperate;''',
        '''            GlobalEventBus.OnFurnitureOperate += OnLocalOperate;
            Furniture_Attached.OnStateChanged += OnLocalStateChanged;   // 家具"状态"变更（无交互驱动的那种）
            NetRoomFlow.OnFurnitureOperate += OnRemoteOperate;''')

    f.patch(
        'fb.uninstall.state',
        '''            GlobalEventBus.OnFurnitureOperate -= OnLocalOperate;
            NetRoomFlow.OnFurnitureOperate -= OnRemoteOperate;
            _friendBridge = null;''',
        '''            GlobalEventBus.OnFurnitureOperate -= OnLocalOperate;
            Furniture_Attached.OnStateChanged -= OnLocalStateChanged;
            NetRoomFlow.OnFurnitureOperate -= OnRemoteOperate;
            _friendBridge = null;''')

    f.patch(
        'fb.report.state',
        '''        /// <summary>日志用的一句话描述（Id / SyncId / 物体名）。</summary>''',
        '''        /// <summary>家具"状态"变更（子类调 <c>Furniture_Attached.ReportState</c>）：走同一条 4036 上行。
        /// <para>▍与交互路径的区别：那条重放 <c>Operate()</c>；这条只带状态（<c>StateKey</c> 非空）⇒ 接收端直接置位。</para></summary>
        static void OnLocalStateChanged(Furniture_Attached f, int code, string key)
        {
            if (NetApplyGuard.Applying) return;          // 远端应用触发的（ApplyRemoteState 里又改了状态）：别再上报
            if (f == null || f.SyncId == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;    // 单机 / 不在房里：两端本来就一致

            FPSGame.Utils.NetSyncLog.SyncLog("同步/家具/状态·发",
                $"{Describe(f, f)} code={code} key={key}");
            flow.SendFurnitureOperate(f.SyncId, flow.IsHost ? 0u : flow.SelfSid, code, key);
        }

        /// <summary>日志用的一句话描述（Id / SyncId / 物体名）。</summary>''')

    f.patch(
        'fb.apply.state',
        '''            var remoteUser = ResolveRemoteUser(m.Sid);

            FPSGame.Utils.NetSyncLog.SyncLog("家具/操作·收", $"{Describe(furn, furn)} 远端操作者={(remoteUser != null ? remoteUser.name : "<null>")}" +''',
        '''            // ★ 状态同步（StateKey 非空）：直接应用状态，**不重放 Operate()**（那是交互路径的事）
            if (!string.IsNullOrEmpty(m.StateKey))
            {
                FPSGame.Utils.NetSyncLog.SyncLog("家具/状态·收", $"{Describe(furn, furn)} → {m.StateKey}（发起 sid={m.Sid}）");
                NetApplyGuard.Begin();
                try { furn.ApplyRemoteState(m.State, m.StateKey); }
                finally { NetApplyGuard.End(); }
                return;
            }

            var remoteUser = ResolveRemoteUser(m.Sid);

            FPSGame.Utils.NetSyncLog.SyncLog("家具/操作·收", $"{Describe(furn, furn)} 远端操作者={(remoteUser != null ? remoteUser.name : "<null>")}" +''')

# =====================================================================
# 7. PatrolContriller：报告生成（生成点 + 逐单位目标点）
# =====================================================================
PC = 'Assets/Scripts/09Manager/Battle/PatrolContriller.cs'
if False:  # PC 段移到 net_sync_b1b.py（原锚点含空行、整段匹配不稳）
    F(PC).patch(
        'pc.report',
        '''            foreach (var item in list)
            {
                // ⚠ 旧实现用 ForEach + GetComponent，任一个单位取不到控制器就抛 NRE 中断整个循环，
                //   后面的单位 PatrolPos 保持 default ⇒ EnemyMobile.Start 直接进 Idle 发呆
                if (!item || !item.TryGetComponent(out EnemyController controller)) continue;

                Vector3 unitPos = controller.transform.position;
                // ⚠ 散开偏移只加在 XZ 上：Vector2.ToVector3() 的 y 恒为 0，把 3D 偏移一起加会把目标拖到地下几十米
                Vector3 point = new Vector3(
                    baseTarget.x + (spawnPos.x - unitPos.x),
                    baseTarget.y,
                    baseTarget.z + (spawnPos.z - unitPos.z));

                controller.PatrolPos = UnityEngine.AI.NavMesh.SamplePosition(
                    point, out var hit, PATROL_TARGET_UNIT_SAMPLE_RADIUS, UnityEngine.AI.NavMesh.AllAreas)
                    ? hit.position
                    : baseTarget;
            }
        }''',
        '''            // ★ 联机（2026-10-11）：逐单位目标点收集起来交给桥 —— 房主广播后成员端才能复刻出同一支（4047）。
            //   成员端这一步不会走到：CreatPatrol 有成员闸门 ⇒ list 为空 ⇒ 上面的早退已经 return。
            var targets = new List<Vector3>(list.Count);

            foreach (var item in list)
            {
                // ⚠ 旧实现用 ForEach + GetComponent，任一个单位取不到控制器就抛 NRE 中断整个循环，
                //   后面的单位 PatrolPos 保持 default ⇒ EnemyMobile.Start 直接进 Idle 发呆
                if (!item || !item.TryGetComponent(out EnemyController controller)) continue;

                Vector3 unitPos = controller.transform.position;
                // ⚠ 散开偏移只加在 XZ 上：Vector2.ToVector3() 的 y 恒为 0，把 3D 偏移一起加会把目标拖到地下几十米
                Vector3 point = new Vector3(
                    baseTarget.x + (spawnPos.x - unitPos.x),
                    baseTarget.y,
                    baseTarget.z + (spawnPos.z - unitPos.z));

                Vector3 unitTarget = UnityEngine.AI.NavMesh.SamplePosition(
                    point, out var hit, PATROL_TARGET_UNIT_SAMPLE_RADIUS, UnityEngine.AI.NavMesh.AllAreas)
                    ? hit.position
                    : baseTarget;

                controller.PatrolPos = unitTarget;
                targets.Add(unitTarget);
            }

            NetPatrolBridge.Report(spawnPos, targets, PatrolTargetKind.PatrolPos);
        }''')

# =====================================================================
# 8. BattleEventBus：巡逻队报告事件（06 -> 09 中转）
# =====================================================================
BEB = 'Assets/Scripts/06Gameplay/Events/BattleEventBus.cs'
if not F(BEB).has('OnPatrolSpawnReport'):
    F(BEB).patch(
        'beb.patrol',
        '''        /// <summary>记录战斗数据条目（玩家序号 / 条目名）</summary>
        public static event Action<int, string> OnAddBattleDataItem;
        public static void AddBattleDataItem(int playerIndex, string name) => OnAddBattleDataItem?.Invoke(playerIndex, name);''',
        '''        /// <summary>记录战斗数据条目（玩家序号 / 条目名）</summary>
        public static event Action<int, string> OnAddBattleDataItem;
        public static void AddBattleDataItem(int playerIndex, string name) => OnAddBattleDataItem?.Invoke(playerIndex, name);

        /// <summary>报告"一支巡逻队已生成"（生成点 / 目标点 / 是否设 <c>HomePoint</c>）。
        /// <para>▍为什么走事件：任务在 <c>06_Gameplay</c>、联机桥在 <c>09_Managers</c>（06 看不见 09）⇒
        /// 由 <c>NetPatrolBridge</c> 订阅后统一决定"房主广播 / 成员复刻"。</para></summary>
        public static event Action<Vector3, Vector3, bool> OnPatrolSpawnReport;
        public static void ReportPatrolSpawn(Vector3 spawnPos, Vector3 target, bool home)
            => OnPatrolSpawnReport?.Invoke(spawnPos, target, home);''')

# =====================================================================
# 9. MissionOilRefining：纯函数种子 + 报告巡逻队
# =====================================================================
MOR = 'Assets/Scripts/06Gameplay/Mission/Main/MissionOilRefining.cs'
if not F(MOR).has('repairSeed'):
    f = F(MOR)

    # 上一轮留的 TODO 四行（现在已实现）删掉
    f.drop_from('            // ⚠ TODO（2026-10-11 用户拍板', 4)

    f.patch(
        'oil.fields',
        '''        /// <summary>上一次刷新提示时的已修复数量，避免每 tick 重复刷新 UI</summary>
        private int fixedPipeCount = -1;''',
        '''        /// <summary>上一次刷新提示时的已修复数量，避免每 tick 重复刷新 UI</summary>
        private int fixedPipeCount = -1;

        /// <summary>本局"确定性修复"的种子（0 = 单机/无种子 ⇒ 走全局随机流，行为与改造前一致）。</summary>
        private int repairSeed;''')

    f.patch(
        'oil.errortimes',
        '''            errorTimes = new() {0};
            int now = 0,min,max;''',
        '''            errorTimes = new() {0};
            // ★ 联机确定性（2026-10-11）：修坏管的时间点用**纯函数**（本局种子 + 任务稳定键 + 序号）生成 ⇒ 两端恒等；
            //   单机（Seed == 0）保持原来的全局随机流。
            repairSeed = ResolveRepairSeed();
            int now = 0,min,max;''')

    f.patch(
        'oil.loop',
        '''            while (now<150)
            {
                now += random.Range(min, max+1);
                if (now < 150) errorTimes.Add(now);
            }''',
        '''            int repairIndex = 0;
            while (now<150)
            {
                now += repairSeed != 0
                    ? DeriveRange(repairSeed, repairIndex++, min, max + 1)
                    : random.Range(min, max+1);
                if (now < 150) errorTimes.Add(now);
            }''')

    f.patch(
        'oil.pick',
        '''            foreach (var sub in subCP)
            {
                if (!sub || sub.pipes == null) continue;
                var items = random.RandomOrdering(sub.pipes).Take(Mathf.Clamp(sub.pipes.Count/5,1,2));''',
        '''            int subIndex = 0;
            foreach (var sub in subCP)
            {
                if (!sub || sub.pipes == null) continue;
                var items = PickRepairPipes(sub.pipes, Mathf.Clamp(sub.pipes.Count/5,1,2), subIndex++);''')

    f.patch(
        'oil.helpers',
        '''        /// <summary>为受损管道生成一支以它为巡逻目标点的巡逻队</summary>
        private void CreatRepairPatrol(Furniture_Pipe pipe)
        {
            Vector3 target = pipe.transform.position;
            var units = manager.CreatPatrol(GetPatrolSpawnPos(target));
            foreach (var unit in units)
            {
                if (!unit || !unit.TryGetComponent(out EnemyController controller)) continue;
                controller.HomePoint = target;
            }
        }''',
        '''        /// <summary>由"本局种子 + 任务稳定键(<see cref="MissionBase.netOrder"/>) + 阶段"派生本任务的修复流。</summary>
        private int ResolveRepairSeed()
        {
            int seed = FPSGame.Data.TaskState.Seed;
            if (seed == 0) return 0;
            return SeedUtil.Derive(SeedUtil.Derive(seed, (int)SeedStream.MissionEntity * 1000 + netOrder), 7771);
        }

        /// <summary>纯函数取 [min,max) 内的整数（同 seed+index ⇒ 恒等；**重试/调用次数差异都不影响**，
        /// 这正是不能用"一条 System.Random 流"的原因 —— 流会因为消费次数不同而错位）。</summary>
        private static int DeriveRange(int seed, int index, int min, int max)
        {
            if (max <= min) return min;
            int h = SeedUtil.Derive(seed, index);
            return min + (int)(unchecked((uint)h) % (uint)(max - min));
        }

        /// <summary>挑本次维修要打坏的管道：联机走**纯函数**（按 (种子, 阶段, 子任务序, 管序) 排序取前 n），
        /// 单机保持原来的 <c>random.RandomOrdering</c> 行为。</summary>
        private List<Furniture_Pipe> PickRepairPipes(List<Furniture_Pipe> pipes, int count, int subIndex)
        {
            if (repairSeed == 0) return random.RandomOrdering(pipes).Take(count).ToList();

            var order = new List<int>(pipes.Count);
            for (int i = 0; i < pipes.Count; ++i) order.Add(i);

            int stageKey = SeedUtil.Derive(repairSeed, battleStage * 1000 + subIndex);
            order.Sort((a, b) =>
            {
                int ha = SeedUtil.Derive(stageKey, a);
                int hb = SeedUtil.Derive(stageKey, b);
                int c = unchecked((uint)ha).CompareTo(unchecked((uint)hb));
                return c != 0 ? c : a.CompareTo(b);   // ⚠ 哈希相等时按原始下标 ⇒ 稳定，避免两端顺序不同
            });

            var re = new List<Furniture_Pipe>();
            for (int i = 0; i < count && i < order.Count; ++i) re.Add(pipes[order[i]]);
            return re;
        }

        /// <summary>为受损管道生成一支以它为巡逻目标点的巡逻队</summary>
        private void CreatRepairPatrol(Furniture_Pipe pipe)
        {
            Vector3 target = pipe.transform.position;
            Vector3 spawn = GetPatrolSpawnPos(target);
            var units = manager.CreatPatrol(spawn);
            foreach (var unit in units)
            {
                if (!unit || !unit.TryGetComponent(out EnemyController controller)) continue;
                controller.HomePoint = target;
            }

            // ★ 联机（2026-10-11）：巡逻队是房主权威 —— 房主把"生成点 + HomePoint"报给桥，成员端复刻同一支（4047）。
            //   ⚠ 成员端这一句是空操作（CreatPatrol 已被闸门挡成空表 + 桥只认房主），不会重复生成。
            if (units != null && units.Count > 0)
                FPSGame.Gameplay.BattleEventBus.ReportPatrolSpawn(spawn, target, true);
        }''')

# =====================================================================
# 10. Furniture_Attached：状态事件 / IsRemoteReplay / 键日志
# =====================================================================
FA = 'Assets/Scripts/06Gameplay/Interactable/Furniture_Attached.cs'
if not F(FA).has('IsRemoteReplay'):
    f = F(FA)
    f.patch(
        'fa.apply',
        '''        public void ApplyRemoteOperate(GameObject remoteUser)
        {
            owner = remoteUser;
            Operate();
        }''',
        '''        public void ApplyRemoteOperate(GameObject remoteUser)
        {
            owner = remoteUser;
            // ⚠ 重放期间置位：某些"本端表现"副作用（改地形高度图 / 重算包围盒）**不能两端各做一遍**（不是幂等、会叠加）
            IsRemoteReplay = true;
            try { Operate(); }
            finally { IsRemoteReplay = false; }
        }

        /// <summary>true = 当前在"重放远端交互"的调用栈内（见 <see cref="ApplyRemoteOperate"/>）。
        /// <para>▍用途：把"只该由本机操作者做一次"的副作用挡掉（<c>Furniture_Pipe</c> 的 <c>ModifyHeightMap</c> 是典型：
        /// 它是往地形高度图上挖坑，两端各做一次就是挖两遍）。</para></summary>
        public static bool IsRemoteReplay { get; private set; }

        /// <summary>家具"需要两端一致的状态"发生了变化（子类调用 <see cref="ReportState"/> 触发）。
        /// <para>参数：(家具, int 编码, 原始键)。由 09 侧 <c>NetFurnitureBridge</c> 订阅后走 4036 上行。</para></summary>
        public static event Action<Furniture_Attached, int, string> OnStateChanged;

        /// <summary>子类在改完"两端必须一致"的状态后调用（如管道的 PipeLink / PipeWait / PipeError）。
        /// <para>▍只用于**没有交互驱动**的状态变更（任务把管道打成 PipeError）；交互路径本身走 4036 重放，不用调这个。</para></summary>
        protected void ReportState(int code, string key = null)
        {
            OnStateChanged?.Invoke(this, code, key);
        }

        /// <summary>【联机】应用远端下发的状态（与 <see cref="ReportState"/> 成对，走同一条 4036）。
        /// <para>默认空实现；有状态机的子类覆写它（如 <c>Furniture_Pipe</c> 还原 <c>Id</c>）。</para></summary>
        public virtual void ApplyRemoteState(int code, string key) { }''')

    f.patch(
        'fa.keylog',
        '''            SyncId = Fnv1a($"{Id}|{qx}|{qy}|{qz}");''',
        '''            SyncId = Fnv1a($"{Id}|{qx}|{qy}|{qz}");
            // 联机诊断（可开关）：跨端稳定键在创建时打一条 —— 两端对不上时用它和 NetFurnitureBridge 的键表对照。
            FPSGame.Utils.NetSyncLog.SyncLog("家具/键", $"{name} id={Id} sync={SyncId} 量化位置=({qx},{qy},{qz})");''')

# =====================================================================
# 11. Furniture_Pipe：状态上报 / 应用 + 远端重放跳过地形副作用
# =====================================================================
FP = 'Assets/Scripts/06Gameplay/Interactable/Furniture_Pipe.cs'
if not F(FP).has('ApplyRemoteState'):
    f = F(FP)
    f.patch(
        'pipe.setid',
        '''        private void SetId(string value)
        {
        if (Identity.IsValidMono()) Identity.Id = value;
        }''',
        '''        /// <summary>状态名的 int 编码（接收端**优先**用原始串解析，这个只在对方没带 Key 时兜底）。</summary>
        public const int StatePipe = 0, StateLink = 1, StateWait = 2, StateComplete = 3, StateError = 4;

        private void SetId(string value)
        {
        if (Identity.IsValidMono()) Identity.Id = value;
        // ★ 联机（2026-10-11）：管道状态（决定 CanOperate 与任务进度）必须两端一致 ⇒ 上报（键 = 家具 SyncId，值 = 状态名）。
        //   没有交互驱动的那条路径（任务 EnterRepair 直接调 Error()）也能靠它传出去。
        ReportState(EncodeState(value), value);
        }

        private static int EncodeState(string value)
        {
            switch (value)
            {
                case "Pipe": return StatePipe;
                case "PipeLink": return StateLink;
                case "PipeWait": return StateWait;
                case "PipeComplete": return StateComplete;
                case "PipeError": return StateError;
                default: return -1;
            }
        }

        private static string DecodeState(int code)
        {
            switch (code)
            {
                case StatePipe: return "Pipe";
                case StateLink: return "PipeLink";
                case StateWait: return "PipeWait";
                case StateComplete: return "PipeComplete";
                case StateError: return "PipeError";
                default: return null;
            }
        }

        /// <summary>【联机】应用远端状态：还原到同一个 <c>Id</c>（<c>CanOperate</c> 与任务的判定随之一致）。</summary>
        public override void ApplyRemoteState(int code, string key)
        {
            string value = !string.IsNullOrEmpty(key) ? key : DecodeState(code);
            if (string.IsNullOrEmpty(value)) return;
            SetId(value);
        }''')

    f.patch(
        'pipe.place.skip',
        '''        if (pipeSkeleton) pipeSkeleton.sharedMaterial = pipeSkin.sharedMaterial;
        inOperate = false;
        SetId("PipeLink");
        audioOper = null;
        if (pipeSkin) FpsHelper.UpdatePipeBounds(pipeSkin.GetComponent<SkinnedMeshRenderer>(),transform);
        if (pipeSkeleton) FpsHelper.UpdatePipeBounds(pipeSkeleton.GetComponent<SkinnedMeshRenderer>(),transform);''',
        '''        if (pipeSkeleton) pipeSkeleton.sharedMaterial = pipeSkin.sharedMaterial;
        inOperate = false;
        SetId("PipeLink");
        audioOper = null;
        // ⚠ 下面这些是"本端表现 / 会改地形"的副作用：**远端重放时跳过** —— 两端各做一次会叠加
        //   （ModifyHeightMap 是往地形高度图上挖坑，不是幂等操作）。
        if (!IsRemoteReplay)
        {
            if (pipeSkin) FpsHelper.UpdatePipeBounds(pipeSkin.GetComponent<SkinnedMeshRenderer>(),transform);
            if (pipeSkeleton) FpsHelper.UpdatePipeBounds(pipeSkeleton.GetComponent<SkinnedMeshRenderer>(),transform);
        }''')

    f.patch(
        'pipe.terrain.skip',
        '''                item.enabled = true;
                item.gameObject.layer = 0;
                //ModifyHeightMap 是协程（迭代器），必须用 StartCoroutine 启动，直接调用不会执行
                FPSGame.Core.TimerHost.RunCoroutine(TerrainUtils.ModifyHeightMap(item.transform.position,0, 2f,0.5f, ShapeType.Circle, false,false));''',
        '''                item.enabled = true;
                item.gameObject.layer = 0;
                //ModifyHeightMap 是协程（迭代器），必须用 StartCoroutine 启动，直接调用不会执行
                // ⚠ 远端重放要跳过：这是"改本端地形高度图"的副作用，两端各挖一次会叠加（见 IsRemoteReplay）。
                if (!IsRemoteReplay)
                    FPSGame.Core.TimerHost.RunCoroutine(TerrainUtils.ModifyHeightMap(item.transform.position,0, 2f,0.5f, ShapeType.Circle, false,false));''')

# =====================================================================
# 12~15. 小修：雷达站 / Furniture_General 注释 / 两个未退订
# =====================================================================
MLS = 'Assets/Scripts/06Gameplay/Mission/Extra/MissionLidarStation.cs'
if not F(MLS).has('OnComple.AddListener(OnKeyScreenComple)'):
    F(MLS).patch(
        'lidar.fix',
        '''            keyScreen = entity.transform.GetComponentInChildren<KeyScreen>();
            keyScreen.OnComple.RemoveListener(OnKeyScreenComple);''',
        '''            keyScreen = entity.transform.GetComponentInChildren<KeyScreen>();
            // ⚠ 2026-10-11 修：原来写的是 RemoveListener（在 StartMission 里退订一个从没订阅过的回调）
            //   ⇒ 终端完成事件永远打不到本任务 ⇒ 雷达站任务永远完不成、RevealAllMissions 也不触发。
            keyScreen.OnComple.AddListener(OnKeyScreenComple);''')

FG = 'Assets/Scripts/06Gameplay/Interactable/Furniture_General.cs'
if not F(FG).has('不调用 <c>base.Operate()</c>'):
    F(FG).patch(
        'fg.note',
        '''    public override void Operate() {
        action._Operate?.Invoke(this);
    }''',
        '''    /// <summary>⚠ 本覆写**不调用 <c>base.Operate()</c>**：交互事件（4036 同步的 <c>GlobalEventBus.FurnitureOperate</c>）、
    /// <c>inOperate</c> 置位、语音都在基类里 ⇒ **只配了 <c>_InOperateUpdate</c>/<c>_CanOperate</c> 而没有 <c>_Operate</c>
    /// 的家具会"点了没反应"**（<c>SignaTower</c> 就是：它的旋转依赖 <c>inOperate == true</c>，没人置真 ⇒ 旋转不生效、
    /// 联机 4036 也永远不会为它上报）。新增配置项时：要么给 <c>_Operate</c>，要么在 lambda 里显式调 <c>furn.BaseOp()</c>。</summary>
    public override void Operate() {
        action._Operate?.Invoke(this);
    }''')

MB = 'Assets/Scripts/06Gameplay/Mission/Extra/MissionBroadcast.cs'
if not F(MB).has('protected override void Uninit()'):
    F(MB).patch(
        'mb.uninit',
        '''        void OnTowerDeath()
        {
            Debug.Log("单位死亡，完成" + gameObject + entity.transform, entity.transform);
            tower.OnDie -= OnTowerDeath;
            keyScreen.furn.canOperate = false;
            CompleteMission();
        }''',
        '''        void OnTowerDeath()
        {
            Debug.Log("单位死亡，完成" + gameObject + entity.transform, entity.transform);
            tower.OnDie -= OnTowerDeath;
            keyScreen.furn.canOperate = false;
            CompleteMission();
        }

        /// <summary>⚠ 成对退订（2026-10-11 补）：基类 <c>Uninit</c> 不管这两条 ⇒ 原来是"任务结束了、
        /// 事件还挂在塔和终端上"（塔先倒下时 <c>OnKeyScreenComple</c> 甚至会打到已收尾的任务上）。</summary>
        protected override void Uninit()
        {
            base.Uninit();
            if (tower != null) tower.OnDie -= OnTowerDeath;
            if (keyScreen != null) keyScreen.OnComple.RemoveListener(OnKeyScreenComple);
        }''')

MD = 'Assets/Scripts/06Gameplay/Mission/Main/MissionDefensive.cs'
if not F(MD).has('_targets'):
    f = F(MD)
    f.patch(
        'md.field',
        '''        List<Vector3> wavePoints;
        KeyScreen keyScreen;''',
        '''        List<Vector3> wavePoints;
        KeyScreen keyScreen;

        /// <summary>本任务监听的守方单位（⚠ 成对退订要在 <see cref="Uninit"/> 里做；原来只存在局部变量里 ⇒ 永远退不掉）。</summary>
        private readonly List<I_AIController> _targets = new();''')

    f.patch(
        'md.sub',
        '''            foreach(var item in targets)
            {
                item.OnDamaged += OnTargetDamage;
                item.OnDie += OnTargetDeath;
            }
            m_TargetCount = targets.Count;''',
        '''            foreach(var item in targets)
            {
                item.OnDamaged += OnTargetDamage;
                item.OnDie += OnTargetDeath;
                _targets.Add(item);
            }
            m_TargetCount = targets.Count;''')

    f.patch(
        'md.uninit',
        '''        protected override void Uninit()
        {
            base.Uninit();
            keyScreen.OnUpdateStage -= OnStageChange;
        }''',
        '''        protected override void Uninit()
        {
            base.Uninit();
            keyScreen.OnUpdateStage -= OnStageChange;

            // ⚠ 成对退订守方单位（2026-10-11 补）：原来只退终端阶段，单位事件挂着已结束的任务
            for (int i = 0; i < _targets.Count; ++i)
            {
                var item = _targets[i];
                if (item == null) continue;
                item.OnDamaged -= OnTargetDamage;
                item.OnDie -= OnTargetDeath;
            }
            _targets.Clear();
        }''')

# =====================================================================
# 两阶段提交
# =====================================================================
def main():
    apply = '--apply' in sys.argv

    plan_new = []
    if not os.path.exists(P('Assets/Scripts/09Manager/Battle/NetPatrolBridge.cs')):
        plan_new.append(('Assets/Scripts/09Manager/Battle/NetPatrolBridge.cs', NEW_PATROL))

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
