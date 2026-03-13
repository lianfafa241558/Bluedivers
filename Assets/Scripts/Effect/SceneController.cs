using System.Collections.Generic;
using Core;
using Unity.BaseTool;
using UnityEngine;

public class SceneController : MonoBehaviour
{
    [SerializeField]
    private DisplayDic<GameStateEnum, GameStateController> ControlDic;

    void Start()
    {
        GameRoot.OnGameStateChange += OnStartTask;
    }
    private void OnDestroy()
    {
        GameRoot.OnGameStateChange -= OnStartTask;
    }

    private void OnStartTask(GameStateEnum exit, GameStateEnum entry)
    {
        var info = ControlDic[entry];

        foreach (var item in info.animControl)
        {
            if (string.IsNullOrEmpty(item.Value)) item.Key.enabled = true;
            else
            {
                item.Key.enabled = true;
                item.Key.Play(item.Value);
            }
        }

        foreach (var item in info.showControl)
        {
            item.Key.SetActive(item.Value);
        }


    }

    private void LoadSceen()
    {
        ResManager.Instance.AsyncLoadScene("TestScene", () => {
            BattleManager.Creat();
            GameRoot.GameState = GameStateEnum.Game;
            GameRoot.WindowState = WindowStateEnum.Game;
            //GlobalEventManager.OnFakeBg(null);
        },true);
    }


    private void AllowSwitchSceen()
    {
        ResManager.Instance.AsyncContinueLoadScene();
    }

    [System.Serializable]
    private class GameStateController
    {
        public List<KVP<Animator, string>> animControl;
        public List<KVP<GameObject, bool>> showControl;
    }

}
