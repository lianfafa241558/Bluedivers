using System.Collections;
using System.Collections.Generic;
using Core;
using FPSGame.Furn;
using UnityEngine;

public class PlayerOperationController : MonoBehaviour
{

    public IFurniture target;

    private Camera m_Camera;
    private PlayerController m_PlayerController;

    private PlayerInputHandler m_InputHandler;
    private AudioSource aud;

    void Start()
    {
        m_PlayerController = GetComponent<PlayerController>();
        m_Camera = m_PlayerController.PlayerCamera;
        m_InputHandler = GetComponent<PlayerInputHandler>();
        WndManager.OnWindowStateChange += OnWindowStateChange;
    }
    private void OnDestroy()
    {
        WndManager.OnWindowStateChange -= OnWindowStateChange;
    }

    void Update()
    {
        if (m_PlayerController.IsThirdPerson)
        {
            // 第三人称：检测角色前方近距离的交互物，取距离最近的可交互物
            Vector3 checkPos = m_PlayerController.CenterPos + Vector3.up * 0.5f + transform.forward * 0.5f;
            IFurniture newtar = null;
            float nearestDist = float.MaxValue;
            foreach (var furn in Furniture_Attached.list.Values)
            {
                if (furn == null || furn.gameObject == null)
                    continue;
                // 排除自己身上的
                if (furn.gameObject.transform.IsChildOf(transform))
                    continue;
                if (furn.CanOperate(gameObject))
                {
                    float dist = Vector3.Distance(furn.CenterPos, checkPos);
                    if (dist < nearestDist && dist <= 2.0f)
                    {
                        nearestDist = dist;
                        newtar = furn;
                    }
                }
            }
            if (target != newtar)
            {
                CancelSteppedPress();
                if (target != null && !target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
                target = newtar;
                if (newtar == null)
                {
                    if (aud) { aud.Stop(); aud = null; }
                    m_InputHandler.InOperation = false;
                }
            }
            else if (newtar == null)
            {
                ClearTarget();
            }

            // 第三人称处于交互操作时自动切瞄准模式
            m_PlayerController.WeaponsManager.ForceAim = m_InputHandler.InOperation;
        }
        else if (Physics.Raycast(m_Camera.ScreenPointToRay(new(Screen.width / 2, Screen.height / 2, 0)), out var hit, 1.3f, m_Camera.cullingMask))
        {
            TrySetTarget(hit);
        }
        else
        {
            ClearTarget();
        }

        // 非第三人称时取消强制瞄准
        if (!m_PlayerController.IsThirdPerson)
        {
            m_PlayerController.WeaponsManager.ForceAim = false;
        }

        if (target != null)
        {
            // 逐步长按家具：把按住过程的推进权交给家具（IStepPress）
            if (target is IStepPress step && step.CanOperateStepped(gameObject))
            {
                if (m_InputHandler.GetOperateDown())
                {
                    if (step.BeginPress(gameObject))
                    {
                        if (target.AudioPress) aud = AudioSvc.PlaySound(new(target.AudioPress, target.CenterPos, 20, AudioGroups.General) { loop = true });
                    }
                }
                else if (m_InputHandler.GetOperateHeld())
                {
                    if (step.StepPress(Time.deltaTime))//本次转完，收尾
                    {
                        target.Handle(gameObject);
                        if (aud) { aud.Stop(); aud = null; }
                        if (!target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
                        if (target == null || !target.CanOperate(gameObject))
                        {
                            target = null;
                        }
                    }
                }
                else if (m_InputHandler.GetOperateUp())//取消操作（保留已转移进度）
                {
                    step.CancelPress();
                    if (aud) { aud.Stop(); aud = null; }
                }
            }
            else if (target.MeetTime == 0)
            {
                if (m_InputHandler.GetOperateDown())
                {
                    target.Handle(gameObject);
                    if (target == null || !target.CanOperate(gameObject))
                    {
                        if (aud) { aud.Stop(); aud = null; }
                        target = null;
                    }
                }
            }
            else
            {
                if (m_InputHandler.GetOperateDown())
                {
                    if (target.AudioPress) aud = AudioSvc.PlaySound(new(target.AudioPress, target.CenterPos, 20, AudioGroups.General) { loop=true});
                }
                else if (m_InputHandler.GetOperateHeld())//完成操作
                {
                    if ((target.Press += Time.deltaTime) >= target.MeetTime)
                    {
                        target.Handle(gameObject);
                        if (aud) { aud.Stop(); aud = null; }
                        if (!target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
                        if (target == null || !target.CanOperate(gameObject))
                        {
                            target = null;
                        }
                    }
                }
                else if (m_InputHandler.GetOperateUp())//取消操作
                {
                    if (!target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
                    if (aud) { aud.Stop(); aud = null; }
                }

            }


        }

    }

    /// <summary>切换/清除目标前，把正在进行的接管按压取消</summary>
    private void CancelSteppedPress()
    {
        if (target is IStepPress step) step.CancelPress();
    }

    private void TrySetTarget(RaycastHit hit)
    {
        var newtar = hit.transform.GetComponentInParent<IFurniture>();
        if(target != newtar && (newtar == null || newtar.CanOperate(gameObject)))
        {
            CancelSteppedPress();
            if (target != null && !target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
            target = newtar;
            if (newtar == null)
            {
                if (aud) { aud.Stop(); aud = null; }
                m_InputHandler.InOperation = false;
            }
        }
    }

    private void ClearTarget()
    {
        if (target != null && (!target.HaveFlag(FurnitureFlag.SwitchState) || !target.InOperate))
        {
            CancelSteppedPress();
            if (!target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
            if (aud) { aud.Stop(); aud = null; }
            target = null;
            m_InputHandler.InOperation = false;
        }
    }

    private void OnWindowStateChange(WindowStateEnum oldState,WindowStateEnum state)
    {
        switch (state)
        {
            case WindowStateEnum.Game:
                enabled = true;
                break;
            default:
                CancelSteppedPress();
                if (target != null &&!target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
                if (aud) { aud.Stop(); aud = null; }
                target = null;
                m_InputHandler.InOperation = false;
                enabled = false;
                break;
        }
    }
}
