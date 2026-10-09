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

    /// <summary>玩家装备总控：用于"手持物品时按交互键丢下手中的物品"</summary>
    private EquipController m_EquipController;

    void Start()
    {
        m_PlayerController = GetComponent<PlayerController>();
        m_Camera = m_PlayerController.PlayerCamera;
        m_InputHandler = GetComponent<PlayerInputHandler>();
        m_EquipController = GetComponent<EquipController>();
        FPSGame.Gameplay.GlobalEventBus.OnWindowStateChange += OnWindowStateChange;
    }
    private void OnDestroy()
    {
        FPSGame.Gameplay.GlobalEventBus.OnWindowStateChange -= OnWindowStateChange;
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

        // 手持物品（HandEquip）时，交互键的三种去向：
        // 1) 正前方是"按下即拾取的手持物"（Furniture_HandEquip 且 MeetTime == 0，如神器/便携装备）：
        //    挂点与双手 IK 只有一个，先把手中的丢下、再把它拿起来（本帧连续完成）；
        // 2) 正前方目标"当前确实可交互"（如炮位/提交点需要用到手中物品）：交互键让给它，物品留着；
        // 3) 其余（面前无物，或 target 粘滞在已不可交互的旧目标上）：丢下手中的物品。
        // 判据用 CanOperate 而不是只判 target != null —— target 会粘滞在已失效的旧目标上，
        // 只看非空会导致玩家怎么按 E 都丢不下手里的东西。
        bool pickupHeldItem = false;
        bool droppedHeld = false;
        if (m_EquipController != null
            && m_EquipController.IsHoldingHandEquip()
            && m_InputHandler.GetOperateDown())
        {
            bool targetOperable = target != null && target.CanOperate(gameObject);
            // 只对"按下即拾取"（MeetTime == 0，当前 ArtifactA / Shell_Explosive 都是）的手持物做替换：
            // 需要长按的拾取物若在按下时就丢掉旧物，玩家中途松手会变成"丢了却没拿到"。
            pickupHeldItem = targetOperable && target is Furniture_HandEquip && target.MeetTime == 0f;

            if (pickupHeldItem || !targetOperable)
            {
                // 紧接着就要捡起面前那件 ⇒ 替换式丢下：跳过"切回主武器"（免得同帧又切主武器又切空手），
                // 也不播"卸载"语音（紧接着会播"安装"语音，两条连着播会打架）
                droppedHeld = m_EquipController.TryDropHandEquip(replace: pickupHeldItem);
            }
        }

        // 丢下后本帧不再交互；但"面前是手持物"时要把 E 继续用于把它拿起来
        if (target != null && (!droppedHeld || pickupHeldItem))
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
