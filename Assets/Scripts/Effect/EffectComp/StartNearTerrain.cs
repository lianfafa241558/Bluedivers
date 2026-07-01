using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 初始紧贴地面，不需要给actor,自带
/// </summary>
public class StartNearTerrain : MonoBehaviour
{

    void Start()
    {
        transform.position = TerrainUtils.WSToTS(transform.position);
        Destroy(this);
    }
}
