using FPSGame.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;

/// <summary>
/// 自由相机模式的窗口（PV 导演模式的宿主）。
/// <para>▍导演逻辑在 <see cref="FPSGame.Effect.DirectorMode"/>（世界操作），本类只做键位转发与面板刷新。
/// 分部件见 <c>FreeCameraWnd_Director.cs</c>。</para>
/// </summary>
[AddComponentMenu("UI/窗口/自由相机")]
public partial class FreeCameraWnd : Window
{
    public Transform Tip,showTime;


    new public Transform camera;
    private float speed = 10;

    private float _nextHudTime;

    public void Update()
    {
        if (InputManager.GetDown(InputState.H))
        {
            bool state = !GetActive(Tip);
            SetActive(Tip, state);
            SetActive(showTime.parent, state);
            if (_panel != null) _panel.SetVisible(state);
            // 落点标记也要一起藏：它是世界物体，不属于 UI，但拍摄时要"干净画面"
            if (_director != null) _director.SetMarkerVisible(state);
        }
        if (InputManager.GetDown(InputState.J) && _director != null)
        {
            _director.TogglePause();
        }
        if (Input.GetKeyDown(KeyCode.U))
        {
            RenderSettings.fog = !RenderSettings.fog;
        }
        if (InputManager.GetDown(InputState.Acceler))speed = Mathf.Min(speed + 1f, 35);
        if (InputManager.GetDown(InputState.Deceler))speed = Mathf.Max(speed - 1f, 1);

        UpdateDirectorHotkeys();

        // 文本拼接每帧一次有 GC 压力，5Hz 足够读
        if (Time.unscaledTime >= _nextHudTime)
        {
            _nextHudTime = Time.unscaledTime + 0.2f;
            SetText(showTime, string.Format("移动速度: {0}\n位置: ({1:F2}, {2:F2}, {3:F2})", speed, camera.position.x, camera.position.y, camera.position.z));
        }

        RefreshDirectorPanel();
    }

    protected override void FirstShowWnd()
    {
        InitDirector();
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

        var baseCameraData = camera.GetComponent<Camera>().GetUniversalAdditionalCameraData();
        // 3. 如果UI相机不在堆栈中，则添加
        if (!baseCameraData.cameraStack.Contains(UICamera.uiCamera))
        {
            baseCameraData.cameraStack.Add(UICamera.uiCamera);
            Debug.Log($"[相机管理]  {UICamera.uiCamera.name} 添加 {camera.name} 的堆栈中");
        }

        // 必须在 FreeCamera 激活之后：控制器在 OnEnable 里取 Camera 组件
        if (_director != null) _director.Enter();
    }

    protected override void HideWnd()
    {
        if (_director != null) _director.Exit();

        SetActive(camera, false);
        var baseCameraData = camera.GetComponent<Camera>().GetUniversalAdditionalCameraData();
        if (baseCameraData.cameraStack.Contains(UICamera.uiCamera))
        {
            baseCameraData.cameraStack.Remove(UICamera.uiCamera);
            Debug.Log($"[相机管理]  {UICamera.uiCamera.name} 移除 {camera.name} 的堆栈中");
        }
    }


 
}
}
