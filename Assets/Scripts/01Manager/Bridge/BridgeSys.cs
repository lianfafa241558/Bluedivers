using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class BridgeSys : SingletonNet<BridgeSys>
{
    public ArmamentWnd armament;


    public void Start()
    {
        armament = WndManager.Instance.armamentWnd;
    }



    /// <summary> 发送玩家选择战备的消息 </summary>
    public void SendPlayerSelectArmament(int playerIndex, int id, int index)
    {
        RPC(ReceivePlayerSelectArmament, RpcTarget.All, playerIndex, id, index);
    }

    [PunRPC]
    /// <summary> 收到玩家选择战备的回调 </summary>
    public void ReceivePlayerSelectArmament(int playerIndex,int id,int index)
    {
        armament.ReceivePlayerSelectAemament(playerIndex, id, index);
    }

    /// <summary> 发送玩家准备的消息 </summary>
    public void SendPlayerReady(int playerIndex, bool state)
    {
        RPC(ReceivePlayerReady, RpcTarget.All, playerIndex, state);
    }

    [PunRPC]
    /// <summary> 收到玩家准备的回调 </summary>
    public void ReceivePlayerReady(int playerIndex, bool state)
    {
        armament.ReceivePlayerReady(playerIndex, state);
    }

}
