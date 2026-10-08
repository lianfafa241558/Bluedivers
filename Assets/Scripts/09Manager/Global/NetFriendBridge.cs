using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Game;
using FPSGame.Gameplay;
using FPSGame.Net;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 【联机 · 远程玩家（Friend）桥】把网络位姿落到"盟友角色实例"上 —— 全工程唯一同时看得见
    /// <c>FPSGame.Net</c>（位姿）与 <c>PlayerController</c>/<c>FriendController</c>（玩法层）的地方。
    ///
    /// <para>▍职责三条：</para>
    /// <list type="number">
    ///   <item><b>按名单增删</b>：订阅 <see cref="NetRoomFlow.OnPlayerList"/>，为每个"非自己"的 <c>Sid</c>
    ///         创建/销毁一个盟友实例（换场景时按缓存的名单重建）；</item>
    ///   <item><b>采集上行</b>：按固定频率把**本机玩家**的位姿交给 <see cref="NetTransformFlow.SendLocalPose"/>
    ///         （房主 = 写入聚合表；成员 = 发给房主）；</item>
    ///   <item><b>应用下行</b>：把收到的批次按 <c>Sid</c> 分发给对应 <see cref="FriendController"/>。</item>
    /// </list>
    ///
    /// <para>▍⚠ 与 <see cref="TeamNetBridge"/> 的分工：那个桥管"局内玩家表（角色/等级/战备）"，
    /// 本桥只管"位姿"。两者都常驻 <c>GameRoot</c>，互不依赖。</para>
    ///
    /// <para>▍安装点：<c>GameRoot/NetRoot</c> 下（net 常驻组件统一收在这个一级子物体里；与
    /// <c>NetRoomFlow</c>/<c>TeamNetBridge</c> 同批）。⚠ `GameRootBase` 的 Init/UnInit 只扫两级
    /// ⇒ 可以挂在一级子物体，别再往深里塞。</para>
    /// </summary>
    public class NetFriendBridge : MonoBehaviour, I_GlobaManager
    {
        [Header("盟友预制体（⚠ 必填）")]
        [InspectorName("盟友预制体")]
        [Tooltip("要求：Actor.type = Friend、无 PlayerController/相机/AudioListener，并已挂 FriendController + NetTransformView")]
        [SerializeField] private GameObject friendPrefab;

        [Header("上行")]
        [InspectorName("位姿上报频率(Hz)")]
        [SerializeField] private float sendHz = 20f;

        [Header("落点")]
        [InspectorName("出生点间隔(米)")]
        [Tooltip("第 N 个玩家 ⇒ 出生点 -(0,0,N*间隔)（原先按圆环分布，见 NextSpawnPos 的注释）")]
        [SerializeField] private float spawnSpacing = 1f;
        [InspectorName("出生点标签")]
        [SerializeField] private string spawnPointTag = "StartPoint";

        /// <summary>sid → 盟友实例。</summary>
        private readonly Dictionary<uint, FriendController> _friends = new Dictionary<uint, FriendController>();

        /// <summary>【联机】按 sid 取盟友实例的 GameObject（<c>0</c> = 房主）。找不到给 null。
        /// <para>家具交互重放要用它把"操作者"指到远端的那个单位上（见 <c>NetFurnitureBridge</c>）。</para></summary>
        public bool TryGetFriendObject(uint sid, out GameObject go)
        {
            go = null;
            if (_friends.TryGetValue(sid, out FriendController fc) && fc != null)
            {
                go = fc.gameObject;
                return true;
            }
            return false;
        }

        /// <summary>名单里已消失的 sid（遍历时不能改字典，收集完再删）。</summary>
        private readonly List<uint> _stale = new List<uint>();

        /// <summary>最近一次名单（换场景后按它重建盟友）。</summary>
        private PlayerInfo[] _lastInfos;
        private PlayerProfile[] _lastProfiles;

        private Transform _selfPlayer;
        private float _sendLeft;
        private bool _firstPoseSent;
        private bool _warnedNoPrefab;

        // ==================== 生命周期 ====================

        public void Init()
        {
            NetRoomFlow.OnPlayerList += HandleRoster;
            NetTransformFlow.OnPose += HandlePoses;
            NetRoomFlow.OnWeaponSwitch += HandleWeaponSwitch;   // 局内切枪（盟友换槽）
            NetRoomFlow.OnShoot += HandleShoot;                 // 开火表现（盟友枪口闪）
            NetRoomFlow.OnVital += HandleVital;                 // 生命状态（盟友血/盾/倒地）
            NetRoomFlow.OnAmmo += HandleAmmo;                   // 弹药系数（盟友 HUD 状态条）
            NetRoomFlow.OnSceneActorReq += HandleSceneActorReq; // 【房主】响应场景 actor 快照请求
            NetRoomFlow.OnSceneActors += HandleSceneActors;     // 【成员】应用场景 actor 快照
            NetRoomFlow.OnAirdropCall += HandleAirdropCall;     // 盟友呼叫战备（在同一个点上复现一份）
            BattleEventBus.OnAirdrop += HandleLocalAirdrop;     // 我释放了一次战备 ⇒ 上报
            NetRoomFlow.OnPlayerLeft += HandlePlayerLeft;       // 有人离开 ⇒ 销毁他的盟友实例（战斗期名单冻结，靠这条）
            NetRoomFlow.OnSpeech += HandleSpeech;               // 盟友喊话（字幕 + 语音）
            BattleEventBus.OnPlayerSpeech += HandleLocalSpeech; // 我喊话 ⇒ 上报
            UnitEventBus.OnFriendLeave += HandleFriendLeave;    // 盟友实例被销毁（字典收尾兜底）
            UnitEventBus.OnPlayerCreate += HandleLocalPlayerCreated;   // 本机玩家出生 ⇒ 绑武器管理器（直接从玩家身上取）
            GlobalEventBus.OnSceneChange += HandleSceneChanged;   // 换场景后按缓存名单重建
        }

        public void UnInit()
        {
            NetRoomFlow.OnPlayerList -= HandleRoster;
            NetTransformFlow.OnPose -= HandlePoses;
            NetRoomFlow.OnWeaponSwitch -= HandleWeaponSwitch;
            NetRoomFlow.OnShoot -= HandleShoot;
            NetRoomFlow.OnVital -= HandleVital;
            NetRoomFlow.OnAmmo -= HandleAmmo;
            NetRoomFlow.OnSceneActorReq -= HandleSceneActorReq;
            NetRoomFlow.OnSceneActors -= HandleSceneActors;
            NetRoomFlow.OnAirdropCall -= HandleAirdropCall;
            BattleEventBus.OnAirdrop -= HandleLocalAirdrop;
            NetRoomFlow.OnPlayerLeft -= HandlePlayerLeft;
            NetRoomFlow.OnSpeech -= HandleSpeech;
            BattleEventBus.OnPlayerSpeech -= HandleLocalSpeech;
            UnitEventBus.OnFriendLeave -= HandleFriendLeave;
            UnitEventBus.OnPlayerCreate -= HandleLocalPlayerCreated;
            GlobalEventBus.OnSceneChange -= HandleSceneChanged;
            BindLocalWeapons(null);
            ClearFriends();
            _lastInfos = null;
            _lastProfiles = null;
        }

        // ==================== 武器：本机上报 + 盟友应用 ====================

        private FPSGame.Gameplay.PlayerWeaponsManager _localWeapons;

        /// <summary>
        /// 【本机玩家出生】—— 绑定武器管理器的**唯一入口**：直接从玩家实例上取组件，不做全场景搜索。
        ///
        /// <para>▍为什么不用 <c>FindObjectOfType</c> + 延迟：那是"猜时机"（早了拿到的还没装配武器、晚了白等）；
        /// 玩家出生是确定时机（`UnitEventSub.OnPlayerCreate`，发布者 `Actor`），玩家身上的
        /// <c>PlayerWeaponsManager</c> 也就在那个实例上。</para>
        ///
        /// <para>顺带在此刻做两件"玩家就绪才能做"的事：报当前槽位、拉一次场景 actor 快照。</para>
        /// <para>⚠ 玩家实例每次进战斗场景都会重建 ⇒ 本回调每次都会来（`BindLocalWeapons` 自带幂等 + 重订阅）。</para>
        /// </summary>
        private void HandleLocalPlayerCreated(FPSGame.GameContract.IActor player)
        {
            var comp = player as Component;      // IActor 是接口 ⇒ 转 Component 才能判空 / 取 transform
            if (comp == null) return;

            BindLocalWeapons(comp.GetComponent<PlayerWeaponsManager>());
            ReportCurrentWeaponSlot();

            if (!_sceneActorRequested)
            {
                _sceneActorRequested = true;
                RequestSceneActors();
            }
        }

        /// <summary>订阅/退订**本机玩家**的切枪与开火（玩家实例每场景重建 ⇒ 换场景要重订阅）。</summary>
        private void BindLocalWeapons(FPSGame.Gameplay.PlayerWeaponsManager wm)
        {
            if (ReferenceEquals(_localWeapons, wm)) return;

            if (_localWeapons != null)
            {
                _localWeapons.OnSwitchedToWeapon -= OnLocalWeaponSwitched;
                _localWeapons.OnShoot -= OnLocalShoot;
            }

            _localWeapons = wm;

            if (_localWeapons != null)
            {
                _localWeapons.OnSwitchedToWeapon += OnLocalWeaponSwitched;
                _localWeapons.OnShoot += OnLocalShoot;
            }
        }

        /// <summary>本机切枪 → 上报。只报主手槽（盟友表现只显示一把枪，双持不在表现层复刻）。</summary>
        private void OnLocalWeaponSwitched(FPSGame.Weapon.WeaponPlayerController weapon, bool isSec)
        {
            if (_localWeapons == null) return;
            NetRoomFlow.Instance?.SendWeaponSwitch(_localWeapons.ActiveWeaponIndex);
        }

        /// <summary>
        /// 【上行】把**当前槽位**再报一次 —— 这不是"切枪事件"，而是**状态对账**。
        ///
        /// <para>▍为什么必须要有：槽位原先只在**切换时**上报 ⇒ 我加入房间那一刻正握着空手（槽 6）时，
        /// 对面永远收不到这条，盟友实例只能吃 <c>FriendWeaponView.Setup</c> 的兜底（默认拿主手）
        /// ⇒ "我空手，别人却看到我举着主武器；一切枪又对上了"（2026-10-07 实测）。</para>
        ///
        /// <para>▍时机：① 名单每次变化（有人加入/离开都会广播名单）⇒ 新人一到就拿到所有人的当前槽位，自愈；
        /// ② 换场景后延迟补一次（玩家实例刚建出来时武器还没装配）。</para>
        ///
        /// <para>⚠ <c>ActiveWeaponIndex &lt; 0</c>（还没拿好武器）时**不报**：那是"未知"，不是"空手"，
        /// 报了会把对面带偏（槽位 6 才是空手）。</para>
        /// </summary>
        private void ReportCurrentWeaponSlot()
        {
            if (_localWeapons == null) return;
            if (_localWeapons.ActiveWeaponIndex < 0) return;      // 未知 ≠ 空手

            NetRoomFlow.Instance?.SendWeaponSwitch(_localWeapons.ActiveWeaponIndex);
        }

        /// <summary>本机开火 → 上报（只做表现同步）。</summary>
        private void OnLocalShoot(FPSGame.Weapon.WeaponPlayerController weapon)
        {
            if (_localWeapons == null) return;

            // 射击方向：优先用**实际开火那把枪**的枪口朝向 + 本武器散布（含第三人称瞄准目标的方向）——
            // 盟友模型只同步了 yaw、没有俯仰，不带上方向就只能水平打。
            UnityEngine.Vector3 dir = UnityEngine.Vector3.zero;
            if (weapon != null)
            {
                var muzzle = weapon.GetMuzzle(0);
                if (muzzle != null) dir = weapon.GetShotDirectionWithinSpread(muzzle);
            }

            // ★ 连**目标点**一起发：第三人称下"枪口朝向"与"准心落点"不在同一条线上，
            //   只发方向时对端的表现弹会沿枪口打过去、落点对不上（2026-10-07 实测）
            NetRoomFlow.Instance?.SendShoot(_localWeapons.ActiveWeaponIndex, dir, LocalAimPoint());
        }

        /// <summary>准心射程（取目标点时的最大距离；打空时用户点取射线远端）。</summary>
        private const float AimRayLength = 300f;

        /// <summary>本机当前俯仰（相机垂直角，度；正 = 抬头）。取不到玩家/控制器 ⇒ 0（不俯仰）。</summary>
        private static float LocalAimPitch()
        {
            var self = ActorsManager.Player;
            if (self == null || self.transform == null) return 0f;

            var bsc = self.transform.GetComponent<BaseSelfController>();
            return bsc != null ? bsc.CameraPitch : 0f;
        }

        /// <summary>本机准心实指那一点（世界空间）。取不到相机（舰桥/玩家未生成）⇒ 零向量 = 未知。</summary>
        private static UnityEngine.Vector3 LocalAimPoint()
        {
            var self = ActorsManager.Player;
            if (self == null || self.transform == null) return UnityEngine.Vector3.zero;

            var pc = self.transform.GetComponent<PlayerController>();
            var cam = pc != null ? pc.PlayerCamera : null;
            if (cam == null) return UnityEngine.Vector3.zero;

            var ray = cam.ViewportPointToRay(new UnityEngine.Vector3(0.5f, 0.5f));
            UnityEngine.RaycastHit hit;
            return UnityEngine.Physics.Raycast(ray, out hit, AimRayLength) ? hit.point : ray.GetPoint(AimRayLength);
        }

        /// <summary>盟友切枪（房主权威转发回来；自己发的会被 <see cref="IsSelf"/> 丢掉）。</summary>
        private void HandleWeaponSwitch(uint sid, int slotIndex)
        {
            if (IsSelf(sid)) return;
            if (_friends.TryGetValue(sid, out var fc) && fc != null) fc.SetActiveWeaponSlot(slotIndex);
        }

        /// <summary>盟友开火（表现：枪口闪光 + 枪响 + 弹道 + 枪械动画）。</summary>
        /// <param name="dir">开火瞬间的射击方向（世界空间）；零向量 = 用枪口朝向</param>
        /// <param name="aimPoint">开枪者准心实指的目标点（世界空间）；零向量 = 未知 ⇒ 只用方向</param>
        private void HandleShoot(uint sid, int slotIndex, UnityEngine.Vector3 dir, UnityEngine.Vector3 aimPoint)
        {
            if (IsSelf(sid)) return;
            if (_friends.TryGetValue(sid, out var fc) && fc != null) fc.PlayShoot(dir, aimPoint);
        }

        // ==================== 呼叫战备（局内释放） ====================

        /// <summary>
        /// 本机**释放了一次战备** → 上报（远端各自在同一个点上复现）。
        ///
        /// <para>▍只报"我放的"：远端复现走 <c>BattleManager.ReleaseAirdrop ⇒ VFXAirdropEffect.TmpAirdrop</c>，
        /// 那条路发出的 <c>BattleEventSub.Airdrop</c> 里 <c>owner = null</c> ⇒ 天然不会回声。</para>
        /// </summary>
        private void HandleLocalAirdrop(GameObject owner, GameObject target, UnityEngine.Vector3 point, AirdropData data)
        {
            if (data == null || data.cfg == null || data.cfg.ID <= 0) return;

            var self = ActorsManager.Player;
            if (owner == null || self == null || self.gameObject == null || owner != self.gameObject) return;

            // ★ 信标朝向也要发：轰炸类战备的炮位/弹道是按信标 rotation 摆的（见 AirdropCallMsg.Yaw）
            float yaw = target != null ? target.transform.eulerAngles.y : (owner.transform != null ? owner.transform.eulerAngles.y : 0f);
            NetRoomFlow.Instance?.SendAirdropCall(data.cfg.ID, point, yaw);
        }

        /// <summary>
        /// 某成员离开（**战斗期也会来**）：销毁他的盟友实例。
        ///
        /// <para>▍为什么单独一条消息：战斗期名单是**冻结**的（<c>NetHostSvc.BroadcastPlayerList</c> 直接 return），
        /// 靠名单推不出"谁走了" ⇒ 以前战斗中被强退的人，模型会一直挂在别人屏幕上（2026-10-07 实测）。
        /// 销毁时会走 <c>UnitEventSub.OnFriendLeave</c>，HUD / 字幕那些订阅方自己会收尾。</para>
        /// </summary>
        private void HandlePlayerLeft(uint sid)
        {
            if (sid == 0u || IsSelf(sid)) return;

            FriendController fc;
            if (!_friends.TryGetValue(sid, out fc)) return;
            _friends.Remove(sid);

            if (fc != null) Tool.Destroy(fc.gameObject);
            Debug.Log($"[NetFriendBridge] 盟友 sid={sid} 已离开 ⇒ 销毁实例");
            FPSGame.Utils.NetSyncLog.SyncLog("盟友离场", $"sid={sid}（房主本地也应收到这条 —— 若房主端没打这句，就是 NetHostSvc 漏了自派发）");
        }

        /// <summary>本机喊话 → 上报（远端用**喊话者那个角色**的配置播同一条）。</summary>
        private void HandleLocalSpeech(GameObject speaker, int speech)
        {
            if (speaker == null) return;

            var self = ActorsManager.Player;
            if (self == null || self.gameObject == null || speaker != self.gameObject) return;   // 只报"我喊的"

            NetRoomFlow.Instance?.SendSpeech(speech);
        }

        /// <summary>盟友喊话：用**他那个角色**的配置取同一条，挂到盟友实例上播（字幕 + 语音）。</summary>
        private void HandleSpeech(uint sid, int speech)
        {
            if (IsSelf(sid)) return;

            FriendController fc;
            if (!_friends.TryGetValue(sid, out fc) || fc == null) return;

            string roleId = fc.RoleId;                       // ⚠ 用他自己的角色，不是我的（配置决定"哪条语音"）
            if (string.IsNullOrEmpty(roleId)) return;

            var res = ResSvc.Instance;
            if (res == null) return;

            var roleCfg = res.LoadRes<FPSGame.GameData.RoleData_SO>("GameData/Role/RD_" + roleId);
            if (roleCfg == null) return;

            var group = roleCfg.SpeechGroup((SpeechTypeEnum)speech);
            if (group == null) return;                       // 该类型没配语音 ⇒ 静默跳过

            GlobalEventBus.ActorSpeech(fc.gameObject, group.Get(fc.transform.position));
        }

        /// <summary>盟友呼叫战备（房主权威转发；自己发的那条按 sid 丢掉 —— 我本地已经真放过一次）。</summary>
        private void HandleAirdropCall(uint sid, int airdropId, UnityEngine.Vector3 point, float yaw)
        {
            if (IsSelf(sid)) return;

            var bm = BattleManager.Instance;
            if (bm == null || airdropId <= 0) return;

            // ⚠ 必须用带 angle 的重载：那个 angle 会变成信标的 rotation，而轰炸的炮位/弹道按它摆
            //   （不带 yaw 时远端那份炮击方向与发起方不同 ⇒ "时间对了位置不对"）
            bm.ReleaseAirdrop(point, yaw, airdropId);    // owner = null ⇒ 不计"呼叫战备次数"、不回声
        }

        private static bool IsSelf(uint sid) =>
            NetRoomFlow.Instance != null && sid == NetRoomFlow.Instance.SelfSid;

        // ==================== 生命状态（血/盾/倒地） ====================

        /// <summary>生命状态上报间隔（秒）：值变化立即发，这个只是"对账"兜底。</summary>
        private const float VitalRefreshInterval = 0.5f;

        private Actor _selfActor;
        private Health _selfHealth;
        private float _vitalLeft;
        private float _lastHp = -1f;
        private float _lastShield = -1f;
        private bool _lastDown;

        /// <summary>
        /// 【上行】本机生命状态。⚠ 用"轮询 + 变化判定"而不是挂事件：血量来源太多
        /// （伤害/治疗/护盾恢复/复活/强化），挂事件要动 05 内核；这里每帧两次浮点比较，零侵入。
        /// </summary>
        private void SendVitalTick(Transform self)
        {
            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            // 玩家实例每场景重建 ⇒ 缓存为空（或已销毁）时重新解析
            if (_selfHealth == null || _selfActor == null)
            {
                _selfHealth = self.GetComponent<Health>();
                _selfActor = self.GetComponent<Actor>();
                if (_selfHealth == null) return;      // 还没挂生命组件（加载中/舰桥）
            }

            float hp = _selfHealth.GetHpCurrent();
            float shield = _selfHealth.GetShieldCurrent();
            bool down = hp <= 0f;                     // 倒地 ⇔ 血量为 0（与 Health.Revive 的判据一致）

            _vitalLeft -= Time.deltaTime;
            bool changed = !Mathf.Approximately(hp, _lastHp)
                           || !Mathf.Approximately(shield, _lastShield)
                           || down != _lastDown;
            if (!changed && _vitalLeft > 0f) return;

            _vitalLeft = VitalRefreshInterval;
            _lastHp = hp;
            _lastShield = shield;
            _lastDown = down;

            flow.SendVital(hp, _selfHealth.GetHpMax(), shield, _selfHealth.GetShieldMax(), down);
        }

        /// <summary>弹药系数上报间隔（秒）：它随每发子弹变化，HUD 级别的刷新率就够（10Hz）。</summary>
        private const float AmmoRefreshInterval = 0.1f;

        private float _ammoLeft;
        private float _lastAmmoRatio = -1f;

        /// <summary>
        /// 【上行】本机弹药系数（口径 = <c>PlayerWeaponsManager.TotalRemainAmmoRatio()</c>，与 HUD 自己那根条同源）。
        /// <para>▍为什么要兜底重绑武器管理器：<c>_localWeapons</c> 只在 <c>Init</c>/换场景时绑定，
        /// 万一绑定那一刻玩家还没出生就会一直是 null ⇒ 这里用当前玩家实例补一次（<see cref="BindLocalWeapons"/>
        /// 自带幂等 + 顺带装好切枪/开火订阅）。</para>
        /// </summary>
        private void SendAmmoTick(Transform self)
        {
            if (NetRoomFlow.Instance == null) return;

            if (_localWeapons == null)
            {
                var wm = self.GetComponent<PlayerWeaponsManager>();
                if (wm == null) return;
                BindLocalWeapons(wm);
            }

            float ratio = _localWeapons.TotalRemainAmmoRatio();
            _ammoLeft -= Time.deltaTime;
            if (_ammoLeft > 0f && Mathf.Approximately(ratio, _lastAmmoRatio)) return;

            _ammoLeft = AmmoRefreshInterval;
            _lastAmmoRatio = ratio;
            NetRoomFlow.Instance.SendAmmo(ratio);
        }

        /// <summary>盟友弹药系数（写进盟友实体，HUD 状态条自己来读）。</summary>
        private void HandleAmmo(uint sid, float ratio)
        {
            if (IsSelf(sid)) return;
            if (_friends.TryGetValue(sid, out var fc) && fc != null) fc.ApplyAmmo(ratio);
        }

        /// <summary>盟友生命状态（房主权威转发回来；自己发的会被 <see cref="IsSelf"/> 丢掉）。</summary>
        private void HandleVital(PlayerVital v)
        {
            if (v == null || IsSelf(v.Sid)) return;
            if (_friends.TryGetValue(v.Sid, out var fc) && fc != null)
            {
                fc.ApplyVital(v.Hp, v.HpMax, v.Shield, v.ShieldMax, v.Down);
            }
        }

        /// <summary>
        /// 【桥调用】按角色配置给盟友装配武器槽。
        /// <para>▍数据通路：<c>PlayerProfile.Weapons</c>（按 <c>WeaponTypeEnum</c> 索引的 int[]，与本地
        /// <c>ArchivesData_SO.GetWeaponSelect</c> 同形）+ <c>RoleData_SO.GetWeapon(type, index)</c> 取预制体；
        /// 改装用 <c>PlayerProfile.Upgrades</c>（入房时随资料一起上报的那份）。</para>
        /// </summary>
        private void AttachRoleWeapons(FriendController fc, PlayerProfile profile)
        {
            if (fc == null || profile == null || string.IsNullOrEmpty(profile.RoleName)) return;

            var res = ResSvc.Instance;
            if (res == null) return;

            var roleCfg = res.LoadRes<FPSGame.GameData.RoleData_SO>("GameData/Role/RD_" + profile.RoleName);
            if (roleCfg == null)
            {
                Debug.LogWarning($"[NetFriendBridge] 找不到角色配置 GameData/Role/RD_{profile.RoleName} ⇒ 盟友没有武器");
                return;
            }

            var list = new List<FPSGame.Weapon.WeaponPlayerController>();
            var types = new[]
            {
                FPSGame.GameData.WeaponTypeEnum.Primary, FPSGame.GameData.WeaponTypeEnum.Secondary,
                FPSGame.GameData.WeaponTypeEnum.Special, FPSGame.GameData.WeaponTypeEnum.Grenade,
                FPSGame.GameData.WeaponTypeEnum.FlareGun,
            };
            for (int i = 0; i < types.Length; i++)
            {
                int t = (int)types[i];
                if (profile.Weapons == null || t >= profile.Weapons.Length) continue;
                var prefab = roleCfg.GetWeapon(types[i], profile.Weapons[t]);
                if (prefab != null) list.Add(prefab);
            }

            fc.SetWeapons(list, profile.Upgrades);
        }

        // ==================== 名单 → 实例 ====================

        private void HandleSceneChanged(string sceneName)
        {
            // ⚠ 这里**不再** FindObjectOfType 找武器管理器：玩家实例创建时会走 UnitEventSub.OnPlayerCreate
            //   （HandleLocalPlayerCreated 直接从玩家身上取组件），时机比"延迟几秒猜"确定。

            // 场景单位（NPC/特殊单位）各自随机游荡、没有连续同步 ⇒ 新场景就绪后向房主拉一次快照对齐。
            // ⚠ 不需要延迟重试：场景里摆好的单位在 **Awake 里就注册进了注册表**（`Actor.Awake` 里
            //   `ActorsManager.Actors.Add(this)`），而本事件由 `ResSvc` 在**场景加载完成之后**才发 ⇒ 那一刻它们已经在表里。
            _sceneActorRequested = true;
            RequestSceneActors();

            if (_lastInfos == null) return;
            HandleRoster(_lastInfos, _lastProfiles);
        }

        private void HandleRoster(PlayerInfo[] infos, PlayerProfile[] profiles)
        {
            _lastInfos = infos;
            _lastProfiles = profiles;
            if (infos == null || infos.Length == 0) return;

            uint self = NetRoomFlow.Instance != null ? NetRoomFlow.Instance.SelfSid : 0u;

            // ⚠ **先清退、再新建/挂模型**：新建那一步会加载角色预制体（`ResSvc.LoadRes`），
            //   若把清退放在后面，一旦这里抛异常（或提前 return），"该走的人"就会被留在场上变成幽灵盟友
            //   （2026-10-06 打包版实测：离开广播在 AttachRoleModel 处抛 UnityException，旧盟友没清掉）。
            _stale.Clear();
            foreach (var kv in _friends) _stale.Add(kv.Key);
            for (int i = 0; i < infos.Length; ++i)
            {
                if (infos[i] != null) _stale.Remove(infos[i].Sid);
            }
            for (int i = 0; i < _stale.Count; ++i) RemoveFriend(_stale[i]);

            for (int i = 0; i < infos.Length; ++i)
            {
                var info = infos[i];
                if (info == null || info.Sid == self) continue;   // 自己不需要盟友实例

                var profile = profiles != null && i < profiles.Length ? profiles[i] : null;
                EnsureFriend(info, profile, i + 1);   // 序号从 1 起（房主 = 1）⇒ 出生点按序号沿 -Z 排开
            }

            // 房间里出现了别人（或自己刚加入）⇒ 首次对齐一次场景单位位置
            //（战斗中热加入就没有"换场景"事件可依赖，走这条）
            if (!_sceneActorRequested && infos.Length > 1)
            {
                _sceneActorRequested = true;
                RequestSceneActors();
            }

            // 名单一变就对自己的"当前槽位"做一次对账：新人一到就能拿到所有人正在拿什么
            //（否则对面只能按 FriendWeaponView 的兜底显示主武器）
            ReportCurrentWeaponSlot();
        }

        private void EnsureFriend(PlayerInfo info, PlayerProfile profile, int playerOrdinal)
        {
            if (_friends.TryGetValue(info.Sid, out var exist) && exist != null)
            {
                exist.SetIdentity(info.Sid, DisplayName(info, profile));
                // 武器只在"换角色"时重建：名单同步很频繁（入房/准备/资料变更），每次重建会不停 Instantiate
                bool roleChanged = profile != null && !string.IsNullOrEmpty(profile.RoleName) && exist.RoleId != profile.RoleName;
                AttachRoleModel(exist, profile);   // 角色可能刚同步过来/被换过（内部会去重）
                if (roleChanged) AttachRoleWeapons(exist, profile);
                return;
            }

            if (friendPrefab == null)
            {
                if (!_warnedNoPrefab)
                {
                    _warnedNoPrefab = true;
                    Debug.LogError("[NetFriendBridge] 未指定盟友预制体（friendPrefab）⇒ 联机时看不到其他玩家。" +
                                   "请生成 Assets/Resources/Prefabs/BattleBase/PlayerFriend.prefab 并在此赋值。");
                }
                return;
            }

            var go = Instantiate(friendPrefab, NextSpawnPos(playerOrdinal), Quaternion.identity);
            go.name = $"Friend_{info.Sid}";

            // 预制体上应当已经挂了 FriendController；这里兜一层，避免"忘了挂"导致完全不动
            var fc = go.GetComponent<FriendController>();
            if (fc == null)
            {
                Debug.LogWarning($"[NetFriendBridge] 盟友预制体上没有 FriendController，已临时补一个（请在预制体上挂好）。", go);
                fc = go.AddComponent<FriendController>();
            }
            fc.SetIdentity(info.Sid, DisplayName(info, profile));

            // 资料已经到位（角色/武器齐）就立刻挂上，不必等下一次名单同步
            if (profile != null && !string.IsNullOrEmpty(profile.RoleName))
            {
                AttachRoleModel(fc, profile);
                AttachRoleWeapons(fc, profile);
            }

            _friends[info.Sid] = fc;
            _firstPoseSent = false;   // 新人进来 ⇒ 下一条位姿用"吸附"，避免从原点飞过去
            Debug.Log($"[NetFriendBridge] 创建盟友实例 sid={info.Sid} 名={fc.PlayerName}");
        }

        /// <summary>
        /// 给盟友挂上"远程玩家当前角色"的身体模型。
        /// <para>▍角色 id 来自 <see cref="PlayerProfile.RoleName"/>（成员自己上报、房主权威转发）
        /// ⇒ 与本地玩家 <c>BridgeRoleManager.SetPlayerRole</c> 用的是同一份数据。</para>
        /// </summary>
        private void AttachRoleModel(FriendController fc, PlayerProfile profile)
        {
            if (fc == null || profile == null || string.IsNullOrEmpty(profile.RoleName)) return;

            // 已经是这个角色 ⇒ 不必再"加载预制体 + 实例化"（AttachModel 内部也会把多出来的实例丢掉）。
            // ⚠ 名单同步很频繁（每次入房/离开/准备切换/资料变更），无脑重挂会白白产生一堆 Instantiate/Destroy。
            if (fc.RoleId == profile.RoleName) return;

            var res = ResSvc.Instance;
            if (res == null) return;

            var prefab = res.LoadRes<Transform>("Prefabs/StudentModle/" + profile.RoleName);
            if (prefab == null)
            {
                Debug.LogWarning($"[NetFriendBridge] 找不到角色模型 Prefabs/StudentModle/{profile.RoleName}");
                return;
            }
            fc.AttachModel(Instantiate(prefab), profile.RoleName);
        }

        /// <summary>
        /// 盟友实例被销毁的兜底收尾（可能来自 <see cref="RemoveFriend"/>、<see cref="ClearFriends"/>
        /// 或换场景时的整体销毁）——把字典里指向它的条目也摘掉，避免留下"已销毁引用"。
        /// <para>▍为什么还要这个：正常路径是"先摘字典再销毁"，但**换场景**是 Unity 直接销毁整棵层级、
        /// 不会走我们的方法 ⇒ 只靠那一处会漏。</para>
        /// </summary>
        private void HandleFriendLeave(Actor actor)
        {
            if (actor == null) return;

            uint found = 0;
            bool has = false;
            foreach (var kv in _friends)
            {
                if (kv.Value == null) continue;
                if (kv.Value.GetComponent<Actor>() == actor) { found = kv.Key; has = true; break; }
            }
            if (has) _friends.Remove(found);   // 不能在遍历中改字典 ⇒ 先记 key 再删
        }

        private void RemoveFriend(uint sid)
        {
            if (!_friends.TryGetValue(sid, out var fc)) return;
            _friends.Remove(sid);
            if (fc != null)
            {
                // 摘注册表 + 销毁是一对、顺序固定 ⇒ 走 ActorsManager.Despawn（别在这里自己拼两行）
                ActorsManager.Despawn(fc.GetComponent<Actor>());
            }
            Debug.Log($"[NetFriendBridge] 移除盟友实例 sid={sid}");
        }

        private void ClearFriends()
        {
            foreach (var kv in _friends)
            {
                if (kv.Value == null) continue;
                ActorsManager.Despawn(kv.Value.GetComponent<Actor>());   // 同 RemoveFriend（换场景/关房一次性清空）
            }
            _friends.Clear();
            _stale.Clear();
        }

        // ==================== 场景 actor 快照（NPC / 特殊单位） ====================

        /// <summary>本次场景/房间是否已经拉过快照（避免每次名单同步都拉）。</summary>
        private bool _sceneActorRequested;

        /// <summary>
        /// 【成员】向房主拉一次场景 actor（NPC/特殊单位）快照并应用到本地同名单位。
        ///
        /// <para>▍为什么需要：每个客户端各自模拟世界，**场景单位只在各端本地走动**（<c>NPCWalk</c> 随机游荡）
        /// ⇒ 后加入的人会看到 NPC 停在出生点，和房主那边完全对不上；而玩家位姿有 20Hz 连续同步，只有这些没有。</para>
        ///
        /// <para>▍为什么只拉一次：它们是氛围物，不需要毫秒级一致；拉一次就把"完全对不上"变成"差一点点"。</para>
        /// </summary>
        private void RequestSceneActors()
        {
            if (NetRoomFlow.Instance == null) return;
            NetRoomFlow.Instance.SendSceneActorReq();          // 房主内部会早退（它自己就是数据源）
        }

        /// <summary>
        /// 收集"场景单位"（NPC / 特殊单位这类由场景生成、**只在各端本地走动**的非玩家单位）。
        /// <para>▍⚠ 不能只用 <c>ActorsManager.SpecUnits</c>：带 <c>ActorFlag.Unimportant</c> 的特殊单位
        /// **不会**注册进去（见 <c>Actor.Awake</c> 的注册分支），而场景 NPC 常带这个标记 ⇒ 会整批漏掉。</para>
        /// <para>⇒ 改成遍历 <c>ActorsManager.Actors</c> 再排除玩家 / 盟友 / 敌人。</para>
        /// </summary>
        private static void CollectSceneActors(List<FPSGame.GameContract.IActor> buffer)
        {
            buffer.Clear();

            var all = ActorsManager.Actors;
            for (int i = 0; i < all.Count; ++i)
            {
                var a = all[i];
                if (!a.IsValidMono()) continue;
                if (ReferenceEquals(a, ActorsManager.Player)) continue;                     // 本机玩家
                if (a.transform.GetComponent<FriendController>() != null) continue;         // 盟友（有连续位姿同步）
                if (IsEnemy(a)) continue;                                                    // 敌人（各自模拟，不能对着同步）

                buffer.Add(a);
            }

            // 兜底：个别单位可能没进 Actors
            var spec = ActorsManager.SpecUnits;
            for (int i = 0; i < spec.Count; ++i)
            {
                var a = spec[i];
                if (!a.IsValidMono()) continue;

                bool dup = false;
                for (int k = 0; k < buffer.Count; ++k)
                    if (ReferenceEquals(buffer[k], a)) { dup = true; break; }
                if (!dup) buffer.Add(a);
            }
        }

        private static bool IsEnemy(FPSGame.GameContract.IActor actor)
        {
            var enemies = ActorsManager.Enemys;
            for (int i = 0; i < enemies.Count; ++i)
                if (ReferenceEquals(enemies[i], actor)) return true;
            return false;
        }

        /// <summary>【房主】把场上非玩家单位的位置/朝向打包回给请求者。</summary>
        private void HandleSceneActorReq(uint sid)
        {
            if (sid == 0) return;

            var list = new List<FPSGame.GameContract.IActor>();
            CollectSceneActors(list);

            var buffer = new List<SceneActorEntry>(list.Count);
            for (int i = 0; i < list.Count; ++i)
            {
                var a = list[i];
                var t = a.transform;
                buffer.Add(new SceneActorEntry
                {
                    Id = a.Id,                        // 匹配主键（跨端稳定）
                    IndexID = a.IndexID,              // 仅参考（各端自增，不保证一致）
                    X = t.position.x,
                    Y = t.position.y,
                    Z = t.position.z,
                    Yaw = t.eulerAngles.y,
                });
            }

            NetRoomFlow.Instance?.SendSceneActors(sid, buffer.ToArray());
            Debug.Log($"[NetFriendBridge] 场景 actor 快照 → sid={sid}，共 {buffer.Count} 个");
        }

        /// <summary>
        /// 【成员】应用快照：按 <c>Id</c> 匹配本地同名单位（同 Id 有多个时取**离报告位置最近**的那个，用过的跳过）。
        /// <para>⚠ 不用 IndexID/NumberID 当主键：那是各端自增，不保证跨端一致（见 <c>KeyScreenControl</c> 的注释）。</para>
        /// <para>⚠ 有 NavMeshAgent 的单位要 <c>Warp</c>（直接改 transform 会被 agent 覆盖回原处）。</para>
        /// </summary>
        private void HandleSceneActors(SceneActorEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                Debug.Log("[NetFriendBridge] 场景 actor 快照为空（房主那边还没有场景单位，或双方场景不同）");
                return;
            }

            var list = new List<FPSGame.GameContract.IActor>();
            CollectSceneActors(list);
            var used = new List<int>();
            int applied = 0;

            for (int i = 0; i < entries.Length; ++i)
            {
                var e = entries[i];
                if (e == null) continue;

                var want = new UnityEngine.Vector3(e.X, e.Y, e.Z);
                int best = -1;
                float bestSqr = float.MaxValue;

                for (int k = 0; k < list.Count; ++k)
                {
                    if (used.Contains(k)) continue;
                    var a = list[k];
                    if (!a.IsValidMono()) continue;
                    if (!string.Equals(a.Id, e.Id, System.StringComparison.Ordinal)) continue;

                    float sqr = (a.transform.position - want).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; best = k; }
                }
                if (best < 0) continue;

                used.Add(best);
                var t = list[best].transform;
                var agent = t.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(want);
                else t.position = want;
                t.rotation = UnityEngine.Quaternion.Euler(0f, e.Yaw, 0f);
                ++applied;
            }

            Debug.Log($"[NetFriendBridge] 应用场景 actor 快照：{applied}/{entries.Length}（本地场景单位 {list.Count} 个）");
        }

        private static string DisplayName(PlayerInfo info, PlayerProfile profile)
        {
            if (profile != null && !string.IsNullOrEmpty(profile.Name)) return profile.Name;
            return info != null ? info.PlayerName : null;
        }

        /// <summary>
        /// 出生点：优先用标签点，退化到"本机玩家位置"（战斗场景未必有 StartPoint）。
        ///
        /// <para>▍**按第几个玩家沿 -Z 排开**：第 N 个 ⇒ <c>出生点 -(0,0,N*spawnSpacing)</c>。
        /// 原先按圆环（半径 spawnRadius）分布，但**首次位姿会把盟友吸附到他的真实位置**
        /// ⇒ 主机站在出生点没动时两人必然重合、被物理互相顶开（盟友挂的角色模型自带 CapsuleCollider）
        /// ⇒ 出生点错开至少让出生瞬间不打架（长期重合由 <c>FriendController</c> 的"忽略与本地玩家碰撞"兜住）。</para>
        /// </summary>
        /// <param name="playerOrdinal">该玩家在房间名单里的序号（房主 = 1）</param>
        private Vector3 NextSpawnPos(int playerOrdinal)
        {
            Vector3 center;
            var tagGo = string.IsNullOrEmpty(spawnPointTag) ? null : GameObject.FindGameObjectWithTag(spawnPointTag);
            if (tagGo != null) center = tagGo.transform.position;
            else
            {
                var self = ResolveSelfPlayer();
                center = self != null ? self.position : Vector3.zero;
            }

            return center + new Vector3(0f, 0f, -Mathf.Max(0, playerOrdinal) * spawnSpacing);
        }

        // ==================== 位姿：上行 ====================

        private Transform ResolveSelfPlayer()
        {
            // 本地玩家是场上唯一的 PlayerController（远程玩家用 Friend 预制体，不含 PlayerController）
            if (_selfPlayer != null) return _selfPlayer;

            // 直接从单位注册表拿（玩家出生时 ActorsManager.RegisterPlayer 写入）⇒ 不做全场景搜索
            var player = ActorsManager.Player as Component;
            _selfPlayer = player != null ? player.transform : null;
            return _selfPlayer;
        }

        private void Update()
        {
            var flow = NetTransformFlow.Instance;
            if (flow == null) return;

            // 只在"房间里"上报：房主有房间，成员已连接
            bool inRoom = flow.IsHost || (NetSvc.Instance != null && NetSvc.Instance.IsConnected);
            if (!inRoom) return;

            var self = ResolveSelfPlayer();
            if (self == null) return;

            // 生命状态 / 弹药系数：各自的计时/变化判定，与位姿节流互不影响（所以放在 _sendLeft 早退之前）
            SendVitalTick(self);
            SendAmmoTick(self);

            _sendLeft -= Time.deltaTime;
            if (_sendLeft > 0f) return;
            _sendLeft = 1f / Mathf.Max(1f, sendHz);

            uint selfSid = NetRoomFlow.Instance != null ? NetRoomFlow.Instance.SelfSid : 0u;
            var snap = new PoseSnapshot
            {
                Sid = selfSid,
                X = self.position.x,
                Y = self.position.y,
                Z = self.position.z,
                Yaw = self.eulerAngles.y,
                Pitch = LocalAimPitch(),     // 抬头/低头也要同步（否则对端看你永远平举枪，见 PoseSnapshot.Pitch）
                Teleport = !_firstPoseSent,   // 首次一律吸附，避免别人看到我从原点滑过去
            };
            _firstPoseSent = true;

            flow.SendLocalPose(snap, NetRoomFlow.CurrentMatchId);
        }

        // ==================== 位姿：下行 ====================

        private void HandlePoses(PoseBatchMsg batch)
        {
            if (batch == null || batch.Items == null) return;   // 空批次 / 旧版本端没带 Items ⇒ 直接跳过

            uint self = NetRoomFlow.Instance != null ? NetRoomFlow.Instance.SelfSid : 0u;

            for (int i = 0; i < batch.Items.Length; ++i)
            {
                var it = batch.Items[i];
                if (it == null || it.Sid == self) continue;

                if (_friends.TryGetValue(it.Sid, out var fc) && fc != null)
                {
                    fc.ApplyPose(new Vector3(it.X, it.Y, it.Z), it.Yaw, it.Pitch, it.Teleport);
                }
            }
        }
    }
}
