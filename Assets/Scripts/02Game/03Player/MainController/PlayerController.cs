using System.Collections.Generic;
using Core;
using Core.Interface;
using FPSGame.Attribute;
using GameContract;
using PEMaths;
using RootMotion.FinalIK;

using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using Utils;

/// <summary>
/// 主控制器
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler), typeof(AudioSource))]
public class PlayerController : BaseSelfMoveableController
{

    [Foldout("一般", true)]

    [SerializeField]
    [InspectorName("玩家倒地摄像机")]
    private Camera PlayerDownCamera;

    [Range(0, 1f)]
    [InspectorName("摄像机所在位置的角色高度")]
    public float CameraHeightRatio = 0.9f;
    [InspectorName("站立高度")]
    public float CapsuleHeightStanding = 1.8f;

    [DisplayField]
    [InspectorName("所属玩家")]
    public int PlayerIndex;


    [Foldout("冲刺", true)]
    [InspectorName("冲刺持续时间")]
    /// <summary>冲刺持续时间</summary>
    public float SprintDuration = 1f;

    [InspectorName("冲刺倍数")]
    /// <summary>冲刺倍数</summary>
    public float SprintSpeedModifier = 2f;

    [InspectorName("冲刺冷却")]
    /// <summary>冲刺冷却</summary>
    public float SprintCool = 15f;

    [InspectorName("冲刺时重力程度")]
    /// <summary>冲刺时重力程度</summary>
    [Range(0, 1f)]
    public float SprintGravity = 0.2f;



    public RoleData_SO Cfg { get; private set; }

    public bool IsDead { get; private set; }

    public Transform ModleRoot { get; private set; }

    public PlayerWeaponsManager WeaponsManager { get; private set; }


    float m_sprintTime;//冲刺的剩余时间


    public void Init(int playerIndex)
    {
        PlayerIndex = playerIndex;
    }

    protected override void Awake()
    {
        base.Awake();
        WeaponsManager = GetComponent<PlayerWeaponsManager>();
        WeaponsManager.OnAim+= OnAim;
    }

    private void OnDestroy()
    {
        if (!WeaponsManager) return;
        WeaponsManager.OnAim -= OnAim;
    }

    protected override void Start()
    {
        Controller.enableOverlapRecovery = true;
        Health.OnDie += OnDie;
        Health.OnRevive += OnRevive;
        WeaponsManager.OnShoot += WeapomRecoil;
        m_sprintTime = -SprintCool;
        if (GameRoot.GameState == GameStateEnum.Bridge) Health.Invincible = true;

    }
    private void OnEnable()
    {
        int mainCameraIgnore = LayerMask.NameToLayer("MainCameraIgnore");
        PlayerCamera.cullingMask &= ~(1 << mainCameraIgnore);
    }
    //组件关闭（进载具之类的第三人称），重新显示脑袋
    private void OnDisable()
    {
        int mainCameraIgnore = LayerMask.NameToLayer("MainCameraIgnore");
        PlayerCamera.cullingMask |= (1 << mainCameraIgnore);
    }

    public void OnAim(bool state)
    {
        GetAttribute(UnitAttrType.AngularSpeed).AddModifier(ModifierType.Factor, (state?-1:1)*(1-(PEInt)AimingRotationMultiplier));
    }


    public void SetBody(Transform modleRoot, RoleData_SO cfg, List<WeaponPlayerController> extraWeapons)
    {
        if (ModleRoot) {
            DestroyImmediate(ModleRoot.gameObject); 
        }


        ModleRoot = modleRoot;
        BaseObject baseMono = modleRoot.GetComponent<BaseObject>();
        m_Actor.ShowName = baseMono.ShowName;
        m_Actor.Id = baseMono.Id;
        m_Actor.Portrait = baseMono.Portrait;
        m_Actor.ExtraPortrait = baseMono.ExtraPortrait;
        m_Actor.Color = baseMono.Color;
        m_Anim = modleRoot.GetComponent<Animator>();



        Tool.Destroy(baseMono);
        Tool.Destroy(modleRoot.GetComponent<Collider>());
        
        modleRoot.SetParent(transform,false);
        modleRoot.localPosition = Vector3.zero;
        modleRoot.localEulerAngles = Vector3.zero;
        modleRoot.Find(item=>item.name.Contains("Head")).gameObject.layer=LayerMask.NameToLayer("MainCameraIgnore");
        var hand = modleRoot.Find(item => item.name.Contains("Hand"));
        hand.gameObject.layer = LayerMask.NameToLayer("FirstPersonWeapon");
        //使手臂在屏幕外也更新
        hand.GetComponent<SkinnedMeshRenderer>().updateWhenOffscreen = true;

        this.Cfg = cfg;
        modleRoot.GetComponent<RootMotion.FinalIK.LookAtController>().target = PlayerCamera.transform.GetChild(0);
        List<WeaponPlayerController> StartingWeapons=new(cfg.GetStartingWeapons(ArchiveSvc.Archive.GetRoleCfg(m_Actor.Id)));
        StartingWeapons.AddRange(extraWeapons);
        WeaponsManager.SetStatrtWeapon(StartingWeapons);

        grounderIK = modleRoot.GetComponent<GrounderFBBIK>();
    }


    public override Vector3 GetInputMove()
    {
        return IsDead?Vector3.zero:base.GetInputMove();
    }

    protected override void Update()
    {
        //在这一帧起床
        DownHandleCharacterMovement();
        base.Update();
        UpdateSprint();
        //base.Update();
        HandleKei();

        
    }
    protected override void LateUpdate()
    {
        base.LateUpdate();
        WeaponsManager.FirstPersonSocket.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
    }

    protected override void TryJump()
    {
        if (IsDead) return;
        base.TryJump();
    }

    protected override void HandleRotation()
    {
        if (IsDead) return;
        base.HandleRotation();
    }
    void UpdateSprint()
    {
        // 冲刺
        if (m_sprintTime < -SprintCool && InputHandler.GetSprintInputDouble())
        {
            m_sprintTime = SprintDuration;
            MoveSpeedScale *= SprintSpeedModifier;
            GravityDownForce *= SprintGravity;
        }
        else if (m_sprintTime > 0 && m_sprintTime < Time.deltaTime)
        {
            MoveSpeedScale /= SprintSpeedModifier;
            GravityDownForce /= SprintGravity;
        }
        m_sprintTime -= Time.deltaTime;
    }

   
    //武器后坐力
    public void WeapomRecoil(WeaponController weapon) {
        var power = (weapon as WeaponPlayerController).GetRecoil() * 1.5f;
        VerticalNewRecoil += power;
    }

    /// <summary>
    /// 倒地后的移动控制
    /// </summary>
    void DownHandleCharacterMovement()
    {
        if (!IsDead) return;
        float mouseX = InputHandler.GetLookInputsHorizontal();
        float mouseY = InputHandler.GetLookInputsVertical(); // 你需要获取垂直输入

        m_CameraHorizontalAngle += mouseX * GetAttribute(UnitAttrType.AngularSpeed).FinalValue.RawFloat * Time.deltaTime;
        m_CameraVerticalAngle += mouseY * GetAttribute(UnitAttrType.AngularSpeed).FinalValue.RawFloat * Time.deltaTime;
        m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -20f, 70f); // 限制上下角度

        Quaternion rotation = Quaternion.Euler(
            m_CameraVerticalAngle,       // 上下
            transform.eulerAngles.y - 180 + m_CameraHorizontalAngle, // 左右
            0
        );

        Vector3 offsetDir = rotation * Vector3.back;
        Vector3 cameraFinalPos = CenterPos+Vector3.up + offsetDir * 4f;

        // 6. 应用位置 + 注意
        PlayerDownCamera.transform.position =Vector3.Lerp(PlayerDownCamera.transform.position, cameraFinalPos, GetAttribute(UnitAttrType.AngularSpeed).FinalValue.RawFloat * Time.deltaTime/2);
        PlayerDownCamera.transform.LookAt(CenterPos);

        //呼叫
        if (InputHandler.GetJumpInputUp())
        {
            BattleEventSub.CallKai(gameObject, transform.position);
            GlobalEventSub.PlayMeetSpeech(gameObject, SpeechTypeEnum.Help);
        }
    }

    void OnDie(GameObject source)
    {
        IsDead = true;
        PlayerCamera.gameObject.SetActive(false);
        PlayerDownCamera.gameObject.SetActive(true);

        WeaponsManager.SwitchToWeaponIndex("", true, false, true);

        //GlobalEventManager.PlayerDead(m_Actor);

        if (GameRoot.GameState == GameStateEnum.Game) BattleManager.Instance.AddBattleDataItem(PlayerIndex, "死亡次数");
        WeaponsManager.enabled = false;
    }

    void OnRevive()
    {
        IsDead = false;
        PlayerCamera.gameObject.SetActive(true);
        PlayerDownCamera.gameObject.SetActive(false);

        WeaponsManager.enabled = true;
        WeaponsManager.SwitchToWeaponIndex(1, true, false, true);
        GlobalEventSub.PlayMeetSpeech(gameObject, SpeechTypeEnum.Thank);
        m_Actor.ActorState = ActorState.Normal;

        //喘息之时现在免费送
        m_Actor.AddTag(ActorFlag.Invincible);
        GameRoot.CreateTimer(() => m_Actor.RemoveTag(ActorFlag.Invincible), 4);
    }
    void HandleKei()
    {
        if (!IsDead && InputHandler.GetMuleDown())
        {
            BattleEventSub.CallKai(gameObject, transform.position);
            GlobalEventSub.PlayMeetSpeech(gameObject, SpeechTypeEnum.Kei);
        }
    }

    /// <summary>
    /// 使用补给
    /// </summary>
    public void UseSupply()
    {
        UseCaisson();
        UseMedicalBag();
    }
    /// <summary>
    /// 使用弹药箱
    /// </summary>
    public void UseCaisson()
    {
        WeaponsManager.UseSupply();
    }
    /// <summary>
    /// 使用医疗包
    /// </summary>
    public void UseMedicalBag()
    {
        Health.Heal(Health.MaxHealth / 2);
    }


}


