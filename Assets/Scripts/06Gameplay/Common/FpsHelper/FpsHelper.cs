using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.MapUtils;
using FPSGame.GameContract;
using PEMaths;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;
using FPSGame.Utils;

namespace FPSGame.Gameplay
{
public static partial class FpsHelper
{
    static FpsHelper()
    {
        //bulletHoles = ResSvc.Instance.LoadObjects<GameObject>("VFX/Weapon/BulletHole/Base");
        bulletHoles = Resources.LoadAll<GameObject>("VFX/BulletHole/Base").ToList();
    }


    public static Vector3 PlayerCameraLookPoint { get;private set; }

    public static PEVector3 PlayerCameraLookLogicPoint { get; private set; }

    public static void SetPlayerCameraLookPoint(Vector3 pos)
    {
        PlayerCameraLookPoint = pos;
        PlayerCameraLookLogicPoint = new(pos);
    }

    /// <summary>
    /// 取"**某个单位**的准心实指那一点"（世界空间）。
    ///
    /// <para>▍为什么需要它（2026-10-13）：制导类投射物（<c>ProjectilePlayerHoming</c>）要每帧知道"射手现在瞄哪"。
    /// 本机玩家有相机；而**盟友实例**（<c>FriendController</c>、`PlayerFriend.prefab`）刻意没有相机
    /// ⇒ 原来那颗远端表现弹取不到制导源，只能直线飞到射程尽头（"客机的制导火箭弹不同步"）。这里统一收口：</para>
    /// <list type="number">
    ///   <item>有 <c>PlayerController.PlayerCamera</c> ⇒ 用它的屏幕中心射线（与 <c>PlayerController.LookPointHandle</c> 同口径）；</item>
    ///   <item>否则有 <c>FriendController</c> ⇒ 用它**已同步的姿态**（yaw + pitch）本地重算（见 <c>FriendController.TryGetAimRay</c>）
    ///         ⇒ **不需要任何新同步字段**。</item>
    /// </list>
    ///
    /// <para>⚠ 别在读 <see cref="PlayerCameraLookPoint"/> 的场合用它：那个静态量的语义是"**本机玩家**的点"，
    /// 远端单位必须走本方法（否则会朝"我的准心"修正方向）。</para>
    /// </summary>
    /// <param name="maxDistance">射线最大距离（打空时目标点取射线该距离处）。</param>
    /// <param name="layerSpeed">命中层集合的挑选参数（高速弹不能穿盾，见 <see cref="GetHittableLayers"/>）。</param>
    /// <returns>false = 取不到瞄准源（舰桥 / 单位未就绪）⇒ 调用方应保持原行为（不制导）。</returns>
    public static bool TryGetAimPoint(GameObject owner, out Vector3 point, float maxDistance = 300f, float layerSpeed = 99f)
    {
        point = Vector3.zero;
        if (owner == null) return false;

        // ① 本机玩家：相机屏幕中心
        var pc = owner.GetComponent<PlayerController>();
        var cam = pc != null ? pc.PlayerCamera : null;
        if (cam != null)
        {
            point = RaycastPoint(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), maxDistance, layerSpeed);
            return true;
        }

        // ② 远端盟友：没有相机 ⇒ 用已同步姿态本地重算
        var friend = owner.GetComponentInParent<FriendController>();
        if (friend != null && friend.TryGetAimRay(out Vector3 origin, out Vector3 dir))
        {
            point = RaycastPoint(new Ray(origin, dir), maxDistance, layerSpeed);
            return true;
        }

        return false;
    }

    /// <summary>射线采样一个目标点（没打到东西就给射程尽头）—— 本文件里几条取点路径共用。</summary>
    static Vector3 RaycastPoint(Ray ray, float maxDistance, float layerSpeed)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, GetHittableLayers(layerSpeed), QueryTriggerInteraction.Collide))
            return hit.point;
        return ray.origin + ray.direction * maxDistance;
    }



    private static List<GameObject> bulletHoles;

    /// <summary>
    /// 是否是主要阶段（战斗中、准备、过渡）
    /// </summary>
    public static bool IsMainStage()
    {
        // 走「数据自持」（2026-10-01 取代 ServiceLocator.Flow）：GameRoot 在 09Manager 发布，本层直读。
        // ⚠ 原来的 `flow != null &&` 是**死判空**（槽位永不 null，空对象返回 false）⇒ 已清掉。
        return FPSGame.Data.FlowState.IsMainStage;
    }


}
}
