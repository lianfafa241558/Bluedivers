using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;

/// <summary>
/// 光环控制器
/// </summary>
public class HaloController : MonoBehaviour
{

    public Transform parent;
    public Vector3 dv;
    public Vector3 lastPos;


    [InspectorName("速度")]
    public float speed =10;

    [InspectorName("回正速度")]
    public float returnSpeed =10;

    /*
        TransformDirection 返回相对位置
        TransformPoint 返回这个点加相对点之后的世界坐标
        TransformVector 返回相对位置(考虑缩放)
    */

    void Start()
    {
        if (!parent) parent = transform.parent;
        //dv = parent.TransformDirection(transform.position- parent.position);
        dv = transform.localPosition;
        lastPos = parent.position;

    }

        // Update is called once per frame
    void LateUpdate()
    {
        Vector3 target = parent.TransformPoint(dv)+(lastPos - parent.position);
        if (Vector3.Distance(target,parent.position)<3) {
            //跟随玩家移动
            transform.position = Vector3.Lerp(transform.position, target, returnSpeed * Time.deltaTime);

            lastPos = Vector3.Lerp(lastPos, parent.position, speed * Time.deltaTime);
        }
        else {//太远疑似瞬移的视角直接跟
            transform.position = parent.TransformPoint(dv);
            lastPos = parent.position;
        }


    }
}
