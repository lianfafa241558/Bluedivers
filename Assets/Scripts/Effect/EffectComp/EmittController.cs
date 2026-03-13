using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;

/// <summary>
/// 光环的发光变化控制器
/// </summary>
public class EmittController : MonoBehaviour
{
    [Range(0, 1)]
    public float emittScale=0.5f;
    [Range(0, 2)]
    public float speedScale=1;

    private float scale = 1;
    private MpbController mpb;

    void Start()
    {
        mpb = new(transform);
    }

    void LateUpdate()
    {
        scale = 1 + emittScale * Mathf.Sin(Time.time* speedScale);
        mpb.Set("_EmissionScale", scale).Apply();
    }

}
