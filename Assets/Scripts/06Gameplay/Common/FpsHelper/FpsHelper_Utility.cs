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

    /// <summary>
    /// 根据根骨骼和末端骨骼，手动更新蒙皮网格的包围盒
    /// </summary>
    /// <param name="smr">目标蒙皮网格渲染</param>
    /// <param name="endBone">管道末端骨骼</param>
    /// <param name="boundsExpand">包围盒向外扩大值（适配管道粗细</param>
    public static void UpdatePipeBounds(SkinnedMeshRenderer smr, Transform endBone, float expand = 1f)
    {
        // 参数校验
        if (smr == null || endBone == null)
        {
            Debug.LogError("参数错误", smr);
            return;
        }

        Transform bone = endBone;

        // 初始化包围盒（以第一个骨骼为起点）
        Bounds bounds = new Bounds(smr.transform.InverseTransformPoint(bone.position), Vector3.zero);
        bone = bone.parent;
        while (bone != null && bone != smr.rootBone)
        {
            bounds.Encapsulate(smr.transform.InverseTransformPoint(bone.position));
            bone = bone.parent;
        }
        bounds.center = new(-bounds.center.z, bounds.center.y, bounds.center.x);
        bounds.size = new(bounds.size.z, bounds.size.y, bounds.size.x);
        // 扩展并赋值
        bounds.Expand(expand);
        smr.localBounds = bounds;
    }

    public static Sprite CameraCaptureToSprite(Camera targetCamera)
    {
        // ⚠ 必须先判相机：`Camera.main` 在"没有 tag=MainCamera 的激活相机"的场景/阶段会返回 null
        //   （实测：撤离结束切到 Armament 后按 ESC 开设置窗，战斗相机已失效）⇒
        //   原样往下走会在 `targetCamera.targetTexture = rt` 抛 NullReferenceException（2026-10-07 实测）。
        if (targetCamera == null) return null;

        // 创建RenderTexture
        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        //int rewordMask = targetCamera.cullingMask;
        //targetCamera.cullingMask|= LayerDefinition.WeaponLayers;
        targetCamera.targetTexture = rt;
        targetCamera.Render();

        // 转换为Texture2D
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        // 生成Sprite
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        targetCamera.targetTexture = null;
        //targetCamera.cullingMask = rewordMask;
        sprite.name = "抓取";
        return sprite;
    }

}
}
