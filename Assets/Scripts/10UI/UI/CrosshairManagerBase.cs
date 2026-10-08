using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;

using FPSGame.Game;
using FPSGame.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using FPSGame.Utils;
using FPSGame.Weapon;

namespace FPSGame.UI
{

public abstract class CrosshairManagerBase : MonoBehaviour
{
    public UnityAction OnSwitchWeapon;
    /// <summary>锁定一个敌人时</summary>
    public UnityAction<IActor, bool> OnLockUpdate;
    /// <summary>开启结束锁定</summary>
    public UnityAction<bool> OnLock;


    public WeaponPlayerController m_Weapons;
    [SerializeField]
    protected Animator m_ActiveSightGo;

    protected virtual void Start()
    {
        UnitEventBus.OnUnitHit += Hit;
    }

    protected virtual void OnDestroy()
    {
        UnitEventBus.OnUnitHit -= Hit;
        SwitchWeapon(null, false);
    }
    protected virtual void SwitchWeapon(WeaponPlayerController weapon,bool isSec = false)
    {
        if (isSec) return;
        //清空旧的
        if (m_Weapons.IsValid())
        {
            m_Weapons.OnShoot -= Attack;
            //m_Weapons.OnHit -= Hit;
            m_Weapons.OnCharget -= Chatget;
            m_Weapons.OnLock -= Chatget;
            m_Weapons.OnLockUpdate -= LockUpdate;
        }
        //Debug.LogError("新武器+ m_Weapons, m_Weapons);
        //设置新的
        m_Weapons = weapon;
        if (!weapon.IsValid()) return;//销毁只清空，不设置
        weapon.OnShoot += Attack;
        //weapon.OnHit += Hit;
        weapon.OnCharget += Chatget;
        weapon.OnLock += Chatget;
        weapon.OnLockUpdate += LockUpdate;
        SetAnimGo();
        OnSwitchWeapon?.Invoke();
    }

    protected abstract void SetAnimGo();


    /// <summary>击中目标回调</summary>
    protected void Hit(GameObject victim, GameObject attacker)
    {
        if(victim != attacker && attacker == m_Weapons.Owner ) m_ActiveSightGo.SetTrigger(Constants.k_AnimOnDamagedParameter);
    }
    /// <summary>发起攻击回调</summary>
    protected void Attack(WeaponBaseController weapon)
    {
        //Debug.LogError("设置射击");
        m_ActiveSightGo.SetTrigger(Constants.k_AnimAttackParameter);
        //Debug.LogError("设置取消蓄力");
        //m_ActiveSightGo.SetBool(Constants.k_AnimChatgetParameter,false);
    }
    /// <summary>开启?取消蓄力回调</summary>
    protected void Chatget(bool state)
    {
        OnLock?.Invoke(state);
        //Debug.LogError(state+"设置开始蓄力 + m_ActiveSightGo, m_ActiveSightGo);
        m_ActiveSightGo.SetBool(Constants.k_AnimChatgetParameter, state);
        //Debug.LogError(state+"获取蓄力状态 + m_ActiveSightGo.GetBool(Constants.k_AnimChatgetParameter), gameObject);
    }

    protected void LockUpdate(IActor actor,bool state)
    {
        OnLockUpdate?.Invoke(actor, state);
    }
}
}
