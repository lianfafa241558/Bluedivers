using Core;
using Unity.BaseTool;
using UnityEngine;

//相关的设置都在项目设置-输入管理器里面


public class PlayerInputHandler : MonoBehaviour
{
    /// <summary>正在和物体交互</summary>
    [InspectorName("正在和物体交互")]
    [DisplayField]
    public bool InOperation;
    /// <summary>正在被窗口阻止</summary>
    [InspectorName("正在被窗口阻止")]
    [DisplayField]
    public bool InWndPrevent;

    private float LastEndOperateTime;

    PlayerController m_PlayerCharacterController;
    /*
    private void Awake()
    {
        Debug.LogWarning("输入组件的Awake");
    }
    */
    void Start()
    {
        GlobalEventManager.OnFurnitureOperate += OnOperation;
        m_PlayerCharacterController = GetComponent<PlayerController>();
        GlobalEventManager.OnWndSwitch += OnWndSwitch;

        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
        //Debug.LogWarning("输入组件的Start");
    }
    void OnDestroy()
    {
        GlobalEventManager.OnFurnitureOperate -= OnOperation;
        GlobalEventManager.OnWndSwitch -= OnWndSwitch;
    }

    string[] preventWnds = new string[] { "MiniMapWnd" };
    int preventWndCount;

    void OnWndSwitch(string name,bool state)
    {
        //Debug.LogError("窗口切换"+name+"状态"+ state+"序号"+ System.Array.IndexOf(preventWnds, name));
        if (System.Array.IndexOf(preventWnds, name) >= 0)
        {
            if(state && preventWndCount++ == 0)
            {
                InWndPrevent = true;
            }
            else if (!state && --preventWndCount == 0)
            {
                InWndPrevent = false;
            }
        }
    }


    void OnOperation(GameObject user,Furniture_Base furn)
    {
        bool switchState = furn.HaveFlag(FurnitureFlag.SwitchState);
        if (user == gameObject && switchState)
        {
            InOperation = furn.inOperate;
            if (!InOperation) LastEndOperateTime = Time.time;
        }
    }

    /// <summary>
    /// 允许输入操作
    /// </summary>
    public bool CanProcessInput(bool allowOperation=false)
    {
        return (allowOperation||!InOperation) && (Time.time - LastEndOperateTime)>0.5f && Cursor.lockState == CursorLockMode.Locked;
    }
    public bool CanProcessInput(GameStateEnum state, bool allowOperation = false)
    {
        return CanProcessInput(allowOperation) && GameRoot.GameState == state;
    }
    public bool CanProcessInput(WindowStateEnum winState, bool allowOperation = false)
    {
        return CanProcessInput(allowOperation) && GameRoot.WindowState == winState;
    }
    public bool CanProcessInput(GameStateEnum gameState,WindowStateEnum winState, bool allowOperation = false)
    {
        return CanProcessInput(allowOperation) && GameRoot.GameState == gameState &&GameRoot.WindowState == winState;
    }

    /// <summary>
    /// 玩家输入的位移方向
    /// </summary>
    public Vector3 GetMoveInput()
    {
        if (!CanProcessInput()) return Vector3.zero;
        return new Vector3(InputManager.GetAxis(InputState.Horizontal), 0f, InputManager.GetAxis(InputState.Vertical)).normalized;
    }
    /// <summary>
    /// 鼠标X轴移动
    /// </summary>
    public float GetLookInputsHorizontal()
    {
        return GetMouseAxis(false);
    }
    /// <summary>
    /// 鼠标Y轴移动
    /// </summary>
    public float GetLookInputsVertical()
    {
        return GetMouseAxis(true);
    }

    /// <summary>
    /// 按下跳跃键
    /// </summary>
    public bool GetJumpInputDown()
    {
        return CanProcessInput()&&InputManager.GetDown(InputState.Jump);
    }

    /// <summary>
    /// 按住跳跃键
    /// </summary>
    public bool GetJumpInputHeld()
    {
        return CanProcessInput() && InputManager.Get(InputState.Jump);
    }

   
    /// <summary>
    /// 按下开火键
    /// </summary>
    public bool GetFireInputDown()
    {
        return CanProcessInput()&& InputManager.GetDown(InputState.Fire);
        //return GetFireInputHeld() && !m_FireInputWasHeld;
    }

    /// <summary>
    /// 松开开火键
    /// </summary>
    public bool GetFireInputReleased()
    {
        return CanProcessInput() && InputManager.GetUp(InputState.Fire);
        //return !GetFireInputHeld() && m_FireInputWasHeld;
    }

    /// <summary>
    /// 按住开火键
    /// </summary>
    public bool GetFireInputHeld()
    {
        return CanProcessInput() && InputManager.Get(InputState.Fire);
    }

    /// <summary>
    /// 按下瞄准键
    /// </summary>
    public bool GetAimInputDown()
    {
        return CanProcessInput() && InputManager.GetDown(InputState.Aim);
        //return GetFireInputHeld() && !m_FireInputWasHeld;
    }

    /// <summary>
    /// 松开瞄准键
    /// </summary>
    public bool GetAimInputReleased()
    {
        return CanProcessInput() && InputManager.GetUp(InputState.Aim);
        //return !GetFireInputHeld() && m_FireInputWasHeld;
    }
    /// <summary>
    /// 按住瞄准键
    /// </summary>
    /// <returns></returns>
    public bool GetAimInputHeld()
    {
        return CanProcessInput() && InputManager.Get(InputState.Aim);
    }

    /// <summary>
    /// 按住冲刺键
    /// </summary>
    /// <returns></returns>
    public bool GetSprintInputHeld()
    {
        return CanProcessInput() && InputManager.Get(InputState.Shift);
    }

    /// <summary>
    /// 双击冲刺键
    /// </summary>
    /// <returns></returns>
    public bool GetSprintInputDouble()
    {
        return CanProcessInput() && InputManager.GetDouble(InputState.Shift);
    }

    /// <summary>
    /// 按下Ctrl键
    /// </summary>
    /// <returns></returns>
    public bool GetCrouchDown()
    {
        return CanProcessInput(GameStateEnum.Game) && InputManager.GetDown(InputState.Crouch);
    }
    /// <summary>
    /// 按住Ctrl键
    /// </summary>
    /// <returns></returns>
    public bool GetCrouch()
    {
        return CanProcessInput(GameStateEnum.Game) && InputManager.Get(InputState.Crouch);
    }
    /// <summary>
    /// 松开Ctrl键
    /// </summary>
    /// <returns></returns>
    public bool GetCrouchUp()
    {
        return CanProcessInput(GameStateEnum.Game) && InputManager.GetUp(InputState.Crouch);
    }
    /// <summary>
    /// 按下投掷键
    /// </summary>
    /// <returns></returns>
    public bool GetThrowDown()
    {
        return CanProcessInput(GameStateEnum.Game) && InputManager.GetDown(InputState.Throw);
    }
    /// <summary>
    /// 按住投掷键
    /// </summary>
    /// <returns></returns>
    public bool GetThrow()
    {
        return CanProcessInput(GameStateEnum.Game) && InputManager.Get(InputState.Throw);
    }
    /// <summary>
    /// 松开投掷键
    /// </summary>
    /// <returns></returns>
    public bool GetThrowUP()
    {
        return CanProcessInput(GameStateEnum.Game) && InputManager.GetUp(InputState.Throw);
    }

    /// <summary>
    /// 按下换弹键
    /// </summary>
    /// <returns></returns>
    public bool GetReloadDown()
    {
        return CanProcessInput() && InputManager.GetDown(InputState.Reload);
    }

  

    /// <summary>
    /// 长按换弹键
    /// </summary>
    /// <returns></returns>
    public bool GetReloadLong()
    {
        return CanProcessInput() && InputManager.GetLong(InputState.Reload);
    }

    /// <summary>
    /// 按下交互键
    /// </summary>
    public bool GetOperateDown()
    {
        return CanProcessInput(WindowStateEnum.Game, true) && InputManager.GetDown(InputState.Operate);
    }

    /// <summary>
    /// 按住交互键
    /// </summary>
    public bool GetOperateHeld()
    {
        return CanProcessInput(WindowStateEnum.Game,true) && InputManager.Get(InputState.Operate);
    }
    /// <summary>
    /// 松开交互键
    /// </summary>
    public bool GetOperateUp()
    {
        return CanProcessInput(WindowStateEnum.Game, true) && InputManager.GetUp(InputState.Operate);
    }

    

    /// <summary>
    /// 按下呼叫凯伊
    /// </summary>
    public bool GetMuleDown()
    {
        return CanProcessInput(WindowStateEnum.Game) && InputManager.GetDown(InputState.Mule);
    }


    // <summary>按下呼叫Kei键</summary>这玩意写在kei自己里面了
    //public bool GetMuleDown() {
    //    return CanProcessInput() && InputManager.GetDown(InputState.Mule);
    //}

    /// <summary>
    /// 获得切换武器键(滚轮实在是用现成体系做不到)
    /// </summary>
    /// <returns></returns>
    public int GetSwitchWeaponInput()
    {
        if (CanProcessInput(WindowStateEnum.Game)&& !InWndPrevent)
        {
            float value= Input.GetAxis("Mouse ScrollWheel");
            //mathf.Sigh=0时也返回1
            if (value > 0) return -1;
            else if (value < 0) return 1;
        }
        return 0;
    }

    /// <summary>
    /// 按下切换至武器键
    /// </summary>
    /// <returns></returns>
    public int GetSelectWeaponInput()
    {
        if (CanProcessInput(GameStateEnum.Game, WindowStateEnum.Game))
        {
            //真的会有人改这个键位吗？
            for(int i= 1,l=5;i<=l;++i)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0+i))
                {
                    //Debug.LogWarning("切换到"+i);
                    return i;
                }
            }

        }
        return 0;
    }


    private static readonly string[] SensitivityKeys = { "水平灵敏度", "垂直灵敏度" };
    private static readonly string[] InvertKeys = { "反转X轴", "反转Y轴" };
    private static readonly string[] AxisKeys = { "Mouse X", "Mouse Y" };

    /// <summary>
    /// 获得鼠标轴
    /// </summary>
    /// <param name="mouseInputName"></param>
    /// <returns></returns>
    float GetMouseAxis(bool isY)
    {
        string mouseInputName = isY ? "" : "";
        
        if (GameRoot.WindowState != WindowStateEnum.FreeCamera&&CanProcessInput())
        {
            int keyPrefix = isY ? 1 : 0;
            
            float speed = GameRoot.GetSetting(SensitivityKeys[keyPrefix]);
            float sigh = GameRoot.GetSetting(InvertKeys[keyPrefix]) > 0 ? -1 : 1;
            float inputValue = Input.GetAxisRaw(AxisKeys[keyPrefix]);
            float i = inputValue * sigh * speed * 0.0001f;

#if UNITY_WEBGL
                // 由于鼠标加速，在WebGL中鼠标往往更敏感，因此请进一步减少它
                i *= 0.3f;
#endif

            return i;
        }

        return 0f;
    }
}
