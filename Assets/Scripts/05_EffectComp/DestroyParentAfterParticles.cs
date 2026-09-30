using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.EffectComp
{

/// <summary>
/// 粒子播放结束后销毁父物体。
/// </summary>
[AddComponentMenu("特效/粒子结束销毁")]
public class DestroyParentAfterParticles : MonoBehaviour
{
    private ParticleSystem parentParticleSystem;

    void Start()
    {
        // 获取父物体的粒子系统
        parentParticleSystem = GetComponent<ParticleSystem>();

        // 如果父物体没有粒子系统，禁用脚本
        if (parentParticleSystem == null)
        {
            Debug.LogWarning("No ParticleSystem found on the parent object.");
            enabled = false;
        }
    }

    void Update()
    {
        // 如果父物体的粒子系统存在且没有在播放
        if (parentParticleSystem != null && !parentParticleSystem.IsAlive(false))
        {
            // 销毁父物体
            Tool.Destroy(gameObject);
        }
    }
}
}
