using System.Collections;
using System.Collections.Generic;
using TMPro;
using FPSGame.Game;
using FPSGame.Net;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Rendering;
using FPSGame.Gameplay;
using FPSGame.GameData;

namespace FPSGame.Managers
{

/// <summary>
/// 舰桥（准备阶段）的角色展示、选择与换人。
/// </summary>
[AddComponentMenu("管理/舰桥角色管理")]
public class BridgeRoleManager : RoleManagerBase
{
    [SerializeField]
    private GameObject ReadyPoint;

    [Header("展示模型列表")]
    public List<GameObject> showModleList;


    private int m_nowShowIndex;
    private GameObject selectPointGo;


    protected override void Start()
    {
        base.Start();
        SnowController.SetEnabled(false);

        var modleList = resManager.LoadObjects<GameObject>("Prefabs/StudentModle");
        //Debug.LogWarning("获取到的学生模型长度"+modleList.Count);
        selectPointGo = TransformUtils.SceenFind("ShowStudentPoint");
        if (!selectPointGo)
        {
            Debug.LogError("场景中找不到ShowStudentPoint");
            return;
        }
        dataList.ForEach(item => {

            var show = Instantiate(modleList.Find(modle => modle.name == item.ID));
            showModleList.Add(show);
            if (selectPointGo != null)
            {
                show.transform.SetParent(selectPointGo.transform, false);
                show.transform.localPosition = default;
                show.transform.localRotation = Quaternion.identity;
            }
            show.SetActive(false);
        });

        m_nowShowIndex = m_nowSelectIndex;

        SetPlayerRole(m_player);
#if UNITY_EDITOR
        //仅供试验
        ArchivesData_SO.Current.GainRoleExp(dataList[m_nowShowIndex].ID, Random.Range(5000, 99999), out int level, out float expScale);
#endif

        GameRoot.GameState = FPSGame.Core.GameStateEnum.Bridge;
        WndManager.WindowState = FPSGame.Core.WindowStateEnum.Game;

        // ⚠ 三档口径（2026-10-10 用户口径；实现分别在 HandlePlayerList 与 Update 的 O 键里）：
        //   ① 房主开房：**不传送**；
        //   ② 新人进房：房主还在 Ready ⇒ 去**开工点**；房主已在 Armament ⇒ 去**集合点**（并跟到 Armament 阶段）；
        //   ③ 集合点只有"**Ready 下手动按 O**"这一条手动入口（Armament 下按 O 不传）。
        //   "进 Armament 自动把所有人挪到准备点"那条链已删（连 GlobalEventBus 的订阅一起）。

        // ★ 加入别人的房间 ⇒ 把**本机玩家**挪到开工点附近的一个空位（只动新人，见 HandlePlayerList）。
        //   原来入房只改名单/配置、不动位置 ⇒ 玩家还站在入房前自己走到的位置（2026-10-10 用户报）。
        NetRoomFlow.OnJoinResult += HandleJoinResult;
        NetRoomFlow.OnPlayerList += HandlePlayerList;
    }

    private void OnDestroy()
    {
        NetRoomFlow.OnJoinResult -= HandleJoinResult;
        NetRoomFlow.OnPlayerList -= HandlePlayerList;
    }

    /// <summary>入房成功 ⇒ 等**名单到位**再挪：要先看清别人站在哪，才能让出空位（见 <see cref="HandlePlayerList"/>）。</summary>
    private void HandleJoinResult(bool ok, string reason)
    {
        if (!ok) return;
        if (NetRoomFlow.Instance != null && NetRoomFlow.Instance.IsHost) return;   // 房主本来就在自己那一位
        _pendingStartSnap = true;
    }

    /// <summary>
    /// 名单同步 ⇒ **只给"刚入房的新人"找一个不与别人重叠的落脚点**（2026-10-10 用户口径）。
    ///
    /// <para>▍为什么不再"按队伍序号对称摊开"：那个口径要求**人数一变整排重排**，与"只挪新人、
    /// 不拽已经站好的人"直接冲突 —— 只挪新人的话，房主会停在"他一个人时"算出的位置（偏移 0），
    /// 而新人按 N=2 站到 <c>+0.25</c>，两人只差 0.25m，看着就是"完全重叠在开工点"。</para>
    ///
    /// <para>▍现在：<c>_pendingStartSnap</c> 只由**新人自己**的 <c>OnJoinResult</c> 置位 ⇒ 本方法只会动新人；
    /// 新人的落点由 <see cref="FindFreeSpotNear"/> 读在场玩家实际位置让出来。房主/先到的人**一律不动**。</para>
    /// </summary>
    private void HandlePlayerList(PlayerInfo[] infos, PlayerProfile[] profiles)
    {
        if (!_pendingStartSnap) return;                                       // ★ 只挪新人：其余人一律不动

        var flow = NetRoomFlow.Instance;
        if (flow == null || flow.SelfHostIndex < 0) return;                   // 名单里还没有我 ⇒ 等下一次
        _pendingStartSnap = false;

        // ★ 落脚点按**房主那边的阶段**分两种（2026-10-10 用户口径）：
        //   房主还在 Ready（选完任务、等人）⇒ 新人到**开工点**找空位；
        //   房主已在 Armament（开始配战备）⇒ 新人直接到**集合点**找空位
        //   （同时本机也跟着进 Armament —— 那步由 TeamNetBridge.HandleTaskConfirm 按补发的 Phase 做）。
        //
        //   ▍为什么这里直接读本机 GameState 就够（顺序保证，别想当然）：
        //     房主收人时的发送顺序 = JoinRoomRsp → 补发 TaskConfirmNtf → BroadcastPlayerList。
        //     · JoinRoomRsp 在本端 FinishJoin 里**先**抛 OnPlayerList（那时 _pendingStartSnap 还没置位 ⇒ 本方法
        //       直接 return，不会用"还没跟阶段"的状态去挪人），**再**抛 OnJoinResult（置位）；
        //     · 补发 TaskConfirmNtf ⇒ 本机 GameState 跟到 Armament；
        //     · 最后 BroadcastPlayerList 才触发本方法真正落位 ⇒ 这时读到的已经是**跟过阶段**的新值。
        bool armament = GameRoot.GameState == FPSGame.Core.GameStateEnum.Armament;
        Vector3 origin = armament && ReadyPoint != null
            ? RawSpot(ReadyPoint.transform.position)
            : RawStartPoint();

        MoveSelfTo(FindFreeSpotNear(origin), armament ? "集合点(让位)" : "开工点(让位)");
    }

    /// <summary>入房后"还没挪到落脚点"（等名单到位，见 <see cref="HandlePlayerList"/>）。</summary>
    private bool _pendingStartSnap;

    [Header("入房落脚点")]
    [InspectorName("最小玩家间距(米)")]
    [Tooltip("入房时新人从落脚点（开工点/集合点）沿 +X 让开，直到与所有在场玩家的水平距离都不小于这个值")]
    [SerializeField] private float minPlayerGap = 0.5f;

    [InspectorName("最多让位次数")]
    [SerializeField] private int maxSpotSteps = 8;

    /// <summary><see cref="maxSpotSteps"/> 读到 0（老场景没序列化这个字段）时的兜底值。</summary>
    private const int DefaultSpotSteps = 8;

    /// <summary>在场其它玩家当前站的位置（每次让位前刷一遍，见 <see cref="CollectOtherPlayerSpots"/>）。</summary>
    private readonly List<Vector3> _otherSpots = new List<Vector3>();

    /// <summary>开工点 / 集合点的**原始点**（不做任何序号偏移 —— 让位由 <see cref="FindFreeSpotNear"/> 干）。</summary>
    private Vector3 RawSpot(Vector3 p) => p + Vector3.up * 0.2f;

    private Vector3 RawStartPoint()
    {
        var go = GameObject.FindGameObjectWithTag("StartPoint");
        return RawSpot(go != null ? go.transform.position : Vector3.zero);
    }

    /// <summary>
    /// 在 <paramref name="origin"/> 附近找一个**不与任何在场玩家重叠**的落脚点：从原点起沿 +X 以
    /// <see cref="RoleManagerBase.spawnSpacing"/> 步进，取第一个"与所有在场玩家的水平距离都 ≥
    /// <see cref="minPlayerGap"/>"的点；都占着就退到最外侧那一位。
    ///
    /// <para>▍为什么读别人的位置、而不是按"队伍序号"算（2026-10-10 用户口径）：序号那套是
    /// **以队伍为中心对称**的 ⇒ 人数一变整排都要平移，与"只挪新人、不动已经站好的人"直接冲突。
    /// 读实际位置则天然兼容"别人站哪就是哪"。</para>
    ///
    /// <para>▍拿到的位置是**盟友实例**（远端玩家在位姿同步里的那一份，见 <c>NetFriendBridge</c>）：
    /// 名单一到就建出来了，位置最多滞后一个位姿周期（20Hz）—— 比"按序号猜"准得多。</para>
    /// </summary>
    private Vector3 FindFreeSpotNear(Vector3 origin)
    {
        float step = Mathf.Max(0.1f, spawnSpacing);
        // ⚠ 兜底别省：老场景/预制体里这两个字段可能还没序列化（工程里踩过"改默认值不生效"的坑，
        //   见 NetFriendBridge.spawnSpacing）⇒ 读到 0 时至少保证"一个步长的间距"与足够的让位次数。
        float gap = Mathf.Max(step, minPlayerGap);
        int steps = maxSpotSteps > 0 ? maxSpotSteps : DefaultSpotSteps;

        CollectOtherPlayerSpots();

        for (int i = 0; i <= steps; ++i)
        {
            Vector3 candidate = origin + new Vector3(i * step, 0f, 0f);
            if (IsFreeSpot(candidate, gap)) return candidate;
        }
        return origin + new Vector3((steps + 1) * step, 0f, 0f);   // 全占着（8 个人以上）⇒ 退到最外侧
    }

    /// <summary>候选点跟别人是否够远（**只比水平距离**：让位是沿 X 让的，Y 沿用地形高度）。</summary>
    private bool IsFreeSpot(Vector3 candidate, float gap)
    {
        float sqr = gap * gap;
        for (int i = 0; i < _otherSpots.Count; ++i)
        {
            Vector3 d = _otherSpots[i] - candidate;
            d.y = 0f;
            if (d.sqrMagnitude < sqr) return false;
        }
        return true;
    }

    /// <summary>
    /// 收集"其它玩家"当前站的位置（本机自己那台不算 —— 我们要挪的正是自己）。
    /// ▍远端玩家在本端就是一个个**盟友实例**（每个都挂 <c>FriendController</c>，见 <c>NetFriendBridge</c>）。
    /// </summary>
    private void CollectOtherPlayerSpots()
    {
        _otherSpots.Clear();

        var friends = UnityEngine.Object.FindObjectsByType<FriendController>(FindObjectsSortMode.None);
        for (int i = 0; i < friends.Length; ++i)
        {
            if (friends[i] != null) _otherSpots.Add(friends[i].transform.position);
        }
    }

    /// <summary>
    /// 把**本机玩家**挪到 <see cref="ReadyPoint"/>（集合点）。
    ///
    /// <para>▍**唯一入口 = Ready 状态下手动按 <c>O</c>**（2026-10-10 用户口径：集合点只有用 O 传送；
    /// **Armament 状态下按 O 不传**）。</para>
    /// <para>▍曾经挂在"进 Armament"上自动挪 —— 那等于"一进战备就被传送"（房主开房尤其明显），已删（连订阅一起）。</para>
    /// <para>▍入房新人走的是另一条：<see cref="HandlePlayerList"/> 里按房主阶段决定去开工点还是集合点，
    /// 并且是"读别人实际位置找空位"（<see cref="FindFreeSpotNear"/>），不走本方法的序号口径。</para>
    ///
    /// <para>▍摊开口径与 <see cref="GetStartPoint"/> 一致（<see cref="RoleManagerBase.Spread"/>，
    /// 沿 +X 按队伍序号对称铺开）：多人各自按 O 时不会叠在同一点。</para>
    /// </summary>
    public void MoveSelfToReadyPoint()
    {
        if (ReadyPoint == null) return;
        MoveSelfTo(Spread(ReadyPoint.transform.position), "准备点");
    }

    /// <summary>
    /// 把**本机玩家**挪到指定点。
    /// <para>⚠ 顺序不能反：先关 <c>CharacterController</c> 再写 <c>position</c>，开着它直接改坐标会被它拽回去。</para>
    /// <para>⚠ 只有一台机器上是"自己"，其余玩家靠 20Hz 位姿同步跟过来 ⇒ 不需要额外同步位置。</para>
    /// </summary>
    private void MoveSelfTo(Vector3 target, string what)
    {
        if (m_player == null || m_player.Controller == null) return;

        m_player.Controller.enabled = false;
        m_player.transform.position = target;
        m_player.Controller.enabled = true;

        NetSyncLog.SyncLog("挪本机玩家", $"{what} {target:F2}（序号={SelfOrdinal()}/{SpawnSlots.TeamCount}；" +
            $"让位前看到其它玩家 {_otherSpots.Count} 个；各端只挪自己，其余靠位姿同步）");
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.O))
        {
            if(GameRoot.GameState== FPSGame.Core.GameStateEnum.Bridge)
            {
                WndManager.Instance.SetWndState(FPSGame.GameContract.WndType.SelectMap, true);
            }
            else if (GameRoot.GameState == FPSGame.Core.GameStateEnum.Ready)
            {
                // ★ 集合点**唯一**的入口：Ready 下手动按 O（Armament 下按 O **不传送**，见 MoveSelfToReadyPoint）
                MoveSelfToReadyPoint();
            }

        }
    }

    /// <summary>
    /// 大厅**初始出生点**（载入场景 / 打完一局回大厅时用；<c>RoleManagerBase.Start</c> 里那次 <c>Instantiate</c>）。
    ///
    /// <para>▍这里仍按**队伍序号**沿 +X 对称铺开：这是一次"全员同时落地"的场合（各端同一帧各自刷自己的人），
    /// 序号口径两端一致、不会打架。</para>
    /// <para>▍⚠ 与"入房让位"分开：入房那条**只挪新人**、靠读别人实际位置让位
    /// （<see cref="HandlePlayerList"/> → <see cref="FindFreeSpotNear"/>）—— 那里若还用序号口径，
    /// 就会出现"房主停在旧位置、新人按新人数站"只差 0.25m 的伪重叠（2026-10-10 用户实测）。</para>
    /// <para>▍序号取 <c>RoleManagerBase.SelfOrdinal()</c>（房主视角下标）而**不是** <c>TeamManager.SelfIndex</c>：
    /// 后者在本地名单里恒为 0 ⇒ 那条"按序号让开"实际一直没生效（2026-10-10 发现并改掉）。</para>
    /// </summary>
    public override Vector3 GetStartPoint()
    {
        // ⚠ 盟友那侧 <c>NetFriendBridge.NextSpawnPos</c> 的初始偏移只是"出生瞬间不打架"，
        //   它的真实位置由位姿同步覆盖 —— 所以**偏移必须发生在本机自己身上**，否则同步完还是重叠。
        return Spread(RawStartPoint());
    }

    public override void SetPlayerRole(PlayerController player)
    {
        player.SetBody(Instantiate(resManager.LoadRes<Transform>("Prefabs/StudentModle/" + dataList[m_nowSelectIndex].ID)), dataList[m_nowSelectIndex], new() { EmptyWeapon });
        player.WeaponsManager.SwitchWeapon(false);//切到空武??
        base.SetPlayerRole(player);
    }


    #region 换人界面相关
    public void StartShowRole(out GameObject go, out RoleData_SO data, out ArchivesData_SO.ArchRoleData arch, out bool isNow)
    {
        SetShow(out go, out data, out arch, out isNow);
    }
    public void SwitchShowRole(bool add, out GameObject go, out RoleData_SO data, out ArchivesData_SO.ArchRoleData arch, out bool isNow)
    {
        showModleList[m_nowShowIndex].gameObject.SetActive(false);
        m_nowShowIndex = Tool.PositiveRemainder(m_nowShowIndex + (add ? 1 : -1), dataList.Count);

        SetShow(out go, out data, out arch, out isNow);
    }
    public void RandomShowRole(out GameObject go, out RoleData_SO data, out ArchivesData_SO.ArchRoleData arch, out bool isNow)
    {
        showModleList[m_nowShowIndex].gameObject.SetActive(false);
        var old = m_nowShowIndex;
        while (old == m_nowShowIndex)
        {
            m_nowShowIndex = Random.Range(0, dataList.Count);
        }


        SetShow(out go, out data, out arch, out isNow);
    }

    private void SetShow(out GameObject go, out RoleData_SO data, out ArchivesData_SO.ArchRoleData arch, out bool isNow)
    {
        go = showModleList[m_nowShowIndex];
        data = dataList[m_nowShowIndex];
        isNow = m_nowSelectIndex == m_nowShowIndex;
        arch = ArchivesData_SO.Current.GetRoleCfg(data.ID);
        showModleList[m_nowShowIndex].gameObject.SetActive(true);
        selectPointGo.transform.GetChild(0).gameObject.SetActive(false);
        selectPointGo.transform.GetChild(0).gameObject.SetActive(true);
    }

    public void SelectRole()
    {
        m_nowSelectIndex = m_nowShowIndex;
        var newRoleId = dataList[m_nowShowIndex].ID;
        ArchivesData_SO.Current.lastSelectRole = newRoleId;
        selectPointGo.transform.GetChild(0).gameObject.SetActive(false);
        selectPointGo.transform.GetChild(0).gameObject.SetActive(true);

        SetPlayerRole(m_player);

        // 同步更新 teamManager.players 中的角色数据，确保其他窗口（GameEndWnd、ArmamentWnd 等）能获取到正确的角色
        ArchivesData_SO.Current.GetRoleLevel(newRoleId, out int level, out float exp);
        var selfData = TeamManager.Instance.players[TeamManager.Instance.SelfIndex];
        selfData.roleName = newRoleId;
        selfData.roleLevel = level;
        selfData.roleExp = exp;
        selfData.weapons = ArchivesData_SO.Current.GetWeaponSelect(newRoleId);
        selfData.Upgrades = ArchivesData_SO.Current.GetWeaponUpgrade(newRoleId);
        selfData.weaponModules = ArchivesData_SO.Current.GetWeaponModules(newRoleId);

        // 联机：把"我换了角色"上报（房主写进 HostProfile 并广播；单机时是空操作）
        TeamNetBridge.SendSelfProfile();
        // 角色变了 ⇒ 武器改装（档位 + 模组）整体也跟着变，配置那条要重发
        TeamNetBridge.SendSelfLoadout();
    }
    #endregion
}
}
