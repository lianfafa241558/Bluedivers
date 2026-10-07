using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Net;

namespace FPSGame.Managers
{

/// <summary>
/// 舰桥准备与战备选择的消息转发 —— 它就是当年 Photon RPC 的**接缝**。
///
/// <para>▍三条链各司其职（2026-10-06 接通）：</para>
/// <list type="bullet">
///   <item><b>Send*（上行出口）</b>：联机时交给 <see cref="NetRoomFlow"/>（房主本地权威应用 + 广播；
///         成员发给房主）；**单机 / 没入房时**保持原样——直接本地回环，UI 行为不变。</item>
///   <item><b>Receive*（下行入口）</b>：由 <c>TeamNetBridge</c> 在"收到权威转发"后调用（联机），
///         或由 Send* 直接调用（单机）；这里只负责转给 <see cref="FPSGame.GameContract.IBridgeArmamentSink"/>（ArmamentWnd）。</item>
///   <item><b>数据落点</b>：<c>TeamManager.players</c> —— 归**常驻**的 <c>TeamNetBridge</c>；
///         本类只负责"叫醒界面"，不是数据的 owner。</item>
/// </list>
///
/// <para>▍安装点：**只挂在大厅场景 <c>Assets/Scene/Utnapishitim.unity</c>**（与 <c>ArmamentWnd</c> 同场景，
/// 两者本就是一体的：<c>ArmamentWnd.Init()</c> 反过来要写 <c>BridgeSys.Instance.armament</c>）。
/// 舰桥准备/战备/强化这三条链的触发点全是 <c>ArmamentWnd</c> 上的点击 ⇒ 该场景就是它们唯一的舞台，
/// **不需要**挂到 GameRoot 常驻（挂了反而会与场景里那份构成单例重复）。
/// 代价是：任何**跨场景**调用点都必须判空（<c>TeamNetBridge</c> 里已按 <c>?.Invoke</c> 处理）。</para>
/// </summary>
[AddComponentMenu("管理/舰桥系统")]
/// <para>⚠ 基类用**全限定** <c>FPSGame.Core.Singleton</c>：RootMotion 插件里也有一个全局命名空间的
/// <c>Singleton&lt;T&gt;</c>，不限定会歧义。</para>
public class BridgeSys : FPSGame.Core.Singleton<BridgeSys>
{
    public FPSGame.GameContract.IBridgeArmamentSink armament;

    /// <summary>本机是否处于"联机房间"里（房主或已入房的成员）。⚠ 只有舰桥场景有本组件，
    /// 所以它能回答的是"**在舰桥时**是否联机"；战斗场景要靠别的信号（如玩家数）。</summary>
    public static bool InOnlineRoom
    {
        get
        {
            var flow = NetRoomFlow.Instance;
            if (flow == null) return false;
            return flow.IsHost || flow.SelfSid != 0u;
        }
    }

    /// <summary> 发送玩家选择战备的消息（联机走网络，上位机权威转发后回到 Receive*）</summary>
    public void SendPlayerSelectArmament(int playerIndex, int id, int index)
    {
        if (InOnlineRoom)
        {
            NetRoomFlow.Instance.SendArmament(playerIndex, id, index);
            return;
        }
        // 单机：老行为（RPC 被注释掉时的本地回环）
        ReceivePlayerSelectArmament(playerIndex, id, index);
    }

    //[PunRPC]
    /// <summary> 收到玩家选择战备的回调</summary>
    public void ReceivePlayerSelectArmament(int playerIndex,int id,int index)
    {
        if (armament == null) return;
        armament.ReceivePlayerSelectAemament(playerIndex, id, index);
    }

    /// <summary> 发送玩家选择全队强化的消息</summary>
    public void SendPlayerSelectTeamEnhance(int playerIndex, int id)
    {
        if (InOnlineRoom)
        {
            NetRoomFlow.Instance.SendBooster(playerIndex, id);
            return;
        }
        ReceivePlayerSelectTeamEnhance(playerIndex, id);
    }

    //[PunRPC]
    /// <summary> 收到玩家选择全队强化的回调</summary>
    public void ReceivePlayerSelectTeamEnhance(int playerIndex, int id)
    {
        if (armament == null) return;
        armament.ReceivePlayerSelectTeamEnhance(playerIndex, id);
    }

    /// <summary> 发送玩家准备的消息</summary>
    public void SendPlayerReady(int playerIndex, bool state)
    {
        var flow = NetRoomFlow.Instance;
        if (flow != null && flow.IsHost)
        {
            // 房主的就绪与成员**同一口径**（没有特权，转场要全员就绪）：写进房主权威表并广播，成员才看得见
            //（含"房主取消就绪"）；成员那份走 ReadyState → OnReadyState。
            NetHostSvc.Instance?.SetHostReady(state);
            ReceivePlayerReady(playerIndex, state);
            return;
        }
        if (flow != null && flow.SelfSid != 0u)
        {
            flow.SendReady(state);   // 成员：上报房主 → 房主广播名单 → 桥回推 ReceivePlayerReady
            return;
        }
        ReceivePlayerReady(playerIndex, state);   // 单机
    }

    
    /// <summary> 收到玩家准备的回调</summary>
    public void ReceivePlayerReady(int playerIndex, bool state)
    {
        if (armament == null) return;
        if (armament is UnityEngine.MonoBehaviour mb && mb == null) return;   // 已销毁的窗口
        armament.ReceivePlayerReady(playerIndex, state);
    }

    /// <summary> 名册变化（有人加入/退出）的回调：让战备界面刷新槽位（退出的人那一行要消失）</summary>
    public void ReceiveRosterChanged()
    {
        if (armament == null) return;
        if (armament is UnityEngine.MonoBehaviour mb && mb == null) return;   // 已销毁的窗口
        armament.ReceiveRosterChanged();
    }

}
}
