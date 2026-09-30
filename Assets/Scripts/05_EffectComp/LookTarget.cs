
using UnityEngine;

namespace FPSGame.EffectComp
{
/// <summary>
/// 注视目标
/// </summary>
[AddComponentMenu("特效/朝向目标")]
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
}
