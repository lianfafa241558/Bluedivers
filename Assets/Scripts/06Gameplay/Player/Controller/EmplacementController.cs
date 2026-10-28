using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Attributes;
using FPSGame.GameContract;

using UnityEngine;
using UnityEngine.Events;
using FPSGame.Gameplay;

namespace FPSGame.Gameplay
{
/// <summary>
/// 炮台控制器
/// </summary>
[AddComponentMenu("玩家/炮台控制器")]
public class EmplacementController : BaseSelfController, IDrivable
{
    public event UnityAction<bool> OnSetOwner;

    [Foldout("一般", true)]
    //[SerializeField]
    //private Transform CameraPoint;


    //[SerializeField]
    private GameObject owner;
    GameObject IDrivable.CurrentOwner => owner;
    private Camera PlayerCamera;
    private bool _wasThirdPersonOnEnter;

    Camera IDrivable.PlayerCamera => PlayerCamera;
    Vector3 CameraBasePoint { get; set; }

    public float MoveSpeedScale { get; set; }
    protected Vector3 RecordCameraBasePoint { get; set; }

    /// <summary>驾驶者**自己**的生命组件（上车时订阅它的 <c>OnDie</c> ⇒ 驾驶者死亡也要强制下车）。</summary>
    private IHealth _ownerHealth;

    /// <summary>本次退接管是不是"**驾驶者死亡**"引发的（死亡时不能走整套还原，见 <see cref="OnDriverDie"/>）。</summary>
    private bool _ownerDead;

    public bool TryExit() => InputHandler.GetOperateDown();
    public void SetOwener(GameObject owner)
    {

        if (owner)
        {
            var comp = owner.GetComponent<PlayerController>();
            PlayerCamera = comp.PlayerCamera;
            _wasThirdPersonOnEnter = comp.IsThirdPerson;

            // 阻止 PlayerController.OnDisable 在第三人称下关闭相机
            comp.SkipCameraDeactivateOnDisable = true;
            comp.enabled = false;
            comp.SkipCameraDeactivateOnDisable = false;

            comp.Controller.enabled = false;
            owner.GetComponent<IActor>().ActorState = ActorState.Hide;
            owner.GetComponent<PlayerWeaponsManager>().WeaponCamera.enabled = false;
            RecordCameraBasePoint = PlayerCamera.transform.localPosition;
            //CameraBasePoint = transform.InverseTransformPoint(CameraPoint.position);
            Health.OnDie += OnDie;

            // ★【驾驶者死亡 ⇒ 强制下车】与 VehicleController 同款：原来只订阅"炮台自己"的 Health.OnDie
            //   ⇒ 驾驶者本人被打死时会卡在炮位上（还能转炮/开火）。
            _ownerHealth = owner.GetComponent<IHealth>();
            if (_ownerHealth != null)
            {
                _ownerHealth.OnDie -= OnDriverDie;   // 幂等：反复上车 / 上次没退干净时不会双挂
                _ownerHealth.OnDie += OnDriverDie;
            }
            else
            {
                FPSGame.Utils.NetSyncLog.Warn("载具/上机·发",
                    $"{owner.name} 上没有 IHealth ⇒ 他死亡时**不会**被强制下车（可以卡在炮位上继续开）");
            }
        }
        else
        {
            PlayerCamera = null;
            if (this.owner)
            {
                var comp = this.owner.GetComponent<PlayerController>();
                comp.enabled = true;
                comp.Controller.enabled = true;
                this.owner.transform.parent = null;
                comp.PlayerCamera.transform.localPosition = RecordCameraBasePoint;

                // ⚠ 只在"他还被我们藏着"时还原（Hide = 上车那一刻设的）：驾驶者死亡时状态可能已是 Dead
                //   ⇒ 绝不能刷回 Normal（那会把他从倒地/死亡里拽出来）。也天然覆盖"炮台被打爆"那支。
                var ownerActor = this.owner.GetComponent<IActor>();
                if (ownerActor != null && ownerActor.ActorState == ActorState.Hide)
                {
                    ownerActor.ActorState = ActorState.Normal;
                }

                // 第三人称下武器相机应保持关闭；⚠ 驾驶者**死亡**时也不开（死亡镜头会叠出武器画面）
                if (!_wasThirdPersonOnEnter && !_ownerDead)
                {
                    this.owner.GetComponent<PlayerWeaponsManager>().WeaponCamera.enabled = true;
                }
            }
            Health.OnDie -= OnDie;
            if (_ownerHealth != null)
            {
                _ownerHealth.OnDie -= OnDriverDie;
                _ownerHealth = null;
            }
        }

        this.owner = owner;
        OnSetOwner?.Invoke(owner);

        // ★【联机】上下机的**唯一收口**就是这里 —— 与 <c>VehicleController.SetOwener</c> 完全同款：
        //   抛给 09 的 NetVehicleBridge 去上报 4053（上下机）、并让 4054（开火表现）跟着绑定。
        //   ⚠ 原来漏了这一句 ⇒ 炮位上机**完全不同步**（客机上骑手站在地上、炮台也不转）——2026-10-14 修。
        BattleEventBus.VehicleOwnerChanged(gameObject, owner);
    }

    void OnDie(GameObject caused, GameObject origin)
    {
        SetOwener(null);

    }

    /// <summary>
    /// 【驾驶者死亡 ⇒ 强制下车】只撤接管，**不**把人还原成活人（口径同 <c>VehicleController.OnDriverDie</c>）。
    /// <para>▍为什么必须走 <see cref="SetOwener"/>：它是上下机的唯一收口，抛的 <c>VehicleOwnerChanged</c>
    /// 是 09 的桥上报 4053 的唯一来源 ⇒ 远端才会把骑手摘掉。自己散着做几件事会留下"死人还坐在炮位上"的幽灵。</para>
    /// </summary>
    void OnDriverDie(GameObject caused, GameObject origin)
    {
        FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·发", $"驾驶者死亡 ⇒ 强制下车（炮台={name}）");
        _ownerDead = true;
        try { SetOwener(null); }
        finally { _ownerDead = false; }
    }

    protected void LateUpdate()
    {
        if (!owner) return;
        if (!PlayerCamera) return;

        // 防御：第三人称下相机若被意外关闭则重新激活
        if (!PlayerCamera.gameObject.activeSelf)
        {
            PlayerCamera.gameObject.SetActive(true);
        }

        var target = transform.TransformPoint(CameraBasePoint);
        float dis = Vector3.Distance(PlayerCamera.transform.position, target);
        if (dis > 0.1f)
        {
            PlayerCamera.transform.position = Vector3.Lerp(PlayerCamera.transform.position,
            target, Time.deltaTime*5);
        }
        
        PlayerCamera.transform.localEulerAngles = new Vector3(CameraPitch + ShakePitch, RecoilYaw + ShakeYaw, CameraRoll);



    }
    void IDrivable.ResetCameraBasePoint(Vector3 vector)
    {
        CameraBasePoint = transform.InverseTransformPoint(vector);
    }


    protected override void HandleRotation()
    {
        if (!owner) return;
        base.HandleRotation();
    }


}
}
