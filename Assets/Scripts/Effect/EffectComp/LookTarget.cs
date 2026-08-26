
using UnityEngine;
/// <summary>
/// 注视目标
/// </summary>
internal class LookTarget : MonoBehaviour
{
    [SerializeField]
    Transform target;

    [SerializeField]
    LineRenderer line;

    private void LateUpdate()
    {
        transform.LookAt(target);
        if (line != null)
        {
            line.SetPosition(0, transform.position);
            line.SetPosition(1, target.position);
        }
    }

}