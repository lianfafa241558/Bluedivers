using System;
using FPSGame.Attribute;
using FPSGame.UI;
using TMPro;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using static WndTools.WndRootTool;


public class VehicleWeaponsManager : MonoBehaviour, IVehicleUIController
{
    
    [Foldout("点位", true)]
    [InspectorName("未瞄准时的位置")]
    public Transform DefaultWeaponPosition;
    [InspectorName("瞄准时的位置")]
    public Transform AimingWeaponPosition;

    [SerializeField]
    [InspectorName("目标点")]
    private Transform targetPoint;
    [InspectorName("目标起始点偏移")]
    [SerializeField]
    private float targetStartOffest = 5;

    [Foldout("摆动", true)]
    [InspectorName("移动时武器在屏幕上移动的频率")]
    public float BobFrequency = 10f;

    [InspectorName("武器摆锤的速度")]
    public float BobSharpness = 10f;

    [InspectorName("不瞄准时武器摆动的距离")]
    public float DefaultBobAmount = 0.05f;

    [InspectorName("瞄准时武器摆动的距离")]
    public float AimingBobAmount = 0.02f;

    [Foldout("后坐力", true)]
    [InspectorName("后坐力")]
    [Range(0f, 2f)]
    public float RecoilForce = 1;

    [InspectorName("这将影响后坐力移动武器的速度，值越大，速度越快")]
    public float RecoilSharpness = 50f;

    [InspectorName("后坐力可以影响武器的最大距离")]
    public float MaxRecoilDistance = 0.5f;

    [InspectorName("反冲结束后，武器返回原始位置的速度有多快")]
    public float RecoilRestitutionSharpness = 10f;

    [Foldout("其他", true)]
    [InspectorName("播放瞄准动画的速度")]
    public float AimingAnimationSpeed = 10f;

    [InspectorName("不瞄准时的视角")]
    public float DefaultFov = 60f;

    [InspectorName("瞄准时放大倍率")]
    [Range(0f, 1f)]
    public float AimZoomRatio = 1f;


    private bool m_isAiming;
    public bool IsAiming
    {
        get => m_isAiming;
        private set
        {
            if (IsAiming != value && OnAim != null)
            {
                OnAim.Invoke(value);
            }
            m_isAiming = value;
        }
    }

    public event UnityAction<bool> OnAim;

    // IVehicleUIController
    public UnityAction<bool, bool> SetWeaponState { get; set; }
    public UnityAction<bool> OnStateChange { get; set; }
    public UnityAction<bool, Color> OnColorChange { get; set; }
    public UnityAction<bool, float> OnFillChange { get; set; }
    public UnityAction<bool, string> OnTextChange { get; set; }
    public UnityAction<bool, Sprite> OnIconChange { get; set; }
    //

    [SerializeField]
    WeaponController[] m_WeaponSlots;
    PlayerInputHandler m_InputHandler;
    I_AIController m_AIController;
    IDrivable m_Controller;


    Vector3 m_WeaponBobLocalPosition;//武器摆动坐标
    Vector3 m_WeaponRecoilLocalPosition;//武器后坐力坐标
    Vector3 m_AccumulatedRecoil;

    //Vector3 m_WeaponMainLocalPosition;//武器偏移
   

    float closeAimDelay;

    private void Awake()
    {
        m_InputHandler = GetComponent<PlayerInputHandler>();
        m_AIController = GetComponent<I_AIController>();
        m_Controller = GetComponent<IDrivable>();
        m_Controller.OnSetOwner += OnSetOwner;

        enabled = false;
    }

    void Start()
    {
        var main = GetActiveWeapon();
        var sec = GetActiveSecWeapon();
        SetWeaponState?.Invoke(true, main != null);
        SetWeaponState?.Invoke(false, sec != null);
        for (int i = 0; i < m_WeaponSlots.Length; i++)
        {
            if (m_WeaponSlots[i])
            {
                SetWeaponStateInternal(m_WeaponSlots[i], true);
            }
        }

        SetFov(DefaultFov);
        m_Controller.ResetCameraBasePoint(DefaultWeaponPosition.position);
    }



    private void OnDestroy()
    {
        m_Controller.OnSetOwner -= OnSetOwner;
    }

    void Update()
    {

        WeaponController activeWeapon = GetActiveWeapon();
        WeaponController activeSecWeapon = GetActiveSecWeapon();
        bool lastIsAiming = IsAiming;

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
        if (activeWeapon != null && !activeWeapon.IsReloading)
        {
            IsAiming = !activeSecWeapon && AimZoomRatio < 1 && m_InputHandler.GetAimInputHeld();
        }
        UpdateWeapon(activeWeapon, activeSecWeapon);

        /*
        foreach(var weapon in m_WeaponSlots)
        {
            var targetPoint = m_Controller.PlayerCamera.transform.TransformPoint(0, 0, 150);
            var dir = targetPoint - weapon.WeaponRoot.transform.position;
            var targetRotation = Quaternion.LookRotation(dir.normalized, weapon.transform.right);
            weapon.WeaponRoot.transform.rotation = targetRotation;
        }*/
        if (lastIsAiming != IsAiming)
        {
            m_Controller.ResetCameraBasePoint((IsAiming? AimingWeaponPosition:DefaultWeaponPosition).position);
        }
    }


    private void UpdateWeapon(WeaponController main, WeaponController sec)
    {
        int hasFired = 0;
        //没有自动换弹且按下键且弹匣不满
        if (m_InputHandler.GetReloadDown())
        {
            if (!main.IsReloading&&!main.HasFlag(WeaponFlag.AutomaticReload) && main.Magazine.ScaleValue < 1) main.TryManualReload();
            if(sec && !sec.IsReloading&&!sec.HasFlag(WeaponFlag.AutomaticReload) && sec.Magazine.ScaleValue < 1) sec.TryManualReload();

        }
        else if(HaveWeapon())//有武器时才处理射击输入
        {
            if (!sec)//单持武器时,正常左键射击
            {
                hasFired += !main.IsReloading && main.HandleShootInputs(
                    m_InputHandler.GetFireInputDown(),
                    m_InputHandler.GetFireInputHeld(),
                    m_InputHandler.GetFireInputReleased()) ? 1 : 0;
                if(hasFired>0) m_AIController?.OnAttack?.Invoke(main);
            }
            else //双持武器时，反向控制
            {
                hasFired += !main.IsReloading && main.HandleShootInputs(
                    m_InputHandler.GetAimInputDown(),
                    m_InputHandler.GetAimInputHeld(),
                    m_InputHandler.GetAimInputReleased()) ? 1 : 0;
                if (hasFired ==1) m_AIController?.OnAttack?.Invoke(main);
                hasFired += !sec.IsReloading && sec.HandleShootInputs(
                    m_InputHandler.GetFireInputDown(),
                    m_InputHandler.GetFireInputHeld(),
                    m_InputHandler.GetFireInputReleased()) ? 1 : 0;
                if (hasFired == 2) m_AIController?.OnAttack?.Invoke(main);
            }
        }

        // 后坐力
        if (hasFired>0)
        {
            m_AccumulatedRecoil += Vector3.back * RecoilForce * hasFired;
            m_AccumulatedRecoil = Vector3.ClampMagnitude(m_AccumulatedRecoil, MaxRecoilDistance);
        }
        if (HaveWeapon())
        {
            OnFillChange?.Invoke(true, main.Magazine.ScaleValue.RawFloat);
            OnTextChange?.Invoke(true, main.Magazine.CurrValue.RawInt + "/" + main.TotalAmmo);
            if (sec)
            {
                OnFillChange?.Invoke(false, sec.Magazine.ScaleValue.RawFloat);
                OnTextChange?.Invoke(false, sec.Magazine.CurrValue.RawInt + "/" + sec.TotalAmmo);
            }
        }
    }

    private void LateUpdate()
    {

        var trans = m_Controller.PlayerCamera.transform;
        if (Physics.Raycast(trans.TransformPoint(new(0, 0, targetStartOffest)), trans.forward, out var hit, 150, 1 << 0 | 1 << 3))
        {
            targetPoint.position = hit.point;
        }
        else
        {
            targetPoint.position = trans.transform.TransformPoint(0, 0, 150);
        }

    }


    #region 手臂/身体位置
    /*
    void LateUpdate()
    {
        //UpdateWeaponAiming();
        UpdateWeaponRecoil();
    }
    
    void UpdateWeaponAiming()
    {
        WeaponController activeWeapon = GetActiveWeapon();
        if (IsAiming && activeWeapon)
        {
            m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition,
                AimingWeaponPosition.localPosition,
                AimingAnimationSpeed * Time.deltaTime);
            SetFov(Mathf.Lerp(m_Controller.PlayerCamera.fieldOfView,
                AimZoomRatio * DefaultFov, AimingAnimationSpeed * Time.deltaTime));
        }
        else
        {
            m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition,
                DefaultWeaponPosition.localPosition, AimingAnimationSpeed * Time.deltaTime);
            SetFov(Mathf.Lerp(m_Controller.PlayerCamera.fieldOfView, DefaultFov,
                AimingAnimationSpeed * Time.deltaTime));
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
    */
    #endregion

    #region 武器相关

    //同时设置主相机和武器相机的视角
    public void SetFov(float fov)
    {
        m_Controller.PlayerCamera.fieldOfView = fov;
    }


    /// <summary>
    /// 获得主武器
    /// </summary>
    /// <returns></returns>
    public WeaponController GetActiveWeapon()
    {
        return GetWeaponAtSlotIndex(0);
    }

    /// <summary>
    /// 获得副武器
    /// </summary>
    /// <returns></returns>
    public WeaponController GetActiveSecWeapon()
    {
        return GetWeaponAtSlotIndex(1);
    }

    /// <summary>
    /// 获得第X个槽位的武器
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public WeaponController GetWeaponAtSlotIndex(int index)
    {
        if (index >= 0 &&
            index < m_WeaponSlots.Length)
        {
            return m_WeaponSlots[index];
        }
        return null;
    }
    public void AddWeapon(WeaponController weapon, Sprite icon)
    {
        var newArray = new WeaponController[m_WeaponSlots.Length + 1];
        Array.Copy(m_WeaponSlots, newArray, m_WeaponSlots.Length);
        newArray[newArray.Length - 1] = weapon;
        m_WeaponSlots = newArray;
        OnIconChange?.Invoke(newArray.Length == 1,icon);

    }
    public bool HaveWeapon()
    {
        return m_WeaponSlots.Length>0;
    }

    #endregion

    #region 事件
    
    void SetWeaponStateInternal(WeaponController weapon, bool state)
    {
        weapon.ShowWeapon(state);
        if (state)
        {
            weapon.OnWantShootChange += OnWantShootChange;
        }
        else
        {
            weapon.OnWantShootChange -= OnWantShootChange;
        }

    }

    private void OnWantShootChange(WeaponBaseController weapon, bool state)
    {
        if (weapon.AttrFinal(WeaponAttrType.MoveSpeedToShoot, 1) != 1)
        {
            m_Controller.MoveSpeedScale += (state ? -1 : 1) * (1 - weapon.AttrFinal(WeaponAttrType.MoveSpeedToShoot, 1)).RawFloat;
        }
    }

    void OnSetOwner(bool state)
    {
        enabled = state;

        if (!HaveWeapon())
        {
            OnStateChange?.Invoke(false);
        }
        else
        {
            OnStateChange?.Invoke(state);
        }
    }

    #endregion
}
