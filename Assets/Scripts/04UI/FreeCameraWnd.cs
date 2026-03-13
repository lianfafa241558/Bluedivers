using Core;
using UnityEngine;
using static WndTools.WndRootTool;

public class FreeCameraWnd : WindowRoot, Wnd
{
    public Transform Tip,showTime;


    new public Transform camera;
    private float speed = 4;

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
        if (InputManager.GetDown(InputState.Acceler))speed = Mathf.Min(speed + 0.5f, 15);
        if (InputManager.GetDown(InputState.Deceler))speed = Mathf.Max(speed - 0.5f, 1);

        SetText(showTime, string.Format("移动速度: {0}\n位置: ({1:F2}, {2:F2}, {3:F2})", speed, camera.position.x, camera.position.y, camera.position.z));

    }

    public override void Init()
    {
        GameRoot.OnWindowStateChange += OnWindowStateChange;
        SetActive(camera,false);
        SetWndState(false);
    }
    public override void UnInit()
    {
        GameRoot.OnWindowStateChange -= OnWindowStateChange;
    }

    protected override void FirstShowWnd()
    {
        
    }

    protected override void ShowWnd()
    {
        GameRoot.CreateTimer(()=> { GameRoot.TimeScale = 0.005f; },0.1f);
        
        var main = Camera.main.transform;
        var forward = main.forward;
        forward.Scale(new(1, 0, 1));
        forward.Normalize();
        camera.position = main.transform.position - forward * 10 + Vector3.up * 5;
        camera.LookAt(main);
        SetActive(camera, true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected override void HideWnd()
    {
        SetActive(camera, false);
    }


    private void OnWindowStateChange(WindowStateEnum oldState, WindowStateEnum state)
    {
        switch (state)
        {
            case WindowStateEnum.FreeCamera:
                SetWndState(true);

                break;
            default:
                SetWndState(false);
                break;
        }
    }
}
