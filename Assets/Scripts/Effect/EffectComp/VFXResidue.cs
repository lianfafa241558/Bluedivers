using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物体死亡时将残留物转移出去
/// </summary>
public class VFXResidue : MonoBehaviour
{
    public List<Transform> residue;
    public bool isMove;
    private void OnDestroy() {
        if (isMove) {
            residue.ForEach(item => item.SetParent(null));
        }
        else {
            residue.ForEach(item => Instantiate(item, item.position, item.rotation, null));
        }
    }
}
