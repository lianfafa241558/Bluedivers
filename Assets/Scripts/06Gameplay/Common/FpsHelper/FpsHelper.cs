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



    private static List<GameObject> bulletHoles;

    /// <summary>
    /// 是否是主要阶段（战斗中、准备、过渡）
    /// </summary>
    public static bool IsMainStage()
    {
        // 走「数据自持」（2026-10-01 取代 ServiceLocator.Flow）：GameRoot 在 01Manager 发布，本层直读。
        // ⚠ 原来的 `flow != null &&` 是**死判空**（槽位永不 null，空对象返回 false）⇒ 已清掉。
        return FPSGame.Data.FlowState.IsMainStage;
    }


}
}
