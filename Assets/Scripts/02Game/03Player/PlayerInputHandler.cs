using Core;
using FPSGame.Attribute;
using FPSGame.Furn;

using UnityEngine;

//相关的设置都在项目-设置-输入管理器里面


public class PlayerInputHandler : MonoBehaviour
{
    /// <summary>跳跃被占用</summary>
    [InspectorName("跳跃被占用")]
    [DisplayField]
    public bool useJump;

    /// <summary>正在和物体交互</summary>
    [InspectorName("正在和物体交互")]
    [DisplayField]
    public bool InOperation;
    // <summary>正在被窗口阻止</summary>
    //[InspectorName("正在被窗口阻止")]
    //[DisplayField]
    //public bool InWndPrevent;
    [SerializeField]
    private bool debugging;

    private float LastEndOperateTime;

    //PlayerController m_PlayerCharacterController;
    /*
    private void Awake()
    {
        Debug.LogWarning("输入组件的Awake");
    }
    */
    void Start()
    {
        GlobalEventSub.OnFurnitureOperate += OnOperation;
        //m_PlayerCharacterController = GetComponent<PlayerController>();
        //GlobalEventManager.OnWndSwitch += OnWndSwitch;

        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
        //Debug.LogWarning("输入组件的Start");
        if (debugging) Cursor.lockState = CursorLockMode.Locked;
    }
    void OnDestroy()
    {
        GlobalEventSub.OnFurnitureOperate -= OnOperation;
        //GlobalEventManager.OnWndSwitch -= OnWndSwitch;
    }

    string[] preventWnds = new string[] { "MiniMapWnd" };
    int preventWndCount;
    /*
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
    }*/


    void OnOperation(GameObject user,IFurniture furn)
    {
        bool switchState = furn.HaveFlag(FurnitureFlag.SwitchState);
        if (user == gameObject && switchState)
        {
            InOperation = furn.InOperate;
            if (!InOperation) LastEndOperateTime = Time.time;
        }
    }

    /// <summary>
    /// 允许输入操作
    /// </summary>
    public bool CanProcessInput(bool allowOperation=false)
    {
        return debugging||((allowOperation||!InOperation) && (Time.time - LastEndOperateTime)>0.5f && Cursor.lockState == CursorLockMode.Locked);
    }
    public bool CanProcessInput(GameStateEnum state, bool allowOperation = false)
    {
        return CanProcessInput(allowOperation) && GameRoot.GameState == state;
    }
    public bool CanProcessInput(WindowStateEnum winState, bool allowOperation = false)
    {
        return CanProcessInput(allowOperation) && WndManager.WindowState == winState;
    }
    public bool CanProcessInput(GameStateEnum gameState,WindowStateEnum winState, bool allowOperation = false)
    {
        return CanProcessInput(allowOperation) && GameRoot.GameState == gameState &&WndManager.WindowState == winState;
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
    /// 长按跳跃键
    /// </summary>
    /// <returns></returns>
    public bool GetJumpInputLong(float time)
    {
        return CanProcessInput() && InputManager.GetLong(InputState.Jump,time);
    }

    /// <summary>
    /// 松开跳跃键
    /// </summary>
    public bool GetJumpInputUp(bool ignoreUse=false)
    {
        return CanProcessInput() &&(!useJump||ignoreUse)&& InputManager.GetUp(InputState.Jump);
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
    /// 按下呼叫凯键
    /// </summary>
    public bool GetMuleDown()
    {
        return CanProcessInput(WindowStateEnum.Game) && InputManager.GetDown(InputState.Mule);
    }

    /// <summary>
    /// 按下切换视角键（T键）
    /// </summary>
    public bool GetToggleViewDown()
    {
        return CanProcessInput(WindowStateEnum.Game) && Input.GetKeyDown(KeyCode.T);
    }


    /// <summary>
    /// 获得切换武器号 滚轮实在是用现成体系做不了
    /// </summary>
    /// <returns></returns>
    public int GetSwitchWeaponInput()
    {
        if (CanProcessInput(WindowStateEnum.Game))// && !InWndPrevent)
        {
            float value= Input.GetAxis("Mouse ScrollWheel");
            //mathf.Sign=0时也返回1
            //Debug.Log("切换成功" + value);

            if (value > 0) return -1;
            else if (value < 0) return 1;
         }
        //else
        //{
        //    Debug.Log("切换失败" + CanProcessInput(WindowStateEnum.Game)+"  "+(InWndPrevent));
        //}
            return 0;
    }

    /// <summary>
    /// 按下切换至武器键
    /// </summary>
    /// <returns></returns>
    public int GetSelectWeaponInput()
    {
        //也就是说大厅不能数字切换武器
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
    /// 获得鼠标输入
    /// </summary>
    /// <param name="mouseInputName"></param>
    /// <returns></returns>
    float GetMouseAxis(bool isY)
    {
        string mouseInputName = isY ? "" : "";
        if (WndManager.WindowState == WindowStateEnum.FreeCamera || !CanProcessInput()) return 0;

        int keyPrefix = isY ? 1 : 0;
        float speed = 100;
        float sigh = 1;
        float inputValue = Input.GetAxisRaw(AxisKeys[keyPrefix]);

        if (ArchiveSvc.Instance)
        {
             speed = ArchiveSvc.GetSetting(SensitivityKeys[keyPrefix]);
             sigh = ArchiveSvc.GetSetting(InvertKeys[keyPrefix]) > 0 ? -1 : 1;
        }
        float i = inputValue * sigh * speed * 0.01f;

#if UNITY_WEBGL
            // 由于鼠标加速，在WebGL中鼠标往往更敏感，因此请进一步减少它
            i *= 0.3f;
#endif

        return i;
  
    }
}
