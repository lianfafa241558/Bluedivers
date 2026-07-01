using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;

using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Events;
/// <summary>
/// 炮台控制器
/// </summary>
public class EmplacementController : BaseSelfController, IDrivable
{
    public event UnityAction<bool> OnSetOwner;

    [Foldout("一般", true)]
    //[SerializeField]
    //private Transform CameraPoint;


    //[SerializeField]
    private GameObject owner;
    private Camera PlayerCamera;

    Camera IDrivable.PlayerCamera => PlayerCamera;
    Vector3 CameraBasePoint { get; set; }

    public float MoveSpeedScale { get; set; }
    protected Vector3 RecordCameraBasePoint { get; set; }

    public bool TryExit() => InputHandler.GetOperateDown();
    public void SetOwener(GameObject owner)
    {

        if (owner)
        {
            var comp = owner.GetComponent<PlayerController>();
            PlayerCamera = comp.PlayerCamera;
            comp.enabled = false;
            comp.Controller.enabled = false;
            owner.GetComponent<I_Actor>().ActorState = ActorState.Hide;
            owner.GetComponent<PlayerWeaponsManager>().WeaponCamera.enabled = false;
            RecordCameraBasePoint = PlayerCamera.transform.localPosition;
            //CameraBasePoint = transform.InverseTransformPoint(CameraPoint.position);
            Health.OnDie += OnDie;
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

                this.owner.GetComponent<I_Actor>().ActorState = ActorState.Normal;
                this.owner.GetComponent<PlayerWeaponsManager>().WeaponCamera.enabled = true;
            }
            Health.OnDie -= OnDie;
        }

        this.owner = owner;
        OnSetOwner?.Invoke(owner);
    }

    void OnDie(GameObject _)
    {
        SetOwener(null);

    }

    protected void LateUpdate()
    {
        if (!owner) return;

        var target = transform.TransformPoint(CameraBasePoint);
        float dis = Vector3.Distance(PlayerCamera.transform.position, target);
        if (dis > 0.1f)
        {
            PlayerCamera.transform.position = Vector3.Lerp(PlayerCamera.transform.position,
            target, Time.deltaTime*5);
        }
        
        PlayerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);



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
