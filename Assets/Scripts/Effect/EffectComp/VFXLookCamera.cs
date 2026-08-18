using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
