using System;
using System.Collections;
using System.Collections.Generic;
using GameContract;
using TMPro;
using UnityEngine;

public class VFXHaloEffect : MonoBehaviour,VfxEffect
{

    public void SetOwner(GameObject owner, GameObject weaponRoot, Collider collider,Vector3 point)
    {
        if (!collider)
        {
            Debug.LogError("目标物体不存在 "+collider+" owner"+owner,gameObject);
        }
        else GlobalEventSub.Mark(owner, collider.gameObject, point);
    }
    

}
