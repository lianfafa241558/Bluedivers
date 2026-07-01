using UnityEngine;
using Utils;

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