using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;

/// <summary>
/// 初始随机旋转
/// </summary>
public class StartRandomRorate : MonoBehaviour
{
    
    void Start()
    {
        transform.eulerAngles=new(transform.eulerAngles.x, RandomUtils.Range(0,360), transform.eulerAngles.z);
        Destroy(this);
    }

}
