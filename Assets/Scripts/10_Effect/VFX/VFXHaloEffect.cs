using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.GameContract;
using TMPro;
using UnityEngine;
using FPSGame.Gameplay;

namespace FPSGame.Effect
{

/// <summary>
/// 光晕类特效的播放控制。
/// </summary>
[AddComponentMenu("特效/光晕特效")]
public class VFXHaloEffect : MonoBehaviour,IVfxEffect
{

    public void SetOwner(GameObject owner, GameObject weaponRoot, Collider collider,Vector3 point)
    {
        if (!collider)
        {
            //Debug.LogError("目标物体不存在 "+collider+" owner"+owner,gameObject);
        }
        else GlobalEventSub.Mark(owner, collider.gameObject, point);
    }
    

}
}
