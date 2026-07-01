using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

public class GameStateController : MonoBehaviour
{
    [SerializeField]
    private ChangeItem[] arr;

    private void Awake()
    {
        GlobalEventSub.OnGameStateChange += GameStateChange;

    }
    private void OnDestroy()
    {
        GlobalEventSub.OnGameStateChange -= GameStateChange;
    }

    private void GameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        for (int i = 0; i < arr.Length; ++i)
        {
            if (exit == arr[i].state && arr[i].isExit)
            {
                //Debug.LogError("退出状态" + exit + gameObject, gameObject);
                arr[i].funs?.Invoke();
                //break;
            }
            else if (entry == arr[i].state && !arr[i].isExit)
            {
                //Debug.LogError("进入状态" + entry + gameObject,gameObject);
                arr[i].funs?.Invoke();
                //break;
            }
        }
    }

    [System.Serializable]
    private struct ChangeItem
    {
        public GameStateEnum state;
        public bool isExit;
        public UnityEngine.Events.UnityEvent funs;
    }
}
