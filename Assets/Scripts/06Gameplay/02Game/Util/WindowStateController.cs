using FPSGame.Core;
using UnityEngine;

namespace FPSGame.Gameplay
{

/// <summary>
/// 按窗口状态切换一组物体的显隐。
/// <para>状态匹配与派发复用 <see cref="StateEventDispatch"/>（与 GameStateController 共用一份逻辑）。</para>
/// </summary>
[AddComponentMenu("框架/窗口状态显示")]
public class WindowStateController : MonoBehaviour
{
    [SerializeField]
    private ChangeItem[] arr;

    private void Awake()
    {
        FPSGame.GameContract.ServiceLocator.Wnd.OnWindowStateChange += WindowStateChange;

    }
    private void OnDestroy()
    {
        FPSGame.GameContract.ServiceLocator.Wnd.OnWindowStateChange -= WindowStateChange;
    }

    private void WindowStateChange(WindowStateEnum exit, WindowStateEnum entry)
    {
        StateEventDispatch.Dispatch(arr, exit, entry);
    }

    /// <summary>单行配置：命中"退出 state"或"进入 state"时执行 funs</summary>
    [System.Serializable]
    private struct ChangeItem : StateEventDispatch.IItem<WindowStateEnum>
    {
        public WindowStateEnum state;
        public bool isExit;
        public UnityEngine.Events.UnityEvent funs;

        WindowStateEnum StateEventDispatch.IItem<WindowStateEnum>.State => state;
        bool StateEventDispatch.IItem<WindowStateEnum>.IsExit => isExit;
        void StateEventDispatch.IItem<WindowStateEnum>.Invoke() => funs?.Invoke();
    }
}
}
