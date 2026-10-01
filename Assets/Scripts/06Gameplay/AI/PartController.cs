using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using PEMaths;


using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using FPSGame.Utils;
using FPSGame.Weapon;

namespace FPSGame.AI
{

/// <summary>
/// 可破坏部件（如护盾发射器）的控制器。
/// </summary>
[AddComponentMenu("AI/部件控制器")]
public class PartController : MonoBehaviour
{
    /// <summary>腿部(影响速度)装甲</summary>
    [Header("腿部(影响速度)装甲")]
    [SerializeField]
    private Damageable[] legs;
    /// <summary>(使自身无敌的)装甲</summary>
    [Header("(使自身无敌的)装甲")]
    public List<Damageable> invincibleArmor;
    /// <summary>(全部摧毁)致死装甲</summary>
    [Header("(全部摧毁)致死装甲")]

    public Damageable[] deathArmor;

    /// <summary>所有无敌装甲被摧毁时触发</summary>
    public event UnityAction OnAllInvincibleArmorDestroyed;

    /// <summary>无敌装甲列表发生变化（增/减/清空降级）时触发，供 UI 同步</summary>
    public event UnityAction OnInvincibleArmorListChanged;

    private int DeathPartCount;

    private I_AIController controller;
    //public NavMeshAgent NavMeshAgent { get; private set; }
    //private float baseSpeed;


    private void OnEnable()
    {

        controller = GetComponent<I_AIController>();
        //NavMeshAgent = GetComponent<NavMeshAgent>();
        //if (FpsHelper.HaveNavMeshAgent(NavMeshAgent)) baseSpeed = NavMeshAgent.speed;

        DeathPartCount = 0;
        for (int i = 0; i < legs.Length; ++i)
        {
            legs[i].OnDestroyPart += OnLegDestroy;
        }
        for (int i = 0; i < deathArmor.Length; ++i)
        {
            deathArmor[i].OnDestroyPart += OnDeathPartDestroy;
        }
        for (int i = 0; i < invincibleArmor.Count; ++i)
        {
            invincibleArmor[i].OnDestroyPart += OnInvinciblePartDestroy;
        }
    }

    private void OnDisable()
    {
        if (legs.IsValid())
        {
            for (int i = 0; i < legs.Length; ++i)
            {
                if (legs[i]) legs[i].OnDestroyPart -= OnLegDestroy;
            }
        }
        if (deathArmor.IsValid())
        {
            for (int i = 0; i < deathArmor.Length; ++i)
            {
                if (deathArmor[i]) deathArmor[i].OnDestroyPart -= OnDeathPartDestroy;
            }
        }
        if (invincibleArmor.IsValid())
        {
            for (int i = 0; i < invincibleArmor.Count; ++i)
            {
                if (invincibleArmor[i]) invincibleArmor[i].OnDestroyPart -= OnInvinciblePartDestroy;
            }
        }
    }



    public void AddInvincibleArmor(Damageable damageable)
    {
        if (invincibleArmor.Count == 0) controller.Actor.AddFlag(ActorFlag.Invincible);
        invincibleArmor.Add(damageable);
        damageable.OnDestroyPart += OnInvinciblePartDestroy;
        OnInvincibleArmorListChanged?.Invoke();
    }


    void OnInvinciblePartDestroy(Damageable damageable)
    {
        damageable.OnDestroyPart -= OnInvinciblePartDestroy;
        invincibleArmor.Remove(damageable);
        if (invincibleArmor.Count == 0)
        {
            controller.Actor.RemoveFlag(ActorFlag.Invincible);
            OnAllInvincibleArmorDestroyed?.Invoke();
        }
        OnInvincibleArmorListChanged?.Invoke();
    }

    void OnLegDestroy(Damageable _)
    {
        controller.GetAttribute(UnitAttrType.Speed).AddModifier(ModifierType.Factor,new(1f/legs.Length));
        //if (FpsHelper.HaveNavMeshAgent(NavMeshAgent))
        //{
        //    NavMeshAgent.speed -= baseSpeed / legs.Length;
        //}

    }
    void OnDeathPartDestroy(Damageable _)
    {

        if (++DeathPartCount >= deathArmor.Length)
        {
            GetComponent<IHealth>().Kill();
        }
    }
}
}
