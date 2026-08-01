using Core;
using FPSGame.Attribute;
using GameContract;
using RootMotion.FinalIK;

using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Events;

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
public class VehicleController : BaseSelfMoveableController,IDrivable
{
    public event UnityAction<bool> OnSetOwner;

    //[SerializeField]
    private GameObject _owner;
    GameObject IDrivable.CurrentOwner => _owner;
    private bool _wasThirdPersonOnEnter;

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
            owner.GetComponent<I_Actor>().ActorState = ActorState.Hide;
            owner.GetComponent<PlayerWeaponsManager>().WeaponCamera.enabled = false;
            if (!_wasThirdPersonOnEnter)
            {
                comp.GetComponentInChildren<LookAtController>().enabled = false;
            }


            RecordCameraBasePoint = PlayerCamera.transform.localPosition;

            Health.OnDie += OnDie;
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

                _owner.GetComponent<I_Actor>().ActorState = ActorState.Normal;

                // 第三人称下武器相机应保持关闭
                if (!_wasThirdPersonOnEnter)
                {
                    _owner.GetComponent<PlayerWeaponsManager>().WeaponCamera.enabled = true;
                    comp.GetComponentInChildren<LookAtController>().enabled = true;
                }
            }

            Health.OnDie -= OnDie;
        }

        _owner = owner;
        OnSetOwner?.Invoke(owner);
    }

    void IDrivable.ResetCameraBasePoint(Vector3 vector)
    {
        CameraBasePoint = transform.InverseTransformPoint(vector);
    }

    void OnDie(GameObject _)
    {
        SetOwener(null);
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

