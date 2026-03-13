using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

public class PartController : MonoBehaviour
{
    /// <summary>腿部(影响速度)装甲</summary>
    [Header("腿部(影响速度)装甲")]
    [SerializeField]
    private Damageable[] legs;
    /// <summary>(使自身无敌的)装甲</summary>
    [Header("(使自身无敌的)装甲")]
    public Damageable[] invincibleArmor;
    /// <summary>(全部摧毁后)致死装甲</summary>
    [Header("(全部摧毁后)致死装甲")]
    [SerializeField]
    private Damageable[] deathArmor;

    private int DeathPartCount;

    public NavMeshAgent NavMeshAgent { get; private set; }
    private float baseSpeed;

    public void Start()
    {
        NavMeshAgent = GetComponent<NavMeshAgent>();
        if (FpsHelper.HaveNavMeshAgent(NavMeshAgent)) baseSpeed = NavMeshAgent.speed;

        DeathPartCount = 0;
        for (int i = 0; i < legs.Length; ++i)
        {
            legs[i].OnDestroyPart += OnLegDestroy;
        }
        for (int i = 0; i < deathArmor.Length; ++i)
        {
            deathArmor[i].OnDestroyPart += OnDeathPartDestroy;
        }
    }


    void OnLegDestroy()
    {
        if (FpsHelper.HaveNavMeshAgent(NavMeshAgent))
        {
            NavMeshAgent.speed -= baseSpeed / legs.Length;
        }

    }
    void OnDeathPartDestroy()
    {

        if (++DeathPartCount >= deathArmor.Length)
        {
            GetComponent<Health>().Kill();
        }
    }
}
