using FPSGame.Core;
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.EffectComp
{

/// <summary>
/// 初始随机旋转
/// </summary>
[AddComponentMenu("特效/随机初始朝向")]
public class StartRandomRorate : MonoBehaviour
{
    
    void Start()
    {
        transform.eulerAngles=new(transform.eulerAngles.x, RandomUtils.Range(0,360), transform.eulerAngles.z);
        Destroy(this);
    }

}
}
