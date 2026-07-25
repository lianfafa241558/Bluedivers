using GameContract;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;

using UnityEngine;

/// <summary>
/// PlayerController 第三人称视角相关逻辑
/// </summary>
public partial class PlayerController
{
    private Vector3 _thirdPersonCameraVelocity;

    private Transform _cameraOriginalParent;

    public Vector3 ScreenCenterTargetPoint { get; private set; }

    /// <summary>
    /// 第三人称旋转速度（限制上限，避免 angularSpeed 过大导致瞬间转向）
    /// </summary>
    private const float k_ThirdPersonRotationSpeedMax = 10f;

    private void RestoreCameraParent()
    {
        if (_cameraOriginalParent && PlayerCamera && _cameraOriginalParent.gameObject.activeInHierarchy)
        {
            PlayerCamera.transform.SetParent(_cameraOriginalParent, true);
            _cameraOriginalParent = null;
        }
    }

    private void HandleToggleView()
    {
        if (!IsDead && InputHandler.GetToggleViewDown())
        {
            IsThirdPerson = !IsThirdPerson;
            ApplyViewMode();
        }
    }

    /// <summary>
    /// 第三人称角色旋转
    /// </summary>
    private void HandleRotationThirdPerson()
    {
        float dt = Time.deltaTime;
        float rawAngularSpeed = GetAttribute(UnitAttrType.AngularSpeed).FinalValue.RawFloat;

        m_CameraVerticalAngle += InputHandler.GetLookInputsVertical() * rawAngularSpeed * dt;
        m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -UpperRotationLimit, LowerRotationLimit);

        bool isAiming = WeaponsManager.IsAiming;

        // 瞄准状态变化时开关头部IK组件
        if (isAiming != _wasAimingLastFrame)
        {
            SetHeadIKEnabled(isAiming);
        }
        _wasAimingLastFrame = isAiming;

        Vector3 rawInput = InputHandler.GetMoveInput();
        bool isMoving = rawInput.sqrMagnitude > 0.01f;

        if (isAiming)
        {
            // 瞄准时：_cameraYaw 由 HandleThirdPersonCamera 中鼠标输入驱动
            // 角色朝向瞄准方向
            float yawDelta = InputHandler.GetLookInputsHorizontal() * rawAngularSpeed * dt;
            _cameraYaw += yawDelta;
            Quaternion targetRotation = Quaternion.Euler(0, _cameraYaw, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rawAngularSpeed * dt);
        }
        else
        {
            float angularSpeed = Mathf.Min(rawAngularSpeed, k_ThirdPersonRotationSpeedMax);
            if (isMoving)
            {
                // 非瞄准 + 移动：面向移动方向（基于相机yaw）
                float yawRad = _cameraYaw * Mathf.Deg2Rad;
                Vector3 camForward = new Vector3(Mathf.Sin(yawRad), 0, Mathf.Cos(yawRad));
                Vector3 camRight = new Vector3(Mathf.Cos(yawRad), 0, -Mathf.Sin(yawRad));
                Vector3 moveDir = (camForward * rawInput.z + camRight * rawInput.x).normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), angularSpeed * dt);
            }
            else
            {
                // 非瞄准 + 静止：快速转向相机方向
                Quaternion targetRotation = Quaternion.Euler(0, _cameraYaw, 0);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, angularSpeed * dt);
            }
        }
    }

    private bool _wasAimingLastFrame;

    private RootMotion.FinalIK.AimIK _aimIK;
    private RootMotion.FinalIK.AimController _aimController;

    /// <summary>
    /// 第三人称瞄准/非瞄准时开关头部IK组件
    /// </summary>
    private void SetHeadIKEnabled(bool enabled)
    {
        if (ModleRoot)
        {
            if (!_aimIK) _aimIK = ModleRoot.GetComponent<RootMotion.FinalIK.AimIK>();
            if (_aimIK) _aimIK.enabled = enabled;

            if (!_aimController) _aimController = ModleRoot.GetComponent<RootMotion.FinalIK.AimController>();
            if (_aimController) _aimController.enabled = enabled;
        }
    }

    /// <summary>
    /// 第三人称移动输入：基于相机yaw
    /// </summary>
    private Vector3 GetInputMoveThirdPerson()
    {
        Vector3 rawInput = InputHandler.GetMoveInput();
        float yawRad = _cameraYaw * Mathf.Deg2Rad;
        Vector3 camForward = new Vector3(Mathf.Sin(yawRad), 0, Mathf.Cos(yawRad));
        Vector3 camRight = new Vector3(Mathf.Cos(yawRad), 0, -Mathf.Sin(yawRad));
        return camForward * rawInput.z + camRight * rawInput.x;
    }

    /// <summary>
    /// 第三人称相机处理
    /// </summary>
    private void HandleThirdPersonCamera()
    {
        if (!PlayerCamera || !_thirdPersonCameraPoint) return;

        bool isAiming = WeaponsManager.IsAiming;

        if (isAiming)
        {
            // 瞄准时：相机脱离父级，围绕 CenterPos 做轨道旋转
            if (PlayerCamera.transform.parent != null)
            {
                _cameraOriginalParent = PlayerCamera.transform.parent;
                PlayerCamera.transform.SetParent(null, true);
            }

            float clampedV = Mathf.Clamp(m_CameraVerticalAngle, -_thirdPersonUpperLimit, _thirdPersonLowerLimit);

            Transform aimPoint = _thirdPersonAimCameraPoint ?? _thirdPersonCameraPoint;
            float xOffset = aimPoint.localPosition.x;
            float height = aimPoint.localPosition.y;
            float distance = Mathf.Abs(aimPoint.localPosition.z);

            Quaternion rotation = Quaternion.Euler(clampedV, _cameraYaw, 0);
            Vector3 offset = rotation * new Vector3(xOffset, height, -distance);
            Vector3 desiredPosition = CenterPos + offset;

            // 遮挡检测
            Vector3 origin = CenterPos;
            Vector3 dir = (desiredPosition - origin).normalized;
            float maxDist = Vector3.Distance(origin, desiredPosition);
            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, _thirdPersonOcclusionLayers, QueryTriggerInteraction.Ignore))
            {
                float occludedDist = Mathf.Max(hit.distance - 0.3f, _thirdPersonMinDistance);
                desiredPosition = origin + dir * occludedDist;
            }

            PlayerCamera.transform.position = Vector3.SmoothDamp(
                PlayerCamera.transform.position, desiredPosition, ref _thirdPersonCameraVelocity, 1f / _thirdPersonSmoothSpeed);

            PlayerCamera.transform.rotation = Quaternion.Slerp(
                PlayerCamera.transform.rotation, rotation, Time.deltaTime * _thirdPersonSmoothSpeed);
        }
        else
        {
            // 非瞄准：相机脱离父级，围绕角色旋转
            if (PlayerCamera.transform.parent != null)
            {
                _cameraOriginalParent = PlayerCamera.transform.parent;
                PlayerCamera.transform.SetParent(null, true);
            }

            float angularSpeed = GetAttribute(UnitAttrType.AngularSpeed).FinalValue.RawFloat * _thirdPersonRotationSensitivity;
            _cameraYaw += InputHandler.GetLookInputsHorizontal() * angularSpeed * Time.deltaTime;

            float clampedVertical = Mathf.Clamp(m_CameraVerticalAngle, -_thirdPersonUpperLimit, _thirdPersonLowerLimit);

            // 相机位置：基于 _thirdPersonCameraPoint 的偏移
            float xOffset = _thirdPersonCameraPoint.localPosition.x;
            float height = _thirdPersonCameraPoint.localPosition.y;
            float distance = Mathf.Abs(_thirdPersonCameraPoint.localPosition.z);

            Quaternion rotation = Quaternion.Euler(clampedVertical, _cameraYaw, 0);
            Vector3 offset = rotation * new Vector3(xOffset, height, -distance);

            Vector3 desiredPosition = CenterPos + offset;

            // 遮挡检测
            Vector3 origin = CenterPos;
            Vector3 dir = (desiredPosition - origin).normalized;
            float maxDist = Vector3.Distance(origin, desiredPosition);
            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, _thirdPersonOcclusionLayers, QueryTriggerInteraction.Ignore))
            {
                float occludedDist = Mathf.Max(hit.distance - 0.3f, _thirdPersonMinDistance);
                desiredPosition = origin + dir * occludedDist;
            }

            PlayerCamera.transform.position = Vector3.SmoothDamp(
                PlayerCamera.transform.position, desiredPosition, ref _thirdPersonCameraVelocity, 1f / _thirdPersonSmoothSpeed);

            // 相机注视角色中心
            PlayerCamera.transform.rotation = Quaternion.Slerp(
                PlayerCamera.transform.rotation, rotation, Time.deltaTime * _thirdPersonSmoothSpeed);
        }

        // 屏幕中心目标点
        var screenCenterRay = PlayerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(screenCenterRay, out RaycastHit targetHit, 1000f, _thirdPersonOcclusionLayers, QueryTriggerInteraction.Ignore))
            ScreenCenterTargetPoint = targetHit.point;
        else
            ScreenCenterTargetPoint = screenCenterRay.GetPoint(500f);

        _wasAimingLastFrameCamera = isAiming;
    }

    private float _cameraYaw;
    private bool _wasAimingLastFrameCamera;
}
