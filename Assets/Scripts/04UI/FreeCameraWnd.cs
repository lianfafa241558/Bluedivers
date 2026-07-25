using Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static WndTools.WndRootTool;

public class FreeCameraWnd : Window
{
    public Transform Tip,showTime;


    new public Transform camera;
    private float speed = 10;

    public void Update()
    {
        if (InputManager.GetDown(InputState.H))
        {
            bool state = !GetActive(Tip);
            SetActive(Tip, state);
            SetActive(showTime.parent, state);
        }
        if (InputManager.GetDown(InputState.J))
        {
            GameRoot.TimeScale= GameRoot.TimeScale>0.1?0.01f:1f;
        }
        if (Input.GetKeyDown(KeyCode.U))
        {
            RenderSettings.fog = !RenderSettings.fog;
        }
        if (InputManager.GetDown(InputState.Acceler))speed = Mathf.Min(speed + 1f, 35);
        if (InputManager.GetDown(InputState.Deceler))speed = Mathf.Max(speed - 1f, 1);

        SetText(showTime, string.Format("移动速度: {0}\n位置: ({1:F2}, {2:F2}, {3:F2})", speed, camera.position.x, camera.position.y, camera.position.z));

    }

    protected override void FirstShowWnd()
    {
        
    }

    protected override void ShowWnd()
    {
        //GameRoot.CreateTimer(()=> { GameRoot.TimeScale = 0.005f; },0.1f);
        
        var main = Camera.main.transform;
        var forward = main.forward;
        forward.Scale(new(1, 0, 1));
        forward.Normalize();
        camera.position = main.transform.position - forward * 10 + Vector3.up * 5;
        camera.LookAt(main);
        SetActive(camera, true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameRoot.TimeScale = 0.005f;

        var baseCameraData = camera.GetComponent<Camera>().GetUniversalAdditionalCameraData();
        // 3. 如果UI相机不在堆栈中，则添加
        if (!baseCameraData.cameraStack.Contains(UICamera.uiCamera))
        {
            baseCameraData.cameraStack.Add(UICamera.uiCamera);
            Debug.Log($"[相机管理]  {UICamera.uiCamera.name} 添加 {camera.name} 的堆栈中");
        }
    }

    protected override void HideWnd()
    {
        SetActive(camera, false);
        GameRoot.TimeScale = 1f;
        var baseCameraData = camera.GetComponent<Camera>().GetUniversalAdditionalCameraData();
        if (baseCameraData.cameraStack.Contains(UICamera.uiCamera))
        {
            baseCameraData.cameraStack.Remove(UICamera.uiCamera);
            Debug.Log($"[相机管理]  {UICamera.uiCamera.name} 移除 {camera.name} 的堆栈中");
        }
    }


 
}
