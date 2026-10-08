using FPSGame.Core;
using UnityEngine;

namespace FPSGame.Gameplay
{

/// <summary>
/// 按游戏状态切换一组物体的显隐。
/// <para>状态匹配与派发复用 <see cref="StateEventDispatch"/>（与 WindowStateController 共用一份逻辑）。</para>
/// </summary>

public class GameStateController : MonoBehaviour
{
    [SerializeField]
    private ChangeItem[] arr;

    private void Awake()
    {
        GlobalEventBus.OnGameStateChange += GameStateChange;

    }
    private void OnDestroy()
    {
        GlobalEventBus.OnGameStateChange -= GameStateChange;
    }

    private void GameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        StateEventDispatch.Dispatch(arr, exit, entry);
    }

    /// <summary>单行配置：命中"退出 state"或"进入 state"时执行 funs</summary>
    [System.Serializable]
    private struct ChangeItem : StateEventDispatch.IItem<GameStateEnum>
    {
        public GameStateEnum state;
        public bool isExit;
        public UnityEngine.Events.UnityEvent funs;

        GameStateEnum StateEventDispatch.IItem<GameStateEnum>.State => state;
        bool StateEventDispatch.IItem<GameStateEnum>.IsExit => isExit;
        void StateEventDispatch.IItem<GameStateEnum>.Invoke() => funs?.Invoke();
    }
}
}
