using System.Collections;
using System.Collections.Generic;
using Core;
using FPSGame.UI;
using GameContract;
using PEMaths;
using Unity.BaseTool;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public partial class PlayerWnd : WindowRoot
{
    public RectTransform CompasRect;
    public int VisibilityAngle = 180;

    public GameObject prefab, prefabBig;

    List<CanvasGroup> dirList;

    #region 血条
    [Foldout("血条", true)]
    [SerializeField]
    private Transform playerRoot,playerName,portrait, frame;
    [SerializeField]
    DynamicBar healthBar, shieldBar, ammoBar;
    #endregion

    #region 武器1
    [Foldout("武器1", true)]
    [SerializeField]
    private Transform weaponNameR,weaponTypeR,nowAmmoR,remainAmmoR,grenadeCountR, grenadeKeyR;

    [SerializeField]
    CanvasGroup weaponList;
    #endregion
    #region 武器2
    
    [Foldout("武器2", true)]
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

    #region
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

    [SerializeField]
    PlayerController m_Controller;
    Health m_Health;
    [SerializeField]
    PlayerWeaponsManager m_WeaponsManager;
    [SerializeField]
    WeaponPlayerController m_ActiveWeapon, m_ActiveSecWeapon;
    float m_LastChangeTime;
    float m_WidthMultiplier;

    private void TryPlayer()
    {
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
        bool isNormal = GameRoot.GameState == GameStateEnum.Game;
        SetActive(weaponList.gameObject, isNormal);
        SetActive(playerRoot, isNormal);
        SetActive(timeShow.parent, isNormal);
        
        for (int i = 0; i < 3; ++i)
        {
            var item = m_WeaponsManager.GetWeaponAtSlotIndex(i) ;
            SetSprite(weaponList.transform.GetChild(i, 0), item.WeaponIcon);
        }

        m_WeaponsManager.OnAddedWeapon += AddWeapon;
        m_WeaponsManager.OnRemovedWeapon += RemoveWeapon;
        m_WeaponsManager.OnSwitchedToWeapon += ChangeWeapon;
        m_Health.OnDie += OnDie;
        m_Health.OnHit += OnTakeDamage;
        m_Health.OnHealed += OnHealed;
        GlobalEventManager.OnBulletHit += BulletHit;
        GlobalEventManager.OnUnitKill += UnitKill;
    }


    private void OnGameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        if (exit == entry) return;
        if (entry== GameStateEnum.Game)
        {
            SetSprite(killIcon, taskManager.nowTask.campData.KillSprite);
            SetSprite(enemyShow, taskManager.nowTask.campData.Sprite);
            SetText(diffshow, taskManager.nowTask.difficulty.ToString());
        }
      
    }

    public override void Init()
    {
        GameRoot.OnGameStateChange += OnGameStateChange;
    }
    public override void UnInit()
    {
        GameRoot.OnGameStateChange -= OnGameStateChange;
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

        InitHitFlash();
    }

    protected override void ShowWnd()
    {

    }

    private void OnDestroy()
    {
        //UnInitWnd();
    }

    protected override void HideWnd()
    {
        GlobalEventManager.OnBulletHit -= BulletHit;
        GlobalEventManager.OnUnitKill -= UnitKill;
        if (m_WeaponsManager)
        {
            m_WeaponsManager.OnAddedWeapon -= AddWeapon;
            m_WeaponsManager.OnRemovedWeapon -= RemoveWeapon;
            m_WeaponsManager.OnSwitchedToWeapon -= ChangeWeapon;
        }
        if (m_Health)
        {
            m_Health.OnDie -= OnDie;
            m_Health.OnHit += OnTakeDamage;
            m_Health.OnHealed += OnHealed;
        }
    }


    void Update()
    {
#if UNITY_EDITOR
        UpdateDebug();
#endif
        if (!m_Controller && ActorsManager.Player!=null&& ActorsManager.Player.transform != null) TryPlayer();
        if(!m_Controller) return;



        UpdateTime();
        UpdateCross();
        UpdateWeapon();
        UpdateFeedback();
        UpdateKill();
    }

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
        var grenade = m_WeaponsManager.GetWeaponAtSlotIndex((int)WeaponTypeEnum.Grenade);
           
        SetText(GrenadeCount, (grenade.AttrFinal(WeaponAttrType.Ammo) + grenade.AttrFinal(WeaponAttrType.Magazine)).RawInt);
         

        if (m_ActiveSecWeapon)
        {
            SetText(nowAmmoL, m_ActiveSecWeapon.Magazine.CurrValue.RawInt);
            SetText(remainAmmoL, m_ActiveSecWeapon.Ammo.CurrValue.RawInt);
        }

        shieldBar.SetBar(m_Health.GetShieldRatio());
        healthBar.SetBar(m_Health.GetHpRatio());
        ammoBar.SetBar(m_WeaponsManager.TotalRemainAmmoRatio());

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
    //讲道理我们根本不会添加武器和移除武器
    void AddWeapon(WeaponPlayerController newWeapon, int weaponIndex)
    {

    }

    void RemoveWeapon(WeaponPlayerController newWeapon, int weaponIndex)
    {

    }
    /// <summary>
    /// 切换武器
    /// </summary>
    /// <param name="weapon"></param>
    /// <param name="isSec"></param>
    void ChangeWeapon(WeaponPlayerController weapon, bool isSec = false)
    {
        //Debug.LogError("窗口显示武器:"+weapon+"是副手"+isSec);
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
            if (index < 3)
            {
                m_LastChangeTime = Time.time;
                for (int i = 0; i < 3; ++i)
                {
                    weaponList.transform.GetChild(i).GetComponent<CanvasGroup>().alpha = index == i ? 1 : 0.5f;
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
        //故意不判定队伍的，这样子友军击毙也能算头
        //不需要判定是玩家几，只要知道是本机玩家就行
        if ((attacker as I_Actor) == ActorsManager.Player || attacker.Owner == ActorsManager.Player)
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