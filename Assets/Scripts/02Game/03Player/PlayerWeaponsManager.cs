using System.Collections.Generic;
using System.Linq;
using Core;
using RootMotion.FinalIK;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using Utils;
using static AirdropController;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerWeaponsManager : MonoBehaviour
    {

        //private const string _FlareGunName = "激光指示器";

        public enum WeaponSwitchState
        {
            /// <summary>拿起</summary>
            Up,
            /// <summary>放下</summary>
            Down,
            /// <summary>(过渡状态)放下旧武器</summary>
            PutDownPrevious,
            /// <summary>(过渡状态)拿起新武器</summary>
            PutUpNew,
        }



        [Foldout("点位", true)]
        [CustomLabel("用于避免看到武器投掷几何形状的辅助摄像头")]
        public Camera WeaponCamera;
        [CustomLabel("第一人称武器根")]
        public Transform FirstPersonSocket;
        [CustomLabel("武器根")]
        public Transform WeaponParentSocket;
        [CustomLabel("未瞄准时的位置")]
        public Transform DefaultWeaponPosition;
        [CustomLabel("瞄准时的位置")]
        public Transform AimingWeaponPosition;
        [CustomLabel("放下时的位置")]//新的武器从这个点位移动到WeaponParentSocket
        public Transform DownWeaponPosition;



        [Foldout("摆动", true)]
        [CustomLabel("移动时武器在屏幕上移动的频率")]
        public float BobFrequency = 10f;

        [CustomLabel("武器摆锤的速度")]
        public float BobSharpness = 10f;

        [CustomLabel("不瞄准时武器摆动的距离")]
        public float DefaultBobAmount = 0.05f;

        [CustomLabel("瞄准时武器摆动的距离")]
        public float AimingBobAmount = 0.02f;

        [Foldout("后坐力", true)]
        [CustomLabel("这将影响后坐力移动武器的速度，值越大，速度越快")]
        public float RecoilSharpness = 50f;

        [CustomLabel("后坐力可以影响武器的最大距离")]
        public float MaxRecoilDistance = 0.5f;

        [CustomLabel("反冲结束后，武器返回原始位置的速度有多快")]
        public float RecoilRestitutionSharpness = 10f;

        [Foldout("其他", true)]
        [CustomLabel("播放瞄准动画的速度")]
        public float AimingAnimationSpeed = 10f;

        [CustomLabel("不瞄准时的视野")]
        public float DefaultFov = 60f;

        [CustomLabel("应用于武器相机的常规视场部分")]
        public float WeaponFovMultiplier = 1f;

        [CustomLabel("在第二次切换武器之前延迟，以避免从鼠标滚轮接收多个输入")]
        public float WeaponSwitchDelay = 1f;

        [CustomLabel("将FPS武器游戏对象设置为的图层")]
        public LayerMask FpsWeaponLayer;

        private bool m_isAiming;
        public bool IsAiming {
            get => m_isAiming;
            private set {
                if (IsAiming != value && OnAim != null)
                {
                    OnAim.Invoke(value);
                }
                m_isAiming = value;
            }
        }

        //private bool IsDown => m_PlayerCharacterController.IsDead;
        /*
        public int ActiveWeaponIndex { get; private set; } = -1;
        public int ActiveSecWeaponIndex { get; private set; } = -1;
        */
        public int ActiveWeaponIndex  = -1;
        public int ActiveSecWeaponIndex = -1;

        public event UnityAction<WeaponPlayerController,bool> OnSwitchedToWeapon;//武器数据，是否是副手
        public event UnityAction<bool> OnAim;
        public event UnityAction<WeaponPlayerController, int> OnAddedWeapon;
        public event UnityAction<WeaponPlayerController, int> OnRemovedWeapon;
        public event UnityAction<WeaponPlayerController> OnShoot;

        [SerializeField]
        WeaponPlayerController[] m_WeaponSlots = new WeaponPlayerController[9]; // 9 available weapon slots
        PlayerInputHandler m_InputHandler;
        PlayerController m_PlayerCharacterController;
        [SerializeField]
        FullBodyBipedIK m_fullIk;

        float m_PlayerAngle;

        float m_WeaponBobFactor;
        Vector3 m_LastCharacterPosition;
        Vector3 m_WeaponMainLocalPosition;

        Vector3 m_WeaponBobLocalPosition;//武器摆动坐标
        Vector3 m_WeaponRecoilLocalPosition;//武器后坐力坐标
        Vector3 m_AccumulatedRecoil;
        float m_TimeStartedWeaponSwitch;


        [SerializeField]
        WeaponSwitchState m_WeaponSwitchState;// { get; set; }
        [SerializeField]
        int m_SwitchNewWeaponIndex;
        bool m_SwitchNewWeaponAllowDual;

        float closeAimDelay;

        private void Awake()
        {
            m_InputHandler = GetComponent<PlayerInputHandler>();
            m_PlayerCharacterController = GetComponent<PlayerController>();
            //m_WeaponSwitchState = WeaponSwitchState.Down;
            OnSwitchedToWeapon += OnWeaponSwitched;
            OnAim += OnAiming;
            GameRoot.OnWindowStateChange += OnAirdrop;

            GlobalEventManager.OnSelectAirdrop += OnInputCompletedAirdrop;
            GlobalEventManager.OnCancelAirdrop += OnCancelAirdrop;
            GlobalEventManager.OnAirdrop += OnAirdrop;
            GlobalEventManager.OnFurnitureOperate += OnOperation;
        }

        void Start()
        {

            //ActiveWeaponIndex = -1;


            SetFov(DefaultFov);

        }


        private void OnDestroy()
        {
            OnSwitchedToWeapon -= OnWeaponSwitched;
            OnAim -= OnAiming;
            GameRoot.OnWindowStateChange -= OnAirdrop;
            GlobalEventManager.OnSelectAirdrop -= OnInputCompletedAirdrop;
            GlobalEventManager.OnCancelAirdrop -= OnCancelAirdrop;
            GlobalEventManager.OnAirdrop -= OnAirdrop;
            GlobalEventManager.OnFurnitureOperate -= OnOperation;
        }

        void Update()
        {

            //if (IsDown) return;
            WeaponPlayerController activeWeapon = GetActiveWeapon();
            WeaponPlayerController activeSecWeapon = GetActiveSecWeapon();


            //设置瞄准时装弹也不立即结束瞄准
            if (activeWeapon != null && activeWeapon.IsReloading && IsAiming)//在瞄准了肯定不是双持
            {
                if ((closeAimDelay += Time.deltaTime) > 0.3f)
                {
                    IsAiming = false;
                    closeAimDelay = 0;
                }
            }
            // 判断是否在瞄准(完全不缩放的武器无法瞄准)
            if(activeWeapon != null && !activeWeapon.IsReloading && m_WeaponSwitchState == WeaponSwitchState.Up)
            {
                IsAiming = !activeSecWeapon && activeWeapon.AimZoomRatio < 1 && m_InputHandler.GetAimInputHeld();
            }


            UpdateFirstWeapon();
            UpdateSecWeapon();

            UpdateTrySwitchWeapon();
            UpdateFlareGun();
            UpdateThrow();

        }

        private void UpdateFirstWeapon()
        {
            WeaponPlayerController activeWeapon = GetActiveWeapon();
            //有正常状态的武器
            if (activeWeapon != null && !activeWeapon.IsReloading && m_WeaponSwitchState == WeaponSwitchState.Up)
            {
                //没有自动换弹且按下键且弹匣不满
                if (!activeWeapon.HasFlag( WeaponFlag.AutomaticReload) && m_InputHandler.GetReloadDown() && activeWeapon.Magazine.ScaleValue < 1)
                {
                    activeWeapon.TryManualReload();
                    return;
                }
                //拿手雷的时候不能左键射击
                if (ActiveWeaponIndex == (int)WeaponTypeEnum.Grenade) return;

                bool hasFired = false;
                if (!GetActiveSecWeapon())//单持武器时,正常左键射击
                {
                    hasFired = activeWeapon.HandleShootInputs(
                       m_InputHandler.GetFireInputDown(),
                       m_InputHandler.GetFireInputHeld(),
                       m_InputHandler.GetFireInputReleased(),
                       IsAiming);
                }
                else //双持武器时，反向控制
                {
                    hasFired = activeWeapon.HandleShootInputs(
                        m_InputHandler.GetAimInputDown(),
                        m_InputHandler.GetAimInputHeld(),
                        m_InputHandler.GetAimInputReleased(),
                        IsAiming);
                }
                // handle shooting
   

                // Handle accumulating recoil
                if (hasFired)
                {
                    m_AccumulatedRecoil += Vector3.back * activeWeapon.RecoilForce;
                    m_AccumulatedRecoil = Vector3.ClampMagnitude(m_AccumulatedRecoil, MaxRecoilDistance);
                }
            }

        }

        private void UpdateSecWeapon()
        {
            WeaponPlayerController activeSecWeapon = GetActiveSecWeapon();
            //有正常状态的武器
            if (activeSecWeapon != null && !activeSecWeapon.IsReloading && m_WeaponSwitchState == WeaponSwitchState.Up)
            {
                //没有自动换弹且按下键且弹匣不满
                if (!activeSecWeapon.HasFlag(WeaponFlag.AutomaticReload) && m_InputHandler.GetReloadDown() && activeSecWeapon.Magazine.ScaleValue < 1)
                {
                    activeSecWeapon.TryManualReload();
                    return;
                }

                //为了不那么反直觉，双持武器时，右键发射右侧(主武器)
                bool hasFired = activeSecWeapon.HandleShootInputs(
                    m_InputHandler.GetFireInputDown(),
                    m_InputHandler.GetFireInputHeld(),
                    m_InputHandler.GetFireInputReleased(),
                    IsAiming);

                // Handle accumulating recoil
                if (hasFired)
                {
                    m_AccumulatedRecoil += Vector3.back * activeSecWeapon.RecoilForce;
                    m_AccumulatedRecoil = Vector3.ClampMagnitude(m_AccumulatedRecoil, MaxRecoilDistance);
                }
            }

        }



        /// <summary>
        /// 切换武器
        /// </summary>
        private void UpdateTrySwitchWeapon()
        {
            //切换武器
            //1.不在瞄准
            //2.手上没有武器或者武器没有在蓄力(去掉)
            //3.武器切换状态为UP或者Down
            if (!IsAiming &&
                (m_WeaponSwitchState == WeaponSwitchState.Up || m_WeaponSwitchState == WeaponSwitchState.Down))
            {

                //优先滚轮切换
                int switchWeaponInput = m_InputHandler.GetSwitchWeaponInput();
                if (switchWeaponInput != 0)
                {
                    bool switchUp = switchWeaponInput > 0;
                    SwitchWeapon(switchUp);
                }
                else
                {
                   
                    switchWeaponInput = m_InputHandler.GetSelectWeaponInput();
                    if (switchWeaponInput != 0)
                    {
                        //然后尝试数字键切换(因为这里输入是1-9，但是武器槽实际上是0-8)
                        if (GetWeaponAtSlotIndex(switchWeaponInput - 1) != null)
                            SwitchToWeaponIndex(switchWeaponInput - 1,false,true);
                    }
                }
            }

        }

        private void UpdateFlareGun()
        {
            //投掷不会打断指示器
            if (m_InputHandler.GetThrow()) return;
            if (m_InputHandler.GetCrouchDown())
            {
                if (ActiveWeaponIndex != (int)WeaponTypeEnum.FlareGun) m_LastWeaponIndex = ActiveWeaponIndex;
                SwitchToWeaponIndex((int)WeaponTypeEnum.FlareGun, true,false,false);
            }
            if (m_InputHandler.GetCrouchUp())
            {
                //信号枪重置原武器
                SwitchToWeaponIndex(m_LastWeaponIndex, true, false, false);
            }
        }

        private void UpdateThrow()
        {
            //指示器不会打断投掷
            if (m_InputHandler.GetCrouch()) return;
            var grenade= (int)WeaponTypeEnum.Grenade;
            if (m_InputHandler.GetThrowDown() && GetWeaponAtSlotIndex(grenade).Magazine.CurrValue> 0)
            {
                if (ActiveWeaponIndex != grenade) m_LastWeaponIndex = ActiveWeaponIndex;
                SwitchToWeaponIndex((int)WeaponTypeEnum.Grenade, true, false,true);
            }
            if (m_InputHandler.GetThrowUP())
            {
                if (ActiveWeaponIndex == grenade)
                {
                    WeaponPlayerController activeSecWeapon = GetActiveWeapon();
                    activeSecWeapon.HandleShootInputs(true, false, false, IsAiming);
                    //投掷重置原武器
                    SwitchToWeaponIndex(m_LastWeaponIndex, true, false, true);
                }
            }
        }
        

        #region 手臂/身体位置
        //在LateUpdate中更新各种动画功能，因为它需要覆盖动画手臂位置
        void LateUpdate()
        {
            UpdatePlayerAngle();
            UpdateWeaponAiming();
            UpdateWeaponBob();
            UpdateWeaponRecoil();
            UpdateWeaponSwitching();

            m_PlayerCharacterController.ModleRoot.localEulerAngles = new(0,Mathf.Lerp(m_PlayerCharacterController.ModleRoot.localEulerAngles.y,m_PlayerAngle, Time.deltaTime * 5), 0);
        
            //根据所有组合动画影响设置最终武器插座位置
            WeaponParentSocket.localPosition = Vector3.Lerp(WeaponParentSocket.localPosition, m_WeaponMainLocalPosition + m_WeaponBobLocalPosition + m_WeaponRecoilLocalPosition,Time.deltaTime* BobSharpness);
        }
        private void UpdatePlayerAngle()
        {
            
            WeaponPlayerController activeWeapon = GetActiveWeapon();
            WeaponPlayerController activeSecWeapon = GetActiveSecWeapon();
            //设置手持武器时的侧身
            if (activeWeapon && activeSecWeapon)
            {
                m_PlayerAngle=(activeWeapon.playerAngle + activeSecWeapon.playerAngle) * 0.5f;
            }
            else if (activeWeapon)//不会出现没有主武器但有副武器的问题
            {
                m_PlayerAngle = activeWeapon.playerAngle;
            }
        }

 

        // Updates weapon position and camera FoV for the aiming transition
        void UpdateWeaponAiming()
        {
            if (m_WeaponSwitchState == WeaponSwitchState.Up)
            {
                WeaponPlayerController activeWeapon = GetActiveWeapon();
                if (IsAiming && activeWeapon)
                {
                    m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition,
                        AimingWeaponPosition.localPosition + activeWeapon.AimOffset,
                        AimingAnimationSpeed * Time.deltaTime);
                    SetFov(Mathf.Lerp(m_PlayerCharacterController.PlayerCamera.fieldOfView,
                        activeWeapon.AimZoomRatio * DefaultFov, AimingAnimationSpeed * Time.deltaTime));
                }
                else
                {
                    m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition,
                        DefaultWeaponPosition.localPosition, AimingAnimationSpeed * Time.deltaTime);
                    SetFov(Mathf.Lerp(m_PlayerCharacterController.PlayerCamera.fieldOfView, DefaultFov,
                        AimingAnimationSpeed * Time.deltaTime));
                }
            }
        }

        // 根据角色速度更新武器摆锤动画
        void UpdateWeaponBob()
        {
            if (Time.deltaTime > 0f)
            {
                //其实是静止状态
                bool isStatic = false;
                //相对位置变化
                Vector3 playerCharacterVelocity = m_PlayerCharacterController.transform.position - m_LastCharacterPosition;
                if (playerCharacterVelocity.magnitude<0.01f)
                {
                    playerCharacterVelocity = m_PlayerCharacterController.transform.forward*Time.deltaTime* m_PlayerCharacterController.MaxSpeedOnGround*0.5f;
                    isStatic = true;
                }
                playerCharacterVelocity /= Time.deltaTime;
                //根据我们与最大地面运动速度的接近程度计算平滑的武器摆锤量
                float characterMovementFactor = 0f;
                if (m_PlayerCharacterController.IsGrounded)
                {
                    characterMovementFactor =
                        Mathf.Clamp01(playerCharacterVelocity.magnitude /
                                      (m_PlayerCharacterController.MaxSpeedOnGround *
                                       m_PlayerCharacterController.SprintSpeedGroundModifier));
                }
                //摆锤幅度(即使停下也会保持一小段时间)
                m_WeaponBobFactor =
                    Mathf.Lerp(m_WeaponBobFactor, characterMovementFactor, BobSharpness * Time.deltaTime);

                //基于正弦函数计算垂直和水平武器摆锤值
                float bobAmount = IsAiming ? AimingBobAmount : DefaultBobAmount;
                float frequency = BobFrequency *(isStatic?0.1f:1);
                float hBobValue = Mathf.Sin(Time.time * frequency) * bobAmount * m_WeaponBobFactor;
                float vBobValue = ((Mathf.Sin(Time.time * frequency * 2f) * 0.5f) + 0.5f) * bobAmount *
                                  m_WeaponBobFactor;

                // Apply weapon bob
                m_WeaponBobLocalPosition.x = hBobValue;
                m_WeaponBobLocalPosition.y = Mathf.Abs(vBobValue);

                m_LastCharacterPosition = m_PlayerCharacterController.transform.position;
            }
        }

        //更新武器后坐力动画
        void UpdateWeaponRecoil()
        {
            //如果累积反冲距离当前位置更远，则使当前位置朝反冲目标移动
            if (m_WeaponRecoilLocalPosition.z >= m_AccumulatedRecoil.z * 0.99f)
            {
                m_WeaponRecoilLocalPosition = Vector3.Lerp(m_WeaponRecoilLocalPosition, m_AccumulatedRecoil,
                    RecoilSharpness * Time.deltaTime);
            }
            // otherwise, move recoil position to make it recover towards its resting pose
            else
            {
                m_WeaponRecoilLocalPosition = Vector3.Lerp(m_WeaponRecoilLocalPosition, Vector3.zero,
                    RecoilRestitutionSharpness * Time.deltaTime);
                m_AccumulatedRecoil = m_WeaponRecoilLocalPosition;
            }
        }

        /// <summary>
        /// 更新切换武器的动画过渡
        /// </summary>
        void UpdateWeaponSwitching()
        {
            //计算武器开关触发后的时间比（0-1）
            float switchingTimeFactor = 0f;
            if (WeaponSwitchDelay == 0f)
            {
                switchingTimeFactor = 1f;
            }
            else
            {
                switchingTimeFactor = Mathf.Clamp01((Time.time - m_TimeStartedWeaponSwitch) / WeaponSwitchDelay);
            }


            //处理转换到新状态
            if (switchingTimeFactor >= 1f)
            {
                if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
                {

                    WeaponPlayerController oldWeapon = GetWeaponAtSlotIndex(ActiveWeaponIndex);
                    WeaponPlayerController oldSecWeapon = GetWeaponAtSlotIndex(ActiveSecWeaponIndex);
                    WeaponPlayerController newWeapon = GetWeaponAtSlotIndex(m_SwitchNewWeaponIndex);
                    //Debug.LogWarning("切换武器"+ newWeapon);
                    if (m_SwitchNewWeaponAllowDual)//允许双持
                    {
                        bool leftFree  = !m_fullIk.solver.leftHandEffector.target;
                        bool rightFree = !m_fullIk.solver.rightHandEffector.target;
                        bool leftNeed  = !(newWeapon == oldWeapon || newWeapon == oldSecWeapon)&&(newWeapon && newWeapon.LHand);
                        bool rightNeed = !(newWeapon== oldWeapon|| newWeapon == oldSecWeapon)&&(newWeapon && newWeapon.RHand);
                        //左手需要但是左手被占，或者右手需要，但是右手被占
                        if ((leftNeed&& !leftFree)
                            || (rightNeed && !rightFree)
                            || (newWeapon.IsValid() && newWeapon.Exhausted)
                            || (oldSecWeapon.IsValid() && oldSecWeapon.Exhausted)
                            || (oldWeapon.IsValid() && oldWeapon.Exhausted)
                        )//如果不支持双持
                        {
                            ReplaceWeapons(oldWeapon, newWeapon);
                        }
                        else //允许双持
                        {
                            bool isSec = newWeapon && newWeapon.LHand;
                            DownDualWeapon(isSec ? oldSecWeapon : oldWeapon, isSec ? oldWeapon : oldSecWeapon , newWeapon, isSec);
                        }
                    }
                    else //不允许双持
                    {
                        ReplaceWeapons(oldWeapon, newWeapon);
                        
                    }
                    switchingTimeFactor = 0;
                }
                else if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
                {
                    m_WeaponSwitchState = WeaponSwitchState.Up;
                    m_WeaponMainLocalPosition = DefaultWeaponPosition.localPosition;
                }
            }

            // 处理 移动武器插座位置，以切换动画武器
            if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(DefaultWeaponPosition.localPosition,
                    DownWeaponPosition.localPosition, switchingTimeFactor);
            }
            else if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(DownWeaponPosition.localPosition,
                    DefaultWeaponPosition.localPosition, switchingTimeFactor);
            }
        }
        /// <summary>
        /// 替换武器(并关闭副手武器)
        /// </summary>
        /// <param name="oldWeapon"></param>
        /// <param name="newWeapon"></param>
        void ReplaceWeapons(WeaponPlayerController oldWeapon, WeaponPlayerController newWeapon)
        {
            // 停用旧武器
            if (oldWeapon != null)
            {
                SetWeaponState(oldWeapon,false);
            }
            
            // 停用副手武器
            var secWeapon = GetActiveSecWeapon();
            if (secWeapon != null)
            {
                SetWeaponState(secWeapon, false);
                ActiveSecWeaponIndex = -1;
            }
            ActiveWeaponIndex = m_SwitchNewWeaponIndex;
            
            // 激活新武器
            OnSwitchedToWeapon?.Invoke(newWeapon,false);


            if (newWeapon)
            {
                m_TimeStartedWeaponSwitch = Time.time;
                m_WeaponSwitchState = WeaponSwitchState.PutUpNew;

            }
            else
            {
                //如果新武器是空的，不要坚持把武器放回原处
                m_WeaponSwitchState = WeaponSwitchState.Down;
            }
        }
        /// <summary>
        /// 双持武器
        /// </summary>
        /// <param name="oldWeapon">被替换的武器</param>
        /// <param name="oldOtherWeapon">另一把武器</param>
        /// <param name="newWeapon">新武器</param>
        /// <param name="isSec">是副手</param>
        void DownDualWeapon(WeaponPlayerController oldWeapon, WeaponPlayerController oldOtherWeapon, WeaponPlayerController newWeapon,bool isSec)
        {
            m_TimeStartedWeaponSwitch = Time.time;
            m_WeaponSwitchState = WeaponSwitchState.PutUpNew;

            //Debug.LogWarning("尝试双持武器:是副手"+ isSec+"   被替换的旧武器"+oldWeapon + "  另一把没被替换的武器"+oldOtherWeapon + "  新装备的武器"+newWeapon);
            //副手武器作为主要武器时尝试切换至自己时
            if (isSec&& oldOtherWeapon== newWeapon)
            {
                return;
            }
            bool isDown = oldWeapon == newWeapon;
            //尝试放下主手武器且没有另一把武器
            if (isDown && !isSec&& !oldOtherWeapon.IsValid())
            {
                return;
            }
            // 停用旧武器
            //bug情况:主手的武器是副手用的时，装备新主手武器时，没有把武器挪到副手再装备，而是直接下掉
            //此时副手为空，应该将当前武器改为副手，然后装备主手
            if (!oldOtherWeapon.IsValid())
            {
                //ActiveSecWeaponIndex = ActiveWeaponIndex;
                //ActiveWeaponIndex = m_SwitchNewWeaponIndex;
            }
            else if (oldWeapon != null)
            {
                Debug.LogWarning("停用旧武器:" + oldWeapon.WeaponName);
                SetWeaponState(oldWeapon, false);
            }

            if (isSec)
            {
                //替换副手武器
                ActiveSecWeaponIndex = isDown ? -1: m_SwitchNewWeaponIndex;
                Debug.LogWarning("替换副手武器:" + ActiveSecWeaponIndex);
                if(!isDown) OnSwitchedToWeapon?.Invoke(newWeapon, true);
                else OnSwitchedToWeapon?.Invoke(oldOtherWeapon, false);
            }
            else
            {
                if (isDown)//放下主武器
                {
                    //前面排除过了，肯定有副手武器
                    //另外的武器变成主手，副手置空
                    ActiveWeaponIndex = ActiveSecWeaponIndex;
                    ActiveSecWeaponIndex = -1;
                    //Debug.LogWarning("放下主武器:" + ActiveWeaponIndex);
                    OnSwitchedToWeapon?.Invoke(oldOtherWeapon, false);
                }
                else //替换主武器
                {
                    ActiveSecWeaponIndex = ActiveWeaponIndex;
                    ActiveWeaponIndex = m_SwitchNewWeaponIndex;
                    Debug.LogWarning("替换主武器:" + ActiveWeaponIndex);
                    OnSwitchedToWeapon?.Invoke(newWeapon, false);
                    //不能直接直接用oldOtherWeapon，因为部分情况会是用的oldWeapon
                    OnSwitchedToWeapon?.Invoke(oldOtherWeapon.IsValid() ?oldOtherWeapon :oldWeapon, true) ;
                }

            }
            /*
            if (isSec)
            {
                OnSwitchedToWeapon?.Invoke(oldOtherWeapon, false);
                if(!isDown)OnSwitchedToWeapon?.Invoke(newWeapon, true);
            }
            else
            {
                if (!isDown) OnSwitchedToWeapon?.Invoke(newWeapon, false);
                OnSwitchedToWeapon?.Invoke(oldOtherWeapon, true);
            }
            */


        }
        #endregion

        #region 武器相关

        //同时设置主相机和武器相机的视野
        public void SetFov(float fov)
        {
            m_PlayerCharacterController.PlayerCamera.fieldOfView = fov;
            WeaponCamera.fieldOfView = fov * WeaponFovMultiplier;
        }

        public void SetStatrtWeapon(List<WeaponPlayerController> StartingWeapons)
        {
            m_fullIk = GetComponentInChildren<FullBodyBipedIK>();
            //Debug.LogWarning("重设FullBodyBipedIK:" + m_fullIk);
            //Debug.LogWarning("重置武器:" + StartingWeapons.Count);
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                if (m_WeaponSlots[i] != null)
                {
                    RemoveWeapon(m_WeaponSlots[i]);
                }
            }
            // Add starting weapons
            foreach (var weapon in StartingWeapons)
            {
                AddWeapon(weapon);
            }
            //SwitchWeapon(true);
        }

        /// <summary>
        /// 找到下一个要切换到的有效武器(一般是滚轮使用)
        /// </summary>
        public void SwitchWeapon(bool ascendingOrder)
        {
            int newWeaponIndex = -1;
            int closestSlotDistance = m_WeaponSlots.Length;
            for (int i = 0; i < (GameRoot.GameState == GameStateEnum.Game ? 3 : m_WeaponSlots.Length); ++i)
            {
                //如果此插槽的武器有效，则计算其与活动插槽索引的“距离”（按升序或降序排列）
                //如果距离最近，请选择它
                if (i != ActiveWeaponIndex && GetWeaponAtSlotIndex(i) != null)
                {
                    int distanceToActiveIndex = GetDistanceBetweenWeaponSlots(ActiveWeaponIndex, i, ascendingOrder);
                    if (distanceToActiveIndex < closestSlotDistance)
                    {
                        closestSlotDistance = distanceToActiveIndex;
                        newWeaponIndex = i;
                    }
                }
            }
            //处理切换到新武器索引(不允许双持)
            SwitchToWeaponIndex(newWeaponIndex, force:false, allowDual:false);
        }

        /// <summary>
        /// 切换到武器插槽中的给定武器索引
        /// </summary>
        /// <param name="newWeaponIndex">目标槽位</param>
        /// <param name="force">强制切换</param>
        /// <param name="allowDual">是否允许双持</param>
        /// <param name="instant">瞬间完成</param>
        public void SwitchToWeaponIndex(int newWeaponIndex, bool force = false,bool allowDual=true,bool instant=false)
        {
            //1.强制
            //2.武器和主手的不一样且不为空
            //3.允许双持且 (“武器和主手不一样”或者"武器和主手一样，但是有副手")
            if (force || (newWeaponIndex != ActiveWeaponIndex && newWeaponIndex >= 0)||(allowDual&&GetActiveSecWeapon()))
            {
                if (ActiveWeaponIndex == (int)WeaponTypeEnum.FlareGun && WaitRelease.IsValid()) GlobalEventManager.CancelAirdrop(gameObject,WaitRelease);
                //存储与武器切换动画相关的数据
                m_SwitchNewWeaponIndex = newWeaponIndex;
                m_TimeStartedWeaponSwitch = Time.time;
                m_SwitchNewWeaponAllowDual = allowDual;
                //处理首次切换到有效武器的情况（只需将其挂起，无需先放下任何东西）
                if (GetActiveWeapon() == null)
                {
                    m_WeaponMainLocalPosition = DownWeaponPosition.localPosition;
                    m_WeaponSwitchState = WeaponSwitchState.PutUpNew;
                    ActiveWeaponIndex = newWeaponIndex;
                    WeaponPlayerController newWeapon = GetWeaponAtSlotIndex(newWeaponIndex);

                    OnSwitchedToWeapon?.Invoke(newWeapon,false);

                }else if (instant)
                {
                    SetWeaponState(GetActiveWeapon(), false);
                    ActiveWeaponIndex = newWeaponIndex;
                    OnSwitchedToWeapon?.Invoke(GetWeaponAtSlotIndex(newWeaponIndex), false);

                }
                //否则，请记住，我们正在放下当前的武器，以便切换到下一个
                else
                {
                    m_WeaponSwitchState = WeaponSwitchState.PutDownPrevious;
                    
                }
            }
        }

        /// <summary>
        /// 切换到武器插槽中的给定武器索引
        /// </summary>
        /// <param name="weaponName">武器名称</param>
        /// <param name="force">强制切换</param>
        /// <param name="allowDual">是否允许双持</param>
        /// <param name="instant">瞬间完成</param>
        public void SwitchToWeaponIndex(string weaponName, bool force = false, bool allowDual = true, bool instant = false)
        {

            int index = m_WeaponSlots.FindIndex(item => item.WeaponName == weaponName);
            //Debug.LogError("寻找武器" + weaponName+"结果"+ index);
            if (index>-1)
            {
                SwitchToWeaponIndex(index, force, allowDual, instant);
            }
        }

        public WeaponPlayerController HasWeapon(WeaponPlayerController weaponPrefab)
        {
            //检查我们是否已经有来自指定预制件的武器
            for (var index = 0; index < m_WeaponSlots.Length; index++)
            {
                var w = m_WeaponSlots[index];
                if (w != null && w.WeaponName == weaponPrefab.WeaponName)
                {
                    return w;
                }
            }

            return null;
        }

        /// <summary>
        /// 添加武器
        /// </summary>
        /// <param name="weaponPrefab"></param>
        /// <returns></returns>
        public bool AddWeapon(WeaponPlayerController weaponPrefab)
        {
            //防止重复（应该可以不要把，我们又不能捡）
            if (HasWeapon(weaponPrefab) != null)
            {
                return false;
            }

            //在我们的武器插槽中搜索第一个空闲的，将武器分配给它，如果找到，则返回true。否则返回false
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                // only add the weapon if the slot is free
                if (m_WeaponSlots[i] == null)
                {
                    // 将武器预制件作为武器插座的子对象生成
                    //但是因为切人物时人物隐藏，因此必须先创建，再移动到武器根下
                    WeaponPlayerController weaponInstance = Instantiate(weaponPrefab);
                    weaponInstance.transform.SetParent(WeaponParentSocket);
                    weaponInstance.transform.localPosition = Vector3.zero;
                    weaponInstance.transform.localRotation = Quaternion.identity;
                    //将所有者设置为该游戏对象，以便武器可以相应地更改投射物/伤害逻辑
                    weaponInstance.PlayerIndex = m_PlayerCharacterController.PlayerIndex;
                    weaponInstance.Owner = gameObject;
                    SetWeaponState(weaponInstance, false);

                    //为武器指定第一人称图层
                    int layerIndex =
                        Mathf.RoundToInt(Mathf.Log(FpsWeaponLayer.value,
                            2)); //此函数将层掩码转换为层索引
                    foreach (Transform t in weaponInstance.gameObject.GetComponentsInChildren<Transform>(true))
                    {
                        t.gameObject.layer = layerIndex;
                    }

                    m_WeaponSlots[i] = weaponInstance;
                    
                    OnAddedWeapon?.Invoke(weaponInstance, i);
                    var arch = GameRoot.Archive.GetRoleCfg(m_PlayerCharacterController.Id);
                    var lenghts = weaponInstance.UpgradeCount();
                    var archWeaponData = GameRoot.Archive.weaponUpgradeDic.TryGet(arch.ID + "_" + weaponInstance.WeaponName, new(arch.ID + "_" + weaponInstance.WeaponName, lenghts.Length));
                    weaponInstance.ApplyUpgrade(archWeaponData.selectIndex, archWeaponData.selectModuleIndex);

                    if (GetActiveWeapon() == null)
                    { 
                        //如果当前没有武器，则自动切换到这个武器
                        SwitchToWeaponIndex(i,true,false);
                    }
                    return true;
                }
            }

            return false;
        }

        public bool RemoveWeapon(WeaponPlayerController weaponInstance)
        {
            // Look through our slots for that weapon
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                // when weapon found, remove it
                if (m_WeaponSlots[i] == weaponInstance)
                {
                    m_WeaponSlots[i] = null;

                    if (OnRemovedWeapon != null)
                    {
                        OnRemovedWeapon.Invoke(weaponInstance, i);
                    }

                    Tool.Destroy(weaponInstance.gameObject);

                    // Handle case of removing active weapon (switch to next weapon)
                    if (i == ActiveWeaponIndex)
                    {
                        SwitchWeapon(true);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 返回总的剩余弹药比例
        /// </summary>
        /// <returns></returns>
        public float TotalRemainAmmoRatio()
        {
            float count = 0;
            float re =0;
            for (int i = 0; i < m_WeaponSlots.Length; ++i)
            {
                if (m_WeaponSlots[i]&&!string.IsNullOrEmpty(m_WeaponSlots[i].WeaponName))
                {
                    ++count;
                    re += m_WeaponSlots[i].CurrentTotalAmmoRatio.RawFloat;
                }
            }
            return re/ count;
        }


        /// <summary>
        /// 使用补给
        /// </summary>
        public void UseSupply()
        {
            for (int i = 0; i < m_WeaponSlots.Length; ++i)
            {
                if (m_WeaponSlots[i])
                {
                    m_WeaponSlots[i].UseSupply();
                }
            }
        }


        /// <summary>
        /// 获得当前使用的武器
        /// </summary>
        /// <returns></returns>
        public WeaponPlayerController GetActiveWeapon()
        {
            return GetWeaponAtSlotIndex(ActiveWeaponIndex);
        }

        /// <summary>
        /// 获得当前使用的副武器
        /// </summary>
        /// <returns></returns>
        public WeaponPlayerController GetActiveSecWeapon()
        {
            return GetWeaponAtSlotIndex(ActiveSecWeaponIndex);
        }

        /// <summary>
        /// 获得第X个槽位的武器
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public WeaponPlayerController GetWeaponAtSlotIndex(int index)
        {
            if (index >= 0 &&
                index < m_WeaponSlots.Length)
            {
                return m_WeaponSlots[index];
            }
            return null;
        }

        //计算两个武器槽索引之间的“距离”
        //例如：如果我们有5个武器插槽，插槽#2和#4之间的距离按升序排列为2，按降序排列为3
        int GetDistanceBetweenWeaponSlots(int fromSlotIndex, int toSlotIndex, bool ascendingOrder)
        {
            int distanceBetweenSlots = 0;

            if (ascendingOrder)
            {
                distanceBetweenSlots = toSlotIndex - fromSlotIndex;
            }
            else
            {
                distanceBetweenSlots = -1 * (toSlotIndex - fromSlotIndex);
            }

            if (distanceBetweenSlots < 0)
            {
                distanceBetweenSlots = m_WeaponSlots.Length + distanceBetweenSlots;
            }

            return distanceBetweenSlots;
        }
        #endregion

        #region 事件
        void OnWeaponSwitched(WeaponPlayerController newWeapon,bool isSec=false)
        {
            if (newWeapon != null)
            {
                //Debug.LogWarning("切换到新武器"+newWeapon);
                SetWeaponState(newWeapon, true);

                m_fullIk.solver.leftHandEffector.target = newWeapon.LHand;
                m_fullIk.solver.rightHandEffector.target = newWeapon.RHand;
                m_fullIk.solver.leftHandEffector.positionWeight = newWeapon.LHand ? 1 : 0;
                m_fullIk.solver.rightHandEffector.positionWeight = newWeapon.RHand ? 1 : 0;
                m_fullIk.solver.leftHandEffector.rotationWeight = newWeapon.LHand ? 1 : 0;
                m_fullIk.solver.rightHandEffector.rotationWeight = newWeapon.RHand ? 1 : 0;

                //newWeapon.LHand.parent = transform;
                //newWeapon.RHand.parent = transform;
            }
        }

        void SetWeaponState(WeaponPlayerController weapon,bool state)
        {
            weapon.ShowWeapon(state);
            if (state)
            {
                weapon.OnShoot += _OnShoot;
                weapon.OnWantShootChange += OnWantShootChange;
            }
            else
            {
                weapon.OnShoot -= _OnShoot;
                weapon.OnWantShootChange -= OnWantShootChange;
            }
            
        }

        private void OnWantShootChange(WeaponBaseController weapon, bool state)
        {
            if ( weapon.AttrFinal(WeaponAttrType.MoveSpeedToShoot, 1) != 1)
            {
                m_PlayerCharacterController.MoveSpeedScale += (state ? -1 : 1) * (1 - weapon.AttrFinal(WeaponAttrType.MoveSpeedToShoot, 1)).RawFloat;
            }
        }

        void OnAiming(bool state)
        {
            var weapon = GetActiveWeapon();
            if(weapon.ScopeGo) weapon.ScopeGo.SetActive(state);
        }

        private int m_LastWeaponIndex;
        void OnAirdrop(WindowStateEnum oldSstate, WindowStateEnum newState)
        {
            if(newState== WindowStateEnum.Airdrop)
            {
                //Debug.LogError("记录上一次武器为"+ ActiveWeaponIndex);
                m_LastWeaponIndex = ActiveWeaponIndex;
                SwitchToWeaponIndex((int)WeaponTypeEnum.FlareGun, false,false,false);
            }
            else if(oldSstate == WindowStateEnum.Airdrop&& AirdropController.WaitRelease==null)
            {
                //Debug.LogError("切换会原武器" + m_LastWeaponIndex);
                SwitchToWeaponIndex((m_LastWeaponIndex == (int)WeaponTypeEnum.FlareGun)?0: m_LastWeaponIndex, false, false, false);
            }
        }
        void OnInputCompletedAirdrop(GameObject go, AirdropData data) {
            if (go != gameObject) return;
            var weapon = GetWeaponAtSlotIndex((int)WeaponTypeEnum.FlareGun);
            weapon.UseDamageIndex = 1;
        }
        void OnCancelAirdrop(GameObject go,AirdropData data)
        {
            if (go != gameObject) return;
            var weapon = GetWeaponAtSlotIndex((int)WeaponTypeEnum.FlareGun);
            weapon.UseDamageIndex = 0;
        }
        public void OnAirdrop(GameObject owner, GameObject target, Vector3 point, AirdropData data)
        {
            if (owner==gameObject)
            {
                var weapon = GetWeaponAtSlotIndex((int)WeaponTypeEnum.FlareGun);
                weapon.UseDamageIndex = 0;
                SwitchToWeaponIndex((m_LastWeaponIndex == (int)WeaponTypeEnum.FlareGun) ? 0 : m_LastWeaponIndex, false, false, false);
            }
        }


        void OnOperation(GameObject user, Furniture_Base furn)
        {
            bool switchState = furn.HaveFlag(FurnitureFlag.SwitchState);
            if (user != gameObject || !switchState) return;

            if (furn.inOperate)
            {
                m_LastWeaponIndex = ActiveWeaponIndex;
                SwitchToWeaponIndex((int)WeaponTypeEnum.FlareGun+1, false, false, false);
            }
            else
            {
                SwitchToWeaponIndex((m_LastWeaponIndex >= (int)WeaponTypeEnum.FlareGun) ? 0 : m_LastWeaponIndex, false, false, false);
            }
        }



        void _OnShoot(WeaponBaseController weapon) {
            OnShoot?.Invoke(weapon as WeaponPlayerController);
            if (GameRoot.GameState == GameStateEnum.Game && !string.IsNullOrEmpty(weapon.name) && weapon.name!="信号枪") BattleManager.Instance.AddBattleDataItem(m_PlayerCharacterController.PlayerIndex, "开火次数");
        }
        #endregion
    }
}