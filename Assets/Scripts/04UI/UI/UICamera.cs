using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
public class UICamera : Singleton<UICamera>
{
    public static Camera uiCamera;

    private UniversalAdditionalCameraData uiCameraData;

    // 用于标记主相机是否已被找到，避免每帧都Find
    private Camera currentBaseCamera;

    public override void Awake()
    {
        base.Awake();
        uiCamera = GetComponent<Camera>();
        uiCameraData = uiCamera.GetUniversalAdditionalCameraData();
    }

    void Update()
    {
        // 1. 寻找场景中除自己以外的、活跃的、RenderType为Base的相机
        Camera targetBaseCamera = FindBaseCamera();

        if (targetBaseCamera != null)
        {
            // 情况A：有其他Base相机存在，需要将UI设为Overlay并依附上去
            SetAsOverlayAndAttachTo(targetBaseCamera);
        }
        else
        {
            // 情况B：没有其他Base相机，UI相机自己充当Base角色
            SetAsBaseCamera();
        }
    }

    /// <summary>
    /// 查找场景中最合适的Base相机（例如主相机）
    /// </summary>
    private Camera FindBaseCamera()
    {
        // 这里你可以使用Tag（比如"MainCamera"）或类型来查找
        Camera[] allCameras = Camera.allCameras;
        foreach (Camera cam in allCameras)
        {
            if (cam == null) continue;
            // 跳过自己
            if (cam == uiCamera) continue;

            // 检查相机是否激活，并获取它的URP数据
            if (cam.isActiveAndEnabled&& cam.depth >= uiCamera.depth)
            {
                var camData = cam.GetUniversalAdditionalCameraData();
                // 确保目标是Base类型
                if (camData != null  && camData.renderType == CameraRenderType.Base)
                {
                    return cam;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 将UI相机设为Overlay模式，并添加到主相机的堆栈中
    /// </summary>
    private void SetAsOverlayAndAttachTo(Camera baseCamera)
    {
        // 1. 修改自身类型
        if (uiCameraData.renderType != CameraRenderType.Overlay)
        {
            uiCameraData.renderType = CameraRenderType.Overlay;
            Debug.Log($"[相机管理] {uiCamera.name} 切换到 Overlay 模式");
        }

        // 2. 获取目标相机的堆栈数据
        var baseCameraData = baseCamera.GetUniversalAdditionalCameraData();
        if (baseCameraData == null) return;

        // 3. 如果UI相机不在堆栈中，则添加
        if (!baseCameraData.cameraStack.Contains(uiCamera))
        {
            // 防止重复添加
            if (currentBaseCamera != null && currentBaseCamera != baseCamera && currentBaseCamera.TryGetComponent<UniversalAdditionalCameraData>(out var oldData))
            {
                oldData.cameraStack.Remove(uiCamera);
            }

            baseCameraData.cameraStack.Add(uiCamera);
            currentBaseCamera = baseCamera;
            Debug.Log($"[相机管理] 将 {uiCamera.name} 添加到 {baseCamera.name} 的堆栈中");
        }
    }

    /// <summary>
    /// 将UI相机设为基础相机模式，自己渲染所有内容
    /// </summary>
    private void SetAsBaseCamera()
    {
        // 如果之前是Overlay且有依附的相机，先从它的堆栈中移除
        if (currentBaseCamera != null && currentBaseCamera.TryGetComponent<UniversalAdditionalCameraData>(out var oldData))
        {
            oldData.cameraStack.Remove(uiCamera);
            currentBaseCamera = null;
        }

        // 修改自身为Base模式
        if (uiCameraData.renderType != CameraRenderType.Base)
        {
            uiCameraData.renderType = CameraRenderType.Base;
            Debug.Log($"[相机管理] {uiCamera.name} 切换到 Base 模式（无其他相机，作为主相机）");
        }

        // 确保清空自己的堆栈（避免死循环引用自身？一般不需要，但清空更安全）
        // 如果你的UI相机下面不需要叠加其他相机，可以清空
        // uiCameraData.cameraStack.Clear(); 
    }
}
