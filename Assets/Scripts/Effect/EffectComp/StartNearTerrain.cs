using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 初始紧贴地面，不需要给actor用(自带了)
/// </summary>
public class StartNearTerrain : MonoBehaviour
{

    void Start()
    {
        transform.position = TerrainUtils.WSToTS(transform.position);
        Destroy(this);
    }
}
