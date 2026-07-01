using Core;
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


    [Foldout("一般", true)]
    //[SerializeField]
    //private Transform CameraPoint;


    //[SerializeField]
    private GameObject owner;

    Camera IDrivable.PlayerCamera => PlayerCamera;
    //Vector3 IDrivable.CameraBasePoint=CameraBasePoint;

    protected Vector3 RecordCameraBasePoint { get; set; }

    //public int PlayerIndex{ get; private set; }
    public bool TryExit() => InputHandler.GetOperateDown();
    public void SetOwener(GameObject owner)
    {

        if (owner)
        {
            var comp = owner.GetComponent<PlayerController>();
            PlayerCamera = comp.PlayerCamera;
             //PlayerIndex = comp.PlayerIndex;
            comp.enabled = false;
            comp.Controller.enabled = false;
            owner.GetComponent<I_Actor>().ActorState= ActorState.Hide;
            owner.GetComponent<PlayerWeaponsManager>().WeaponCamera.enabled=false;
            comp.GetComponentInChildren<LookAtController>().enabled = false;

            RecordCameraBasePoint = PlayerCamera.transform.localPosition;
            //CameraBasePoint = transform.InverseTransformPoint(CameraPoint.position);
            //Controller.enabled = true;
            Health.OnDie += OnDie;
        }
        else
        {
            PlayerCamera = null;
            //PlayerIndex = -1;
            if (this.owner)
            {
                var comp = this.owner.GetComponent<PlayerController>();
                comp.enabled = true;
                comp.Controller.enabled = true;
                this.owner.transform.parent = null;
                comp.PlayerCamera.transform.localPosition = RecordCameraBasePoint;
                comp.GetComponentInChildren<LookAtController>().enabled = true;
                this.owner.GetComponent<I_Actor>().ActorState = ActorState.Normal;
                this.owner.GetComponent<PlayerWeaponsManager>().WeaponCamera.enabled = true;
            }
            Health.OnDie -= OnDie;
        }

        this.owner = owner;
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
        if (!owner) return;

    }

    public override Vector3 GetInputMove()
    {
        return !owner ? Vector3.zero : base.GetInputMove();
    }


    protected override void TryJump()
    {
        if (!owner) return;
        base.TryJump();
    }

    protected override void HandleRotation()
    {
        if (!owner) return;
        base.HandleRotation();
    }

}

