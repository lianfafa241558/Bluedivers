using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;

public class TransSceneController : MonoBehaviour
{
    /// <summary>
    /// 事件调用
    /// </summary>
    public void StartLoad()
    {
        
        AudioSvc.PlayMusic("Shooting Athletes", 0.3f);
        ResSvc.Instance.AsyncLoadScene("TestScene", () => {
            BattleManager.Creat(true);
        }, true,false);

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        transform.position = Vector3.up * 500;

    }

    public void Destroy()
    {
        Tool.Destroy(gameObject);
    }

    /// <summary>
    /// 动画调用
    /// </summary>
    private void AllowSwitchSceen()
    {
        ResSvc.Instance.AsyncContinueLoadScene();
    }

}
