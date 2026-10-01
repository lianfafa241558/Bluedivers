using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Audio;

namespace FPSGame.Gameplay
{

/// <summary>
/// 扫描交互目标、处理长按，并把按压输入转交给 IStepPress。
/// </summary>
[AddComponentMenu("玩家/交互控制器")]
public class PlayerOperationController : MonoBehaviour
{
    /// <summary>第三人称与物体交互时的相机视距缩放（0.5 = 视距缩小一半，交互物看起来更大）</summary>
    private const float k_InteractCameraDistanceScale = 0.3f;

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
        FPSGame.Gameplay.GlobalEventSub.OnWindowStateChange += OnWindowStateChange;
    }
    private void OnDestroy()
    {
        FPSGame.Gameplay.GlobalEventSub.OnWindowStateChange -= OnWindowStateChange;
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

        // 第三人称与物体交互时拉近相机视距，让交互物看起来更大。
        // ⚠ 必须同时等 IsAiming 为 true：交互会先 ForceAim，但 IsAiming 要等切空手武器动画走完（WeaponSwitchDelay×2，约 0.4s）
        // 才为 true，相机基准点也正是在 IsAiming 为 true 时才切到瞄准点。若此处只判 InOperation，
        // 就会出现"视距先缩（还在非瞄准点）→ 0.4s 后切瞄准点再缩"的相机两段跳。
        m_PlayerController.ThirdPersonDistanceScale =
            m_PlayerController.IsThirdPerson && m_InputHandler.InOperation && target != null
            && m_PlayerController.WeaponsManager.IsAiming
                ? k_InteractCameraDistanceScale : 1f;

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
                // 脚本被禁用后不再刷新，需在此还原相机视距
                if (m_PlayerController) m_PlayerController.ThirdPersonDistanceScale = 1f;
                enabled = false;
                break;
        }
    }
}
}
