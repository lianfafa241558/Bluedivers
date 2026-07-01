/*
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
/// <summary>
/// 残骸(单位死亡时刷新一个模型)
/// </summary>
public class Wreckage : MonoBehaviour
{
    public GameObject go;
    private void OnDisable()
    {
#if UNITY_EDITOR
        if (EditorApplication.isPlaying)
#else
        if (Application.isPlaying)
#endif
        {
            var go=Instantiate(this.go, transform.position, transform.rotation);
            if(go.TryGetComponent<SkinnedMeshRenderer>(out var smr))
            {
                var mr=go.AddComponent<MeshRenderer>();
                mr.materials = smr.materials;
                var mf = go.AddComponent<MeshFilter>();
                mf.mesh = smr.sharedMesh;
                Destroy(smr);
            }
        } 
    }
}
*/