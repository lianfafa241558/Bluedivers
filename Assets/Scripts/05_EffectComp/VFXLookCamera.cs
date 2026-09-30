using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.EffectComp
{

/// <summary>
/// 让特效持续朝向相机（公告板）。
/// </summary>
[AddComponentMenu("特效/朝向相机")]
public class VFXLookCamera : MonoBehaviour
{
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

        transform.LookAt(_cameraTransform);
    }

    private void RefreshCamera()
    {
        Camera mainCamera = Camera.main;
        _cameraTransform = mainCamera != null ? mainCamera.transform : null;
    }

}
}
