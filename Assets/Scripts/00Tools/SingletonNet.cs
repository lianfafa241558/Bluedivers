using System;
using Photon.Pun;
using UnityEngine;
using Utils;

public class SingletonNet<T> : MonoBehaviourPunCallbacks where T : SingletonNet<T>
{
    /// <summary>
    /// 外部访问，公共静态成员（单例）
    /// </summary>
    private static T instance;
    
    public static T Instance
    {
        get {
            return instance; 
        }
    }

    public virtual void Awake()
    {
        if (instance != null&& instance!=this)
        {
            Debug.Log(gameObject.name+"已有相同物体"+instance.gameObject.name);
            Tool.Destroy(gameObject);
        }
        else
        {
            instance = (T)this;
        }
        
    }

    public static bool isInit()
    {
        if (instance) return true;
        return false;
    }

    protected virtual void onDestroy ()
    {
        if (instance == this) instance = null;
    }


    public bool RPC(Action action, RpcTarget target)
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC(nameof(action), target, default);
            return true;
        }
        else
        {
            action.Invoke();
            return false;
        }
    }
    public bool RPC<T1>(Action<T1> action, RpcTarget target, T1 parameter)
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC(nameof(action), target, action.Method.Name, parameter);
            return true;
        }
        else
        {
            action.Invoke(parameter);
            return false;
        }
    }
    public bool RPC<T1, T2>(Action<T1, T2> action, RpcTarget target, T1 p1, T2 p2)
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC(nameof(action), target, action.Method.Name, p1, p2);
            return true;
        }
        else
        {
            action.Invoke(p1, p2);
            return false;
        }
    }

    public bool RPC<T1, T2, T3>(Action<T1, T2, T3> action, RpcTarget target, T1 p1, T2 p2, T3 p3)
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC(nameof(action), target, action.Method.Name, p1, p2, p3);
            return true;
        }
        else
        {
            action.Invoke(p1, p2, p3);
            return false;
        }
    }

}
