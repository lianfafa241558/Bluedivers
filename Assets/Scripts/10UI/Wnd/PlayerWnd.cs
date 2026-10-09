using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.GameContract;

using FPSGame.Game;
using FPSGame.Gameplay;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.GameData;
using FPSGame.Weapon;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;

/// <summary>
/// 玩家主 HUD（罗盘、武器、任务、击杀等）。
/// </summary>
[AddComponentMenu("UI/窗口/玩家 HUD")]
public partial class PlayerWnd : Window
{
    public RectTransform CompasRect;
    public int VisibilityAngle = 180;

    public GameObject prefab, prefabBig;

    List<CanvasGroup> dirList;

    #region 血量
    [Foldout("血量", true)]
    [SerializeField]
    private Transform playerRoot,playerName,portrait, frame;
    [SerializeField]
    DynamicBar healthBar, shieldBar, ammoBar;
    #endregion

    #region 武器R
    [Foldout("武器R", true)]
    [SerializeField]
    private Transform weaponNameR,weaponTypeR,nowAmmoR,remainAmmoR,grenadeCountR, grenadeKeyR;

    [SerializeField]
    CanvasGroup weaponList;
    #endregion
    #region 武器L
    
    [Foldout("武器L", true)]
    [SerializeField]
    private Transform weaponNameL, weaponTypeL, nowAmmoL, remainAmmoL;

    #endregion

    #region 手雷
    [Foldout("手雷", true)]
    [SerializeField]
    private Transform GrenadeCount;

    #endregion

    #region 任务
    [Foldout("任务", true)]
    [SerializeField]
    private Transform timeShow,enemyShow,diffshow;
    private float m_TaskStartTime;
    #endregion

    #region 击杀
    [Foldout("击杀", true)]
    [SerializeField]
    private CanvasGroup killRoot;
    [SerializeField]
    private Transform killIcon, killCount;
    float m_LastKillTime;
    int m_KillCount;
    #endregion

    #region 调试

    [Foldout("调试", true)]
    [SerializeField]
    private Transform framerateCounter;

    #endregion

    [Foldout("其他", true)]
    bool initPlayer;
    [SerializeField]
    PlayerController m_Controller;
    IHealth m_Health;
    [SerializeField]
    PlayerWeaponsManager m_WeaponsManager;
    [SerializeField]
    WeaponPlayerController m_ActiveWeapon, m_ActiveSecWeapon;
    float m_LastChangeTime;
    float m_WidthMultiplier;

    private void TryPlayer()
    {
        initPlayer = true;
        //Debug.LogError("设置玩家");
        m_Controller = ActorsManager.Player.transform.GetComponent<PlayerController>();
        m_WeaponsManager = m_Controller.WeaponsManager;
        m_Health = m_Controller.Health;
        m_ActiveWeapon = m_WeaponsManager.GetActiveWeapon() as WeaponPlayerController;
        weaponList.alpha = 0;

        killRoot.alpha = 0;
        m_LastKillTime = -15;
        m_KillCount = 0;

        m_TaskStartTime = Time.time+5;

        if (m_ActiveWeapon)
        {
            AddWeapon(m_ActiveWeapon, m_WeaponsManager.ActiveWeaponIndex);
            ChangeWeapon(m_ActiveWeapon);
        }

        SetText(playerName, m_Controller.PlayerName);
        SetSprite(portrait, m_Controller.Portrait);
        SetColor(frame, m_Controller.Color); 
        bool isNormal = GameState == GameStateEnum.Game;
        SetActive(weaponList.gameObject, isNormal);
        SetActive(playerRoot, isNormal);
        SetActive(timeShow.parent, isNormal);
        
        for (int i = 0; i < weaponList.transform.childCount; ++i)
        {
            RefreshWeaponSlot(i);
        }

        m_WeaponsManager.OnAddedWeapon += AddWeapon;
        m_WeaponsManager.OnRemovedWeapon += RemoveWeapon;
        m_WeaponsManager.OnSwitchedToWeapon += ChangeWeapon;
        m_Health.OnDie += OnDie;
        m_Health.OnHit += OnTakeDamage;
        m_Health.OnHealed += OnHealed;
        UnitEventBus.OnBulletHit += BulletHit;
        UnitEventBus.OnUnitKill += UnitKill;
    }

    /// <summary>
    /// 事件调用
    /// </summary>
    public void RefreshMissionIcon()
    {
        SetWndState(true);
        SetSprite(killIcon, taskManager.nowTask.campData.KillSprite);
        SetSprite(enemyShow, taskManager.nowTask.campData.Sprite);
        SetText(diffshow, taskManager.nowTask.difficulty.ToString());
    }



    protected override void FirstShowWnd()
    {
        m_WidthMultiplier = CompasRect.rect.width / VisibilityAngle;
        dirList = new();
        for (int i = 0; i < 36; ++i){
            var item=Instantiate(i%9==0? prefabBig : prefab, CompasRect);
            var text = item.transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>();
            if (i % 9 == 0)
            {
                switch (i / 9)
                {
                    case 0:
                        text.text = "N";
                        break;
                    case 1:
                        text.text = "E";
                        break;
                    case 2:
                        text.text = "S";
                        break;
                    case 3:
                        text.text = "W";
                        break;
                }

            }
            else
            {
                text.text = ""+10 * i;
            }
            dirList.Add(item.GetComponent<CanvasGroup>());
        }
        //Debug.LogError("初始化playerwnd"+ ActorsManager.Player);
        InitHitFlash();
    }

    protected override void ShowWnd()
    {
        SetActive(CompasRect,GameState == GameStateEnum.Game);
    }


    protected override void HideWnd()
    {
        initPlayer = false;
        UnitEventBus.OnBulletHit -= BulletHit;
        UnitEventBus.OnUnitKill -= UnitKill;
        if (m_WeaponsManager)
        {
            m_WeaponsManager.OnAddedWeapon -= AddWeapon;
            m_WeaponsManager.OnRemovedWeapon -= RemoveWeapon;
            m_WeaponsManager.OnSwitchedToWeapon -= ChangeWeapon;
        }
        if (m_Health.IsValidMono())
        {
            m_Health.OnDie -= OnDie;
            m_Health.OnHit -= OnTakeDamage;
            m_Health.OnHealed -= OnHealed;
        }
    }


    void Update()
    {
#if UNITY_EDITOR
        UpdateDebug();
#endif
        if (!initPlayer && ActorsManager.Player!=null&& ActorsManager.Player.transform != null) TryPlayer();
        if(!initPlayer) return;



        UpdateTime();
        UpdateCross();
        UpdateWeapon();
        UpdateSelfDeathState();
        UpdatePlayerStates();
        UpdateFeedback();
        UpdateKill();
    }

    /// <summary>本机自己那行的倒地遮罩（`PlayerStateSelf/Icon/DeathState`，与盟友行同一套结构）。
    /// <para>⚠ 它在**另一个嵌套预制体** `PlayerStateSelf.prefab` 里，拿不到序列化引用 ⇒ 按路径找一次并缓存。</para></summary>
    private GameObject m_SelfDeathState;

    private void UpdateSelfDeathState()
    {
        if (m_SelfDeathState == null)
        {
            var t = playerRoot != null ? playerRoot.Find(SelfStateRowName + "/Icon/DeathState") : null;
            if (t == null) return;
            m_SelfDeathState = t.gameObject;
        }
        if (m_Health == null) return;
        SetActive(m_SelfDeathState, m_Health.GetHpCurrent() <= 0f);
    }

    #region 盟友状态行（players 下除本机自己那行之外的槽位）

    /// <summary>
    /// 一条盟友状态行（预制体里这些节点**没有任何脚本组件**，原先就没有驱动代码）。
    /// <para>▍为什么血/盾/弹药用 <c>UnityEngine.UI.Image.fillAmount</c> 而不是 <c>DynamicBar</c>：
    /// 预制体里这两套行结构不同 —— 本机的 `PlayerStateSelf` 用 `DynamicBar`（带 Animator 动效），
    /// 盟友行是 `Type=Filled / FillMethod=Horizontal` 的普通 Image（`ShidleBar_/ShieldBar` 等）⇒ 直接填比例即可。</para>
    /// </summary>
    private class AllyStateRow
    {
        public GameObject Root;
        public Transform Name;
        public Transform Portrait;
        public Transform Frame;
        public GameObject DeathState;
        public DynamicBar Hp, Shield, Ammo;
    }

    private AllyStateRow[] m_AllyRows;

    /// <summary>上次打日志时的盟友行数（只在"行数变化"时打，避免每帧刷屏）。</summary>
    private int _lastLoggedAllyRows = -1;

    /// <summary>本机自己那行的节点名（它由 `UpdateWeapon` 里的 healthBar/shieldBar/ammoBar 驱动，这里要跳过）。</summary>
    private const string SelfStateRowName = "PlayerStateSelf";

    /// <summary>
    /// 收集盟友行。⚠ 按**子物体顺序**取、不按名字：Unity 自动编号的名字（`PlayerState (1)`）一改名就废。
    /// </summary>
    private void EnsureAllyRows()
    {
        if (m_AllyRows != null) return;
        if (playerRoot == null) { m_AllyRows = new AllyStateRow[0]; return; }

        var rows = new List<AllyStateRow>();
        for (int i = 0; i < playerRoot.childCount; ++i)
        {
            var child = playerRoot.GetChild(i);
            if (child.name == SelfStateRowName) continue;

            var shield = child.Find("ShidleBar_");   // 资产里的拼写就是 Shidle（勿"修正"）
            var hp = child.Find("HpBar_");
            var ammo = child.Find("Ammo_");
            var death = child.Find("Icon/DeathState");
            rows.Add(new AllyStateRow
            {
                Root = child.gameObject,
                Name = child.Find("playerName"),
                Portrait = child.Find("Icon/portrait"),
                Frame = child.Find("frame"),
                DeathState = death != null ? death.gameObject : null,
                // 三根条与 PlayerStateSelf 同款：DynamicBar（bar 主填充 + slider 装饰滑块 + 分段 stage）
                Shield = shield != null ? shield.GetComponent<DynamicBar>() : null,
                Hp = hp != null ? hp.GetComponent<DynamicBar>() : null,
                Ammo = ammo != null ? ammo.GetComponent<DynamicBar>() : null,
            });
        }
        m_AllyRows = rows.ToArray();
    }

    /// <summary>
    /// 刷新盟友状态行：名字 / 血 / 盾 / 弹药系数。
    ///
    /// <para>▍数据来源是**世界里的盟友实体**（`ActorsManager.Players` 里带 `FriendController` 的那些）：
    /// 血盾读它身上那份"镜像生命值"（09 的桥把同步值写进去），弹药系数读 `FriendController.AmmoRatio`。
    /// ⇒ UI 不依赖网络层，房主 / 成员两种角色同一套代码，也不用管"消息什么时候到"。</para>
    ///
    /// <para>▍盟友离场（掉线/强退）怎么收尾：<c>ActorsManager.Unregister</c> 会把它摘出 <c>Players</c>
    /// ⇒ 本方法下一帧自然把多出来的行隐藏掉（本方法每帧由 <c>Update</c> 调用，不需要额外订阅事件）。</para>
    ///
    /// <para>⚠ 真正的前提在上游：**房主端必须收到 <c>PlayerLeftNtf</c>** 才会销毁盟友实例、才会走到这里。
    /// 2026-10-09 修的正是这个 —— <c>SendToAll</c> 不含房主本地、且主动退房那条路根本没发这条消息
    /// ⇒ 房主屏幕上模型与这一行都不会消失（见 <c>NetHostSvc.RemoveMemberAndNotify</c>）。</para>
    /// </summary>
    private void UpdatePlayerStates()
    {
        if (playerRoot == null) return;
        EnsureAllyRows();

        int used = 0;
        var actors = ActorsManager.Players;
        for (int i = 0; i < actors.Count && used < m_AllyRows.Length; ++i)
        {
            var actor = actors[i];
            if (!actor.IsValidMono() || ReferenceEquals(actor, ActorsManager.Player)) continue;

            var t = actor.transform;
            var friend = t.GetComponent<FriendController>();
            if (friend == null) continue;      // 只认盟友实例（本机玩家身上没有这个组件）

            var row = m_AllyRows[used++];
            SetActive(row.Root, true);
            SetText(row.Name, string.IsNullOrEmpty(friend.PlayerName) ? actor.ShowName : friend.PlayerName);
            // 头像/常色：挂角色模型时 FriendController 已把模型的 BaseObject 身份搬到 Actor 上
            if (row.Portrait != null) SetSprite(row.Portrait, actor.Portrait != null ? actor.Portrait : actor.ExtraPortrait);
            if (row.Frame != null) SetColor(row.Frame, actor.Color);

            var health = t.GetComponent<IHealth>();
            float hp = health != null ? health.GetHpRatio() : 0f;
            float shield = health != null ? health.GetShieldRatio() : 0f;
            if (row.Hp != null) row.Hp.SetFill(Mathf.Clamp01(hp));
            if (row.Shield != null) row.Shield.SetFill(Mathf.Clamp01(shield));
            if (row.Ammo != null) row.Ammo.SetFill(Mathf.Clamp01(friend.AmmoRatio));

            // 倒地遮罩（与 Self 那行同一个节点：Icon/DeathState）
            if (row.DeathState != null) SetActive(row.DeathState, health != null && health.GetHpCurrent() <= 0f);
        }

        for (int i = used; i < m_AllyRows.Length; ++i) SetActive(m_AllyRows[i].Root, false);

        // 数据说话：盟友行数变化时打一条 —— "强退后 UI 有没有回落"直接看这行
        if (used != _lastLoggedAllyRows)
        {
            _lastLoggedAllyRows = used;
            FPSGame.Utils.NetSyncLog.SyncLog("HUD 盟友行", $"显示 {used} 行（ActorsManager.Players={ActorsManager.Players.Count} 项）");
        }
    }

    #endregion

    void UpdateTime()
    {
        float seconds = Time.time - m_TaskStartTime;
        int totalSeconds = (int)seconds;
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        int milliseconds = (int)((seconds - totalSeconds) * 100);
        SetText(timeShow, string.Format("{0:D2}:{1:D2}:{2:D2}", minutes, remainingSeconds, milliseconds));
    }

    void UpdateCross()
    {
        var m_PlayerTransform = m_Controller.transform;

        float playerAngle = m_PlayerTransform.localEulerAngles.y;
        if (playerAngle >= 180) playerAngle -= 360;
        for (int i = 0; i < 36; ++i)
        {
            var element = dirList[i];
            float angle = 10 * i - playerAngle;
            if (angle >= 180) angle -= 360;
            if (Tool.In(angle * 2, -VisibilityAngle, VisibilityAngle))
            {
                element.alpha = Mathf.Clamp01(0.03f * (VisibilityAngle / 2 - Mathf.Abs(angle)));
                element.transform.localPosition = new Vector2(m_WidthMultiplier * angle, CompasRect.rect.height * (1 - Mathf.Sin((0.6f * angle + 90) * Mathf.Deg2Rad)));
            }
            else
            {
                element.alpha = 0;
            }
        }

    }
#if UNITY_EDITOR
    float m_AccumulatedDeltaTime = 0f;
    int m_AccumulatedFrameCount = 0;

    private void UpdateDebug()
    {
        m_AccumulatedDeltaTime += Time.deltaTime;
        m_AccumulatedFrameCount++;

        if (m_AccumulatedDeltaTime >= 1)
        {
            int framerate = Mathf.RoundToInt((float)m_AccumulatedFrameCount / m_AccumulatedDeltaTime);
            SetText(framerateCounter, framerate);
            m_AccumulatedDeltaTime = 0f;
            m_AccumulatedFrameCount = 0;
        }
    }
#endif

    #region 武器
    void UpdateWeapon()
    {
        if (!m_ActiveWeapon) return;
        //迫于无奈，直接这边获取了
        SetText(nowAmmoR, m_ActiveWeapon.Magazine.CurrValue.RawInt);
        SetText(remainAmmoR, m_ActiveWeapon.Ammo.CurrValue.RawInt);
        // ⚠ 手雷槽可能为空（未解锁/换装中）⇒ 读之前必须判空，否则 HUD 每帧 NRE
        var grenade = m_WeaponsManager.GetWeaponAtSlotIndex(PlayerWeaponsManager.SlotOf(WeaponTypeEnum.Grenade));
        if (grenade) SetText(GrenadeCount, grenade.Ammo.CurrValue.RawInt + grenade.Magazine.CurrValue.RawInt);
         

        if (m_ActiveSecWeapon)
        {
            SetText(nowAmmoL, m_ActiveSecWeapon.Magazine.CurrValue.RawInt);
            SetText(remainAmmoL, m_ActiveSecWeapon.Ammo.CurrValue.RawInt);
        }

        shieldBar.SetFill(m_Health.GetShieldRatio());
        healthBar.SetFill(m_Health.GetHpRatio());
        ammoBar.SetFill(m_WeaponsManager.TotalRemainAmmoRatio());

        //武器栏的显示
        float delay = Time.time - m_LastChangeTime;
        if (Tool.In(delay, 0, 3)&& weaponList.alpha<1)
        {
            weaponList.alpha = Mathf.Lerp(weaponList.alpha,1.1f, 2 * Time.deltaTime);
        }
        else if (Tool.In(delay, 3, 6)&& weaponList.alpha>0)
        {
            weaponList.alpha = Mathf.Lerp(weaponList.alpha, -0.1f,2*Time.deltaTime);
        }
    }
    // 武器增删（拾取/卸载支援武器）时刷新武器栏对应槽位
    void AddWeapon(WeaponPlayerController newWeapon, int weaponIndex)
    {
        RefreshWeaponSlot(weaponIndex);
    }

    void RemoveWeapon(WeaponPlayerController newWeapon, int weaponIndex)
    {
        RefreshWeaponSlot(weaponIndex);
    }

    /// <summary>
    /// 刷新武器栏第 index 个槽位：有武器则显示子项并设图标，空则隐藏子项。
    /// </summary>
    void RefreshWeaponSlot(int index)
    {
        if (index < 0 || index >= weaponList.transform.childCount) return;
        var slot = weaponList.transform.GetChild(index);
        var item = m_WeaponsManager.GetWeaponAtSlotIndex(index);
        // 空槽隐藏对应 UI 物体（如支援武器初始为空），有武器则显示并设图标
        SetActive(slot.gameObject, item != null);
        SetSprite(weaponList.transform.GetChild(index, 0), item ? item.WeaponIcon : null);
    }
    /// <summary>
    /// 切换武器
    /// </summary>
    /// <param name="weapon"></param>
    /// <param name="isSec"></param>
    void ChangeWeapon(WeaponPlayerController weapon, bool isSec = false)
    {
        //Debug.LogError("窗口显示武器"+weapon+"是副手"+isSec);
        if (isSec) 
        {
            m_ActiveSecWeapon = weapon;
        }
        else
        {
            m_ActiveWeapon = weapon;
        }
        if (!weapon) return;

        if (!isSec)
        {
            SetText(weaponNameR, weapon.WeaponName);
            SetText(weaponTypeR, weapon.WeaponType);




            if (SetActive(remainAmmoR, !weapon.InfiniteAmmo))
            {
                SetText(remainAmmoR, weapon.Ammo.CurrValue.RawInt);
            }
            if (SetActive(nowAmmoR, !weapon.InfiniteMagazine))
            {
                SetText(nowAmmoR, weapon.Magazine.CurrValue.RawInt);
            }
            
            int index = m_WeaponsManager.ActiveWeaponIndex;
            if (index < weaponList.transform.childCount)
            {
                m_LastChangeTime = Time.time;
                // 遍历全部槽位（含支援武器），当前激活项高亮，其余调暗；隐藏项保持隐藏
                for (int i = 0; i < weaponList.transform.childCount; ++i)
                {
                    var slot = weaponList.transform.GetChild(i);
                    if (!slot.gameObject.activeSelf) continue;
                    slot.GetComponent<CanvasGroup>().alpha = index == i ? 1 : 0.5f;
                }
            }
            SetActive(weaponNameL.parent, false);
            SetActive(nowAmmoL.parent, false);
        }
        else
        {
            SetText(weaponNameL, weapon.WeaponName);
            SetText(weaponTypeL, weapon.WeaponType);

            if (SetActive(remainAmmoL, !weapon.InfiniteAmmo))
            {
                SetText(remainAmmoL, weapon.Ammo.CurrValue.RawInt);
            }
            if (SetActive(nowAmmoL, !weapon.InfiniteMagazine))
            {
                SetText(nowAmmoL, weapon.Magazine.CurrValue.RawInt);
            }
          
            SetActive(weaponNameL.parent, true);
            SetActive(nowAmmoL.parent, true);
        }
    }

    void OnDie(GameObject source)
    {
        //m_Controller = null;


    }

    #endregion
    #region 击杀

    void UnitKill(Actor attacker, Actor victim)
    {
        //击杀其他单位不算
        if (victim.Type == UnitTypeEnum.Other) return;
        //Debug.LogError(attacker);
        //故意不判定队伍的，这样子友军击毙也能算
        //不需要判定是玩家几，只要知道是本机玩家就行
        if ((attacker as IActor) == ActorsManager.Player || attacker.Owner == ActorsManager.Player)
        {
            ++m_KillCount;
            m_LastKillTime = Time.time;
            wndManager.PlaySound(new("UI/HUD/Kill", AudioGroups.UI) {cache=true });
            SetText(killCount,"x"+m_KillCount);
            var color = Color.HSVToRGB(Mathf.Clamp01(0.5f + m_KillCount / 100f), 0.5f, 1);
            SetColor(killIcon, color);
            SetColor(killCount, color);
            //SetAlpha(killRoot,1);
        }
    }

    void UpdateKill()
    {
        float normalized = (Time.time - m_LastKillTime) / 10;

        if (normalized <= 1f)
        {
            float flashAmount =4 - 4.01f*normalized;
            killRoot.alpha = flashAmount;
        }else if (m_KillCount>0)
        {
            m_KillCount = 0;
        }

    }
    #endregion
}
}
