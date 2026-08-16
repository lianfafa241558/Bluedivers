using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class BridgeSys : SingletonNet<BridgeSys>
{
    public ArmamentWnd armament;



    /// <summary> 发送玩家选择战备的消息</summary>
    public void SendPlayerSelectArmament(int playerIndex, int id, int index)
    {
        RPC(ReceivePlayerSelectArmament, RpcTarget.All, playerIndex, id, index);
    }

    [PunRPC]
    /// <summary> 收到玩家选择战备的回调</summary>
    public void ReceivePlayerSelectArmament(int playerIndex,int id,int index)
    {
        armament.ReceivePlayerSelectAemament(playerIndex, id, index);
    }

    /// <summary> 发送玩家选择全队强化的消息</summary>
    public void SendPlayerSelectTeamEnhance(int playerIndex, int id)
    {
        RPC(ReceivePlayerSelectTeamEnhance, RpcTarget.All, playerIndex, id);
    }

    [PunRPC]
    /// <summary> 收到玩家选择全队强化的回调</summary>
    public void ReceivePlayerSelectTeamEnhance(int playerIndex, int id)
    {
        armament.ReceivePlayerSelectTeamEnhance(playerIndex, id);
    }

    /// <summary> 发送玩家准备的消息</summary>
    public void SendPlayerReady(int playerIndex, bool state)
    {
        RPC(ReceivePlayerReady, RpcTarget.All, playerIndex, state);
    }

    [PunRPC]
    /// <summary> 收到玩家准备的回调</summary>
    public void ReceivePlayerReady(int playerIndex, bool state)
    {
        armament.ReceivePlayerReady(playerIndex, state);
    }

}
