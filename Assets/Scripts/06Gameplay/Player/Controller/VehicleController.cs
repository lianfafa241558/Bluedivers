using FPSGame.Core;
using FPSGame.Attributes;
using FPSGame.GameContract;
using RootMotion.FinalIK;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.Events;
using FPSGame.Gameplay;

namespace FPSGame.Gameplay
{

public interface IDrivable
{
    public void SetOwener(GameObject owner);
    public bool TryExit();
    public event UnityAction<bool> OnSetOwner;

    public float MoveSpeedScale { get; set; }

    public Camera PlayerCamera { get; }

    /// <summary>当前驾驶员（null 表示无人驾驶）</summary>
    public GameObject CurrentOwner { get; }

    /// <summary>
    /// 重设相机基准点,给载具使用
    /// </summary>
    public void ResetCameraBasePoint(Vector3 vector);

}
/// <summary>
/// 载具控制器
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler), typeof(AudioSource))]
[AddComponentMenu("玩家/载具控制器")]
public class VehicleController : BaseSelfMoveableController,IDrivable
{
    public event UnityAction<bool> OnSetOwner;

    //[SerializeField]
    private GameObject _owner;
    GameObject IDrivable.CurrentOwner => _owner;
    private bool _wasThirdPersonOnEnter;

    /// <summary>驾驶者**自己**的生命组件（上车时订阅它的 <c>OnDie</c> ⇒ 驾驶者死亡也要强制下车）。
    /// <para>▍存成字段才能可靠退订：<c>_owner</c> 在死亡/销毁后可能已经取不到组件了。</para></summary>
    private IHealth _ownerHealth;

    /// <summary>本次 <see cref="SetOwener"/>（退的那一支）是不是"**驾驶者死亡**"引发的。
    /// <para>▍用途：死亡下车**不能**走整套还原（见 <see cref="OnDriverDie"/>）。</para></summary>
    private bool _ownerDead;

    Camera IDrivable.PlayerCamera => PlayerCamera;

    protected Vector3 RecordCameraBasePoint { get; set; }

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
            if (!_wasThirdPersonOnEnter)
            {
                comp.GetComponentInChildren<LookAtController>().enabled = false;
            }


            RecordCameraBasePoint = PlayerCamera.transform.localPosition;

            Health.OnDie += OnDie;

            // ★【驾驶者死亡 ⇒ 强制下车】原来这里**只**订阅了"车自己"的 Health.OnDie ⇒
            //   驾驶者本人被打死时没人管，人就**一直挂在车上、还能接着开**（2026-10-14 实测）。
            //   与上面那条同口径：都走唯一收口 SetOwener(null) ⇒ 09 的桥据此自动上报"下机"，远端把骑手摘掉。
            _ownerHealth = owner.GetComponent<IHealth>();
            if (_ownerHealth != null)
            {
                _ownerHealth.OnDie += OnDriverDie;
            }
            else
            {
                FPSGame.Utils.NetSyncLog.Warn("载具/上机·发",
                    $"{owner.name} 上没有 IHealth ⇒ 他死亡时**不会**被强制下车（可以接着开）");
            }
        }
        else
        {
            PlayerCamera = null;

            if (_owner)
            {
                var comp = _owner.GetComponent<PlayerController>();
                comp.enabled = true;
                comp.Controller.enabled = true;
                _owner.transform.parent = null;
                comp.PlayerCamera.transform.localPosition = RecordCameraBasePoint;

                // ⚠ 只在"他还被我们藏着"时还原（Hide = 上车那一刻设的）。驾驶者死亡时状态可能已经是 Dead
                //   ⇒ 绝不能刷回 Normal（那会把他从倒地/死亡里拽出来）；这条判据也天然覆盖"车被打爆"那支
                //   （人活着 ⇒ 状态还是 Hide ⇒ 照旧还原）。
                var ownerActor = _owner.GetComponent<IActor>();
                if (ownerActor != null && ownerActor.ActorState == ActorState.Hide)
                {
                    ownerActor.ActorState = ActorState.Normal;
                }

                // 第三人称下武器相机应保持关闭；⚠ 驾驶者**死亡**时也不开：
                //   那时 PlayerController.OnDie 已经把主相机切到 PlayerDownCamera，武器相机一亮就会叠出武器画面。
                if (!_wasThirdPersonOnEnter && !_ownerDead)
                {
                    _owner.GetComponent<PlayerWeaponsManager>().WeaponCamera.enabled = true;
                    comp.GetComponentInChildren<LookAtController>().enabled = true;
                }
            }

            Health.OnDie -= OnDie;
            if (_ownerHealth != null)
            {
                _ownerHealth.OnDie -= OnDriverDie;
                _ownerHealth = null;
            }
        }

        _owner = owner;
        OnSetOwner?.Invoke(owner);

        // 【联机】上下机的**唯一收口**就是这里（进入 / 退出 / OnDie 全走它）⇒ 抛事件给 09 的 NetVehicleBridge
        // 去上报（06 看不见 09，与 BattleEventBus 那批同口径）。
        BattleEventBus.VehicleOwnerChanged(gameObject, owner);
    }

    void IDrivable.ResetCameraBasePoint(Vector3 vector)
    {
        CameraBasePoint = transform.InverseTransformPoint(vector);
    }

    void OnDie(GameObject caused, GameObject origin)
    {
        SetOwener(null);
    }

    /// <summary>
    /// 【驾驶者死亡 ⇒ 强制下车】只把"接管"撤掉，**不**把人还原成活人。
    ///
    /// <para>▍为什么不能直接复用 <c>SetOwener(null)</c> 的那套还原：那套会把 <c>ActorState</c> 刷回
    /// <c>Normal</c>、把武器相机 / 注视重新打开 —— 对刚倒地的人全是错的（等于把他从死亡态里拽出来，
    /// 还会在死亡镜头上叠一层武器画面）。所以这里只置 <see cref="_ownerDead"/>，由还原分支自己跳过那两件。</para>
    ///
    /// <para>▍为什么"必须"走 <see cref="SetOwener"/>：它是上下机的**唯一收口**，它抛的
    /// <c>BattleEventBus.VehicleOwnerChanged</c> 是 09 的桥上报 4053 的唯一来源 ⇒ 远端才会把骑手摘掉。
    /// 若在这里自己散着做几件事，客机上会留一个"人死了还坐在机甲里"的幽灵。</para>
    /// </summary>
    void OnDriverDie(GameObject caused, GameObject origin)
    {
        FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·发", $"驾驶者死亡 ⇒ 强制下车（车={name}）");
        _ownerDead = true;
        try { SetOwener(null); }
        finally { _ownerDead = false; }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (!_owner) return;

        // 防御：第三人称下相机若被意外关闭则重新激活
        if (PlayerCamera && !PlayerCamera.gameObject.activeSelf)
        {
            PlayerCamera.gameObject.SetActive(true);
        }
    }

    public override Vector3 GetInputMove()
    {
        return !_owner ? Vector3.zero : base.GetInputMove();
    }

    protected override void TryJump()
    {
        if (!_owner) return;
        base.TryJump();
    }

    protected override void HandleRotation()
    {
        if (!_owner) return;
        base.HandleRotation();
    }

}

}
