using System;
using FPSGame.Attributes;
// 原有一行 `using FPSGame.UI;` 是废弃 using（未用 DynamicBar），且 FPSGame.UI 属 04_UI 程序集
// ⇒ 会构成"玩法层 → UI 层"的反向依赖，已删：
using TMPro;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.Events;
using FPSGame.GameContract;
using static FPSGame.WndTools.WndRootTool;
using FPSGame.AI;
using FPSGame.Weapon;

namespace FPSGame.Gameplay
{


/// <summary>
/// 载具武器槽位管理与对应 UI 驱动。
/// </summary>
[AddComponentMenu("玩家/载具武器管理")]
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
    GameObject _owner;


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
        weapon.Owner = _owner;
        OnIconChange?.Invoke(newArray.Length == 1,icon);
        HookWeaponShoot(weapon, true);   // ★ 联机：新武器也要进"开火上报"的订阅（重建后旧的已被销毁）

    }
    public bool HaveWeapon()
    {
        return m_WeaponSlots.Length>0;
    }

    /// <summary>
    /// 清空全部武器槽（销毁挂上来的武器实例）。
    /// <para>▍用途：联机时载具要按**驾驶者的配置**重装一次武器（见 <c>BattleApplyVehicleData.ApplyForOwner</c>），
    /// 没有它就会在原武器上再 `AddWeapon` 一份 ⇒ 双份武器。</para>
    /// </summary>
    public void ClearWeapons()
    {
        for (int i = 0; i < m_WeaponSlots.Length; i++)
        {
            var w = m_WeaponSlots[i];
            if (w == null) continue;
            SetWeaponStateInternal(w, false);   // 退订 OnWantShootChange
            w.Owner = null;
            Destroy(w.gameObject);
        }
        m_WeaponSlots = new WeaponController[0];
    }

    /// <summary>【联机】读某槽位武器**当前弹量**（<paramref name="magazine"/> = 弹匣、<paramref name="ammo"/> = 后备）；
    /// 该槽位没武器 ⇒ false；某一项为无限弹（<c>FinalValue &lt; 0</c>）⇒ 该项返回 <c>-1</c>（收端就不动它）。
    ///
    /// <para>▍为什么要它：载具武器是**每端各一份实例**（<c>BattleApplyVehicleData</c> 按持有者配置重建），
    /// 弹量天然会分叉 —— 驾驶者那台打了 10 发，另一端的副本还是满的（实测：玩家 a 打完下车、玩家 b 上车看到满弹）。
    /// 口径 = **驾驶者那台才是权威**，每次开火随 4054 把弹量推给其余端（见 <c>NetVehicleBridge</c>）。</para>
    /// </summary>
    public bool TryGetAmmo(int index, out int magazine, out int ammo)
    {
        magazine = -1;
        ammo = -1;

        var w = GetWeaponAtSlotIndex(index) as WeaponReloadController;
        if (w == null) return false;

        if (!w.InfiniteMagazine) magazine = w.Magazine.CurrValue.RawInt;
        if (!w.InfiniteAmmo) ammo = w.Ammo.CurrValue.RawInt;
        return true;
    }

    /// <summary>【联机】某武器"**这一发打完之后**"的弹量（折算口径与 <c>WeaponReloadController.UseMagazine</c> 一致）。
    ///
    /// <para>▍为什么不能直接用 <see cref="TryGetAmmo"/>：开火上报挂的是 <c>OnShoot</c>，它是在
    /// <c>HandleShoot()</c> 里抛的，而 <c>UseMagazine(ShootCost)</c> 在它**之后**才执行 ⇒ 回调里读到的还是
    /// "扣之前"的值，直接上报会**永远差一发**（本机打空下车、别人上车却看到还剩 1 发）。</para>
    /// </summary>
    public bool TryGetAmmoAfterShot(int index, out int magazine, out int ammo)
    {
        magazine = -1;
        ammo = -1;

        var w = GetWeaponAtSlotIndex(index) as WeaponReloadController;
        if (w == null) return false;

        if (w.InfiniteMagazine)
        {
            if (!w.InfiniteAmmo) ammo = w.Ammo.CurrValue.RawInt;
            return true;
        }

        // 与 WeaponReloadController.UseMagazine 同款：弹匣不够的部分从后备弹补
        int cost = w.AmmoPerShot;
        int mag = w.Magazine.CurrValue.RawInt;
        int spare = w.Ammo.CurrValue.RawInt;

        if (cost > mag)
        {
            spare -= cost - mag;
            mag = 0;
            if (spare < 0) spare = 0;
        }
        else
        {
            mag -= cost;
        }

        magazine = mag;
        if (!w.InfiniteAmmo) ammo = spare;
        return true;
    }

    /// <summary>【联机】把驾驶者那台下来的**权威弹量**写进本端这把武器（见 <see cref="TryGetAmmo"/>）。
    /// <para>▍<c>&lt;0</c> = 该项未带/无限 ⇒ 不动；<c>GameCurrentAttribute</c> 的 setter 自带上下限约束。</para>
    /// <para>⚠ 本端**没有**驾驶者时 UI 不刷（<see cref="Update"/> 早退），所以这里不必手动刷 UI：
    /// 等本地玩家真上车时，UI 读的就是这份被同步过来的值。</para></summary>
    public void ApplyRemoteAmmo(int index, int magazine, int ammo)
    {
        var w = GetWeaponAtSlotIndex(index) as WeaponReloadController;
        if (w == null) return;

        if (magazine >= 0 && !w.InfiniteMagazine) w.Magazine.CurrValue = magazine;
        if (ammo >= 0 && !w.InfiniteAmmo) w.Ammo.CurrValue = ammo;
    }

    /// <summary>某把武器在槽位表里的下标（联机上报要用它；取不到 ⇒ <c>-1</c>）。
    /// <para>⚠ 必须按**引用**比：这些武器是从预制体实例化出来的子物体，靠名字/类型都可能撞。</para></summary>
    public int IndexOfWeapon(WeaponBaseController weapon)
    {
        if (weapon == null) return -1;
        for (int i = 0; i < m_WeaponSlots.Length; ++i)
        {
            if (ReferenceEquals(m_WeaponSlots[i], weapon)) return i;
        }
        return -1;
    }

    /// <summary>【联机 · 载具开火表现】本载具**任意槽位武器开了一次火**
    /// （每"次"射击一次；多弹丸/齐射也只一次，见 <c>WeaponBaseController.OnShoot</c>）。
    ///
    /// <para>▍为什么挂在这里、而不是让 09 的桥逐个订阅武器：武器会被 <see cref="ClearWeapons"/> +
    /// <c>AddWeapon</c> **整批重建**（按驾驶者配置重装，见 <c>BattleApplyVehicleData</c>）⇒ 订阅点必须跟着
    /// **武器生命周期**走，而只有本类知道它们何时生灭。桥因此只需订阅本事件一次。</para>
    ///
    /// <para>▍参数 = 开火的那把武器（桥用它取槽位下标 / 枪口 / 射击方向 / 伤害档位 / 发射点下标）。</para>
    /// </summary>
    public event UnityAction<WeaponBaseController> OnWeaponShoot;

    /// <summary>订阅 / 退订某把武器的开火（<b>幂等</b>：先 <c>-=</c> 再 <c>+=</c> ——
    /// <c>AddWeapon</c> 与 <c>Start</c> 里的 <c>SetWeaponStateInternal</c> 可能都对同一把调）。</summary>
    void HookWeaponShoot(WeaponController weapon, bool on)
    {
        if (weapon == null) return;

        weapon.OnShoot -= RelayWeaponShoot;
        if (on) weapon.OnShoot += RelayWeaponShoot;
    }

    void RelayWeaponShoot(WeaponBaseController weapon) => OnWeaponShoot?.Invoke(weapon);

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

        // ★ 联机：开火表现的订阅点必须跟着**武器生灭**走（武器会被 ClearWeapons + AddWeapon 整批重建）
        HookWeaponShoot(weapon, state);
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
        _owner = state ? m_Controller.CurrentOwner : null;

        // 同步武器所有者
        for (int i = 0; i < m_WeaponSlots.Length; i++)
        {
            if (m_WeaponSlots[i])
            {
                m_WeaponSlots[i].Owner = _owner;
            }
        }

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
}
