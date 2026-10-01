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
    public static void TryMove(this CharacterController Controller,Vector3 value,bool isTeleport=false)
    {
        if (Controller.enabled)
        {
            if (isTeleport) Controller.Teleport(value);
            else Controller.Move(value);
        }
    }


   
    /// <summary>
    /// 向指定方向传送，忽略路径障碍，允许空中，但避免卡进地下
    /// </summary>
    public static void Teleport(this CharacterController controller, Vector3 direction)
    {
        Vector3 targetPosition = controller.transform.position + direction;

        // 先检测地下再移动，避免 CharacterController 自身的碰撞体干扰 SphereCast
        Vector3 finalPosition = PreventUnderground(controller, targetPosition);
        controller.transform.position = finalPosition + 0.2f * Vector3.up;
        Physics.SyncTransforms();
    }

    /// <summary>
    /// 防止角色卡进地下（修正Y轴位置）
    /// 先用地形高度保底，再用 SphereCast 检测建筑物等人工结构
    /// </summary>
    private static Vector3 PreventUnderground(CharacterController controller, Vector3 position)
    {
        float halfHeight = controller.height * 0.5f;
        float radius = controller.radius * 0.9f;
        float skinWidth = 0.01f;
        float maxCheckDistance = 100f;
        LayerMask groundMask = LayerDefinition.GroundLayers;
        RaycastHit hit;

        // === 第一步：地形优先 ===
        float terrainSurfaceY = TerrainUtils.Main != null ? TerrainUtils.Main.WSToHeight(position) : float.MinValue;
        float terrainMinY = terrainSurfaceY - skinWidth;

        if (TerrainUtils.Main != null)
        {
            // 目标在地形以下 → 直接修正到地形表面
            if (position.y < terrainMinY)
            {
                float correctedY = terrainSurfaceY + halfHeight + skinWidth;
                return new Vector3(position.x, correctedY, position.z);
            }
        }

        // === 第二步：SphereCast 向下检测（建筑物等人工结构）===
        // 注：能走到这里的 position.y 一定在地形以上（Step 1 已处理地下），
        // 所以 origin = position + (halfHeight + radius) 不会从地形内部出发
        Vector3 origin = position + Vector3.up * (halfHeight + radius);

        if (Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out hit,
            maxCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            float groundDistance = hit.distance - halfHeight - radius;

            // 仅在紧贴/低于地面时修正（正常空中保持原位）
            if (groundDistance < skinWidth)
            {
                float sphereCastY = hit.point.y + halfHeight + skinWidth;
                float bestY = TerrainUtils.Main != null
                    ? Mathf.Max(sphereCastY, terrainSurfaceY + halfHeight + skinWidth)
                    : sphereCastY;
                return new Vector3(position.x, bestY, position.z);
            }
            // 空中正常 → 不修正
            return position;
        }

        // === 第三步：SphereCast 向上检测（完全在地下时的兜底）===
        origin = position + Vector3.up * (radius + 0.01f);

        if (Physics.SphereCast(
            origin,
            radius,
            Vector3.up,
            out hit,
            maxCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            float correctedY = hit.point.y + halfHeight + skinWidth;
            return new Vector3(position.x, correctedY, position.z);
        }

        // === 第四步：最终兜底 ===
        if (TerrainUtils.Main != null && position.y < terrainMinY)
        {
            float correctedY = terrainSurfaceY + halfHeight + skinWidth;
            return new Vector3(position.x, correctedY, position.z);
        }

        return position;
    }



}
}
