# -*- coding: utf-8 -*-
"""
联机 P2：家具交互同步（4036）。
- Furniture_Attached：SyncId(FNV1a(Id+位置量化), 创建时缓存) + listBySyncId + ApplyRemoteOperate
- NetFriendBridge：TryGetFriendObject(sid) 供重放时解析"操作者"
- NetRoomFlow(07)：事件 + 收发 + 注册/退订
- WaveManager：Install/Uninstall NetFurnitureBridge

每个 (old,new) 必须在该文件里恰好命中 1 次，否则整脚本中止、不落盘。行尾自适应（各文件 EOL 不统一）。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FA = os.path.join(ROOT, "Assets", "Scripts", "06Gameplay", "Interactable", "Furniture_Attached.cs")
NF = os.path.join(ROOT, "Assets", "Scripts", "09Manager", "Global", "NetFriendBridge.cs")
RF = os.path.join(ROOT, "Assets", "Scripts", "07NetGame", "NetRoomFlow.cs")
WM = os.path.join(ROOT, "Assets", "Scripts", "09Manager", "Battle", "WaveManager.cs")

EDITS = []


def E(path, old, new):
    EDITS.append((path, old, new))


# ---------- Furniture_Attached：SyncId + 跨端索引 ----------
E(FA,
  "    public class Furniture_Attached : BaseMono , IFurniture\n"
  "    {\n"
  "        public static Dictionary<int, IFurniture> list = new();\n"
  "\n"
  "        private static int nowID = 0;\n"
  "        private static int GetID => ++nowID;\n",

  "    public class Furniture_Attached : BaseMono , IFurniture\n"
  "    {\n"
  "        public static Dictionary<int, IFurniture> list = new();\n"
  "\n"
  "        /// <summary>同步键 → 家具 的**跨端稳定**索引（键 = <see cref=\"SyncId\"/>）。联机同步用。</summary>\n"
  "        public static readonly Dictionary<int, Furniture_Attached> listBySyncId = new();\n"
  "\n"
  "        private static int nowID = 0;\n"
  "        private static int GetID => ++nowID;\n"
  "\n"
  "        /// <summary>\n"
  "        /// 跨端稳定的同步键 = <c>FNV1a(身份Id + 世界坐标 0.1m 量化)</c>，**创建时算一次并缓存**。\n"
  "        /// <para>▍为什么不能只用 <c>Id</c>：同类家具存在多个实例（同 Id 多份）。</para>\n"
  "        /// <para>▍为什么不每帧重算：家具会位移，键必须稳定。</para>\n"
  "        /// <para>▍为什么不用 <c>NumberID</c>：那是各端静态自增且从不复位（同 <c>KeyScreenControl</c> 的结论）。</para>\n"
  "        /// <para>▍量化到 0.1m 是为了吃掉两端浮点抖动；位置来自场景 / 已同步的生成落点 ⇒ 跨端一致。</para>\n"
  "        /// </summary>\n"
  "        public int SyncId { get; private set; }\n")

# ---------- Furniture_Attached：Awake 里算一次 + 辅助方法 ----------
E(FA,
  "            ResolveIdentity();\n"
  "\n"
  "            // 首次分配唯一ID\n"
  "            if (NumberID == 0)\n"
  "                NumberID = GetID;\n"
  "        }\n",

  "            ResolveIdentity();\n"
  "\n"
  "            // 首次分配唯一ID\n"
  "            if (NumberID == 0)\n"
  "                NumberID = GetID;\n"
  "\n"
  "            ComputeSyncId();\n"
  "        }\n"
  "\n"
  "        /// <summary>算一次 <see cref=\"SyncId\"/>（必须在 <see cref=\"ResolveIdentity\"/> 之后，因为要用到 <see cref=\"Id\"/>）。</summary>\n"
  "        private void ComputeSyncId()\n"
  "        {\n"
  "            Vector3 p = transform.position;\n"
  "            int qx = Mathf.RoundToInt(p.x * 10f);\n"
  "            int qy = Mathf.RoundToInt(p.y * 10f);\n"
  "            int qz = Mathf.RoundToInt(p.z * 10f);\n"
  "            SyncId = Fnv1a($\"{Id}|{qx}|{qy}|{qz}\");\n"
  "        }\n"
  "\n"
  "        /// <summary>FNV-1a 32 位（零分配、跨端稳定；与任务指纹同族做法）。</summary>\n"
  "        private static int Fnv1a(string s)\n"
  "        {\n"
  "            unchecked\n"
  "            {\n"
  "                uint h = 2166136261u;\n"
  "                for (int i = 0; i < s.Length; ++i)\n"
  "                {\n"
  "                    h ^= s[i];\n"
  "                    h *= 16777619u;\n"
  "                }\n"
  "                return (int)h;\n"
  "            }\n"
  "        }\n"
  "\n"
  "        /// <summary>【联机】按 <see cref=\"SyncId\"/> 找本地家具（找不到 = 本端没这个家具）。</summary>\n"
  "        public static Furniture_Attached FindBySyncId(int syncId)\n"
  "        {\n"
  "            return listBySyncId.TryGetValue(syncId, out Furniture_Attached f) ? f : null;\n"
  "        }\n")

# ---------- Furniture_Attached：注册表维护（带"同键只删自己"保护） ----------
E(FA,
  "        protected virtual void OnEnable()\n"
  "        {\n"
  "            list[NumberID] = this;\n"
  "        }\n"
  "\n"
  "        protected virtual void OnDisable()\n"
  "        {\n"
  "            list.Remove(NumberID);\n"
  "        }\n"
  "\n"
  "        private void OnDestroy()\n"
  "        {\n"
  "            OnOperate = null;\n"
  "            list.Remove(NumberID);\n"
  "        }\n",

  "        protected virtual void OnEnable()\n"
  "        {\n"
  "            list[NumberID] = this;\n"
  "            if (SyncId != 0) listBySyncId[SyncId] = this;\n"
  "        }\n"
  "\n"
  "        protected virtual void OnDisable()\n"
  "        {\n"
  "            list.Remove(NumberID);\n"
  "            UnregisterSyncId();\n"
  "        }\n"
  "\n"
  "        private void OnDestroy()\n"
  "        {\n"
  "            OnOperate = null;\n"
  "            list.Remove(NumberID);\n"
  "            UnregisterSyncId();\n"
  "        }\n"
  "\n"
  "        /// <summary>摘掉同步索引 —— ⚠ 只在\"当前登记的就是我\"时删，避免同键的另一份实例被误删。</summary>\n"
  "        private void UnregisterSyncId()\n"
  "        {\n"
  "            if (SyncId == 0) return;\n"
  "            if (listBySyncId.TryGetValue(SyncId, out Furniture_Attached cur) && ReferenceEquals(cur, this))\n"
  "                listBySyncId.Remove(SyncId);\n"
  "        }\n")

# ---------- Furniture_Attached：远端重放入口 ----------
E(FA,
  "            if (audioOper) PlaySound(audioOper);\n"
  "            lastOperatetime = Time.time;\n"
  "            GlobalEventBus.FurnitureOperate(user, this);\n"
  "            OnOperate?.Invoke();\n"
  "        }\n",

  "            if (audioOper) PlaySound(audioOper);\n"
  "            lastOperatetime = Time.time;\n"
  "            GlobalEventBus.FurnitureOperate(user, this);\n"
  "            OnOperate?.Invoke();\n"
  "        }\n"
  "\n"
  "        /// <summary>\n"
  "        /// 【联机】远端重放一次交互：把操作者设成**远端的那个单位**，再走同一条 <see cref=\"Operate\"/> 链。\n"
  "        ///\n"
  "        /// <para>▍为什么\"重放本地逻辑\"而不是\"只派发事件\"：共享状态的推进（<c>TaskState</c> / 谜题进度 /\n"
  "        /// 物件消失 / 欧帕兹计数）都写在各自的 <c>Operate</c> 覆写里 ⇒ 只有真跑一遍，两端状态才会自然一致\n"
  "        /// （所以 <c>OnOOPartCollect</c>/<c>OnSubmitOOPart</c>/<c>OnKeiSubmit</c> 都不需要单独同步）。</para>\n"
  "        ///\n"
  "        /// <para>▍⚠ 操作者必须传**远端单位**（该端的盟友实例；解析不到传 null），**绝不能传本机玩家**：\n"
  "        /// <c>PlayerInputHandler.OnOperation</c> / <c>PlayerWeaponsManager.OnOperation</c> 靠 <c>user == gameObject</c>\n"
  "        /// 判定\"是不是我在操作\" ⇒ 传远端单位它们自然早退，不会误动本机玩家。</para>\n"
  "        ///\n"
  "        /// <para>▍⚠ \"给操作者个人收益\"的部分在远端会因拿不到对应组件而自然跳过\n"
  "        /// （例：<c>OOPart</c> 走 <c>owner.TryGetComponent&lt;PlayerOOPartInventory&gt;</c>）；仍需逐类复核。</para>\n"
  "        /// </summary>\n"
  "        public void ApplyRemoteOperate(GameObject remoteUser)\n"
  "        {\n"
  "            owner = remoteUser;\n"
  "            Operate();\n"
  "        }\n")

# ---------- NetFriendBridge：按 sid 取盟友 GameObject ----------
E(NF,
  "        /// <summary>sid → 盟友实例。</summary>\n"
  "        private readonly Dictionary<uint, FriendController> _friends = new Dictionary<uint, FriendController>();\n",

  "        /// <summary>sid → 盟友实例。</summary>\n"
  "        private readonly Dictionary<uint, FriendController> _friends = new Dictionary<uint, FriendController>();\n"
  "\n"
  "        /// <summary>【联机】按 sid 取盟友实例的 GameObject（<c>0</c> = 房主）。找不到给 null。\n"
  "        /// <para>家具交互重放要用它把\"操作者\"指到远端的那个单位上（见 <c>NetFurnitureBridge</c>）。</para></summary>\n"
  "        public bool TryGetFriendObject(uint sid, out GameObject go)\n"
  "        {\n"
  "            go = null;\n"
  "            if (_friends.TryGetValue(sid, out FriendController fc) && fc != null)\n"
  "            {\n"
  "                go = fc.gameObject;\n"
  "                return true;\n"
  "            }\n"
  "            return false;\n"
  "        }\n")

# ---------- NetRoomFlow：事件 ----------
E(RF,
  "        /// <summary>【成员侧】房主下发任务状态 / 进度（Key = MissionBase.netOrder）⇒ 本地直接置位，不自行判定。</summary>\n"
  "        public static event Action<MissionUpdateMsg> OnMissionUpdate;\n",

  "        /// <summary>【成员侧】房主下发任务状态 / 进度（Key = MissionBase.netOrder）⇒ 本地直接置位，不自行判定。</summary>\n"
  "        public static event Action<MissionUpdateMsg> OnMissionUpdate;\n"
  "\n"
  "        /// <summary>【双向】家具交互（<c>SyncId</c> = 家具跨端稳定键）⇒ 远端重放同一交互。</summary>\n"
  "        public static event Action<FurnitureOperateMsg> OnFurnitureOperate;\n")

# ---------- NetRoomFlow：注册 ----------
E(RF,
  "            MessageCenter.Register<MissionUpdateMsg>(CmdId.MissionUpdateNtf, HandleMissionUpdateNtf);\n"
  "        }\n",

  "            MessageCenter.Register<MissionUpdateMsg>(CmdId.MissionUpdateNtf, HandleMissionUpdateNtf);\n"
  "            MessageCenter.Register<FurnitureOperateMsg>(CmdId.FurnitureOperateNtf, HandleFurnitureOperateNtf);\n"
  "        }\n")

# ---------- NetRoomFlow：退订 ----------
E(RF,
  "            MessageCenter.Unregister(CmdId.MissionUpdateNtf);\n"
  "            if (ReferenceEquals(Instance, this)) Instance = null;\n",

  "            MessageCenter.Unregister(CmdId.MissionUpdateNtf);\n"
  "            MessageCenter.Unregister(CmdId.FurnitureOperateNtf);\n"
  "            if (ReferenceEquals(Instance, this)) Instance = null;\n")

# ---------- NetRoomFlow：收发 ----------
E(RF,
  "        private void HandleMissionUpdateNtf(MissionUpdateMsg m)\n"
  "        {\n"
  "            if (m != null) OnMissionUpdate?.Invoke(m);\n"
  "        }\n",

  "        private void HandleMissionUpdateNtf(MissionUpdateMsg m)\n"
  "        {\n"
  "            if (m != null) OnMissionUpdate?.Invoke(m);\n"
  "        }\n"
  "\n"
  "        /// <summary>家具交互：成员上报房主 / 房主转发全体（由 09 侧的 NetFurnitureBridge 决定走向）。</summary>\n"
  "        public void SendFurnitureOperate(int syncId, uint sid)\n"
  "        {\n"
  "            var msg = MessageCenter.Pack(CmdId.FurnitureOperateNtf,\n"
  "                new FurnitureOperateMsg { SyncId = syncId, Sid = sid });\n"
  "\n"
  "            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);\n"
  "            else NetSvc.Instance?.SendMsg(msg);\n"
  "        }\n"
  "\n"
  "        private void HandleFurnitureOperateNtf(FurnitureOperateMsg m)\n"
  "        {\n"
  "            if (m != null) OnFurnitureOperate?.Invoke(m);\n"
  "        }\n")

# ---------- WaveManager：Install ----------
E(WM,
  "            NetMissionBridge.Install();       // 任务状态 / 进度（房主权威）\n"
  "        }\n",

  "            NetMissionBridge.Install();       // 任务状态 / 进度（房主权威）\n"
  "            NetFurnitureBridge.Install();     // 家具交互（共享世界物件，一处收口）\n"
  "        }\n")

# ---------- WaveManager：Uninstall ----------
E(WM,
  "            NetMissionBridge.Uninstall();\n"
  "        }\n",

  "            NetMissionBridge.Uninstall();\n"
  "            NetFurnitureBridge.Uninstall();\n"
  "        }\n")


def _conv(text, crlf):
    return text.replace("\n", "\r\n") if crlf else text


def main():
    problems = []
    plans = []

    for i, (path, old, new) in enumerate(EDITS):
        if not os.path.isfile(path):
            problems.append("EDIT#%d NOT FOUND: %s" % (i + 1, path))
            continue
        raw = open(path, "r", encoding="utf-8", newline="").read()
        crlf = "\r\n" in raw
        o, n = _conv(old, crlf), _conv(new, crlf)
        hit = raw.count(o)
        plans.append((path, o, n))
        if hit != 1:
            problems.append("EDIT#%d HIT=%d (expect 1) %s%s :: %s"
                            % (i + 1, hit, os.path.basename(path), " [CRLF]" if crlf else "",
                               old.strip().splitlines()[0][:60]))

    if problems:
        print("=== ABORT，未落盘 ===")
        for p in problems:
            print("  " + p)
        return 1

    for i, (path, o, n) in enumerate(plans):
        text = open(path, "r", encoding="utf-8", newline="").read()
        open(path, "w", encoding="utf-8", newline="").write(text.replace(o, n, 1))
        print("OK  EDIT#%d  %s" % (i + 1, os.path.relpath(path, ROOT)))

    print("\n全部 %d 处改写完成" % len(plans))
    return 0


if __name__ == "__main__":
    sys.exit(main())
