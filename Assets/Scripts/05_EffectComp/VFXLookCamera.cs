using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.EffectComp
{

/// <summary>
/// 让特效持续朝向相机（公告板）。
/// 可逐轴关闭旋转：关掉的轴固定为 0（世界轴），用于避免特效随俯仰翻倒或自转。
/// </summary>
[AddComponentMenu("特效/朝向相机")]
public class VFXLookCamera : MonoBehaviour
{
    [SerializeField]
    [InspectorName("允许X轴旋转(俯仰)")]
    private bool _allowPitch = true;

    [SerializeField]
    [InspectorName("允许Y轴旋转(偏航)")]
    private bool _allowYaw = true;

    [SerializeField]
    [InspectorName("允许Z轴旋转(翻滚)")]
    private bool _allowRoll = true;

    private Transform _cameraTransform;

    private void Start()
    {
        RefreshCamera();
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null)
        {
            RefreshCamera();
            if (_cameraTransform == null)
            {
                return;
            }
        }

        if (_allowPitch && _allowYaw && _allowRoll)
        {
            transform.LookAt(_cameraTransform);
            return;
        }

        // 三轴全开时走 LookAt 保持原行为；否则拆欧拉角清零被锁的轴。
        Quaternion lookRotation = Quaternion.LookRotation(_cameraTransform.position - transform.position, Vector3.up);
        Vector3 euler = lookRotation.eulerAngles;
        if (!_allowPitch)
        {
            euler.x = 0f;
        }
        if (!_allowYaw)
        {
            euler.y = 0f;
        }
        if (!_allowRoll)
        {
            euler.z = 0f;
        }
        transform.rotation = Quaternion.Euler(euler);
    }

    private void RefreshCamera()
    {
        Camera mainCamera = Camera.main;
        _cameraTransform = mainCamera != null ? mainCamera.transform : null;
    }

}
}
