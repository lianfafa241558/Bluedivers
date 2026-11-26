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

    /// <summary>H 键的显隐开关（Tip / showTime / 导演面板 / 落点标记统一听它的）。</summary>
    private bool _hudVisible = true;

    public void Update()
    {
        // 开窗期间阶段可能变（打完回结算等），每帧对一次开关
        SyncDirectorState();

        if (InputManager.GetDown(InputState.H))
        {
            _hudVisible = !_hudVisible;
            ApplyHudVisibility();
        }
        if (_directorActive && InputManager.GetDown(InputState.J) && _director != null)
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

    /// <summary>
    /// 面板只有导演模式可用时才显示，其余情况只是原来的自由相机 HUD。
    /// <para>⚠ Tip/showTime 在 prefab 里必挂，<c>SetActive</c> 内部不判空。</para>
    /// </summary>
    private void ApplyHudVisibility()
    {
        SetActive(Tip, _hudVisible);
        SetActive(showTime.parent, _hudVisible);
        if (_panel != null) _panel.SetVisible(_hudVisible && _directorActive);
        // 落点标记也要一起藏：它是世界物体，不属于 UI，但拍摄时要"干净画面"
        if (_director != null) _director.SetMarkerVisible(_hudVisible);
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
        // 每次开窗都重算：只有战斗阶段才进导演模式，其他阶段保持原来的自由相机
        _directorActive = false;
        SyncDirectorState();
    }

    protected override void HideWnd()
    {
        // 没进过导演模式就别 Exit：它会写回 timeScale=1、改玩家输入，会影响别的阶段
        if (_director != null && _directorActive)
        {
            _director.Exit();
            _directorActive = false;
        }

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
