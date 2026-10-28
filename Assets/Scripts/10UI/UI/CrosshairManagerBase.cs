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
using PEMaths;

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

    [SerializeField]
    protected Animator m_RicochetGo;

    /// <summary>准星的原始 localScale（按 Animator 实例缓存）：同一个准星会被多把武器复用，
    /// 不缓存就会在每次切武器时把已放大的尺寸再乘一次。</summary>
    private readonly Dictionary<Animator, Vector3> m_SightBaseScale = new();

    /// <summary>当前订阅散布属性的那个属性对象</summary>
    private GameAttribute m_SpreadAttr;

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
            UnbindSpreadAttr();
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
        BindSpreadAttr(weapon);
        ApplySightScale();
        OnSwitchWeapon?.Invoke();
    }

    protected abstract void SetAnimGo();

    /// <summary>
    /// 用「子弹散布角」的 最终值/原始值 等比缩放当前准星（原始 5 → 最终 10 ⇒ 两倍大）。
    /// 由切武器与散布属性变化（升级/装模组）触发，不放在 Update 里。
    /// </summary>
    protected void ApplySightScale()
    {
        if (!m_ActiveSightGo) return;
        if (!m_SightBaseScale.TryGetValue(m_ActiveSightGo, out var baseScale))
        {
            baseScale = m_ActiveSightGo.transform.localScale;
            m_SightBaseScale.Add(m_ActiveSightGo, baseScale);
        }
        m_ActiveSightGo.transform.localScale = baseScale * GetSpreadScale();
    }

    /// <summary>散布倍率。⚠ 原始值/最终值为 0 时比值无意义（最终值 0 会把准星缩成看不见），一律按 1 处理。</summary>
    private float GetSpreadScale()
    {
        if (!m_SpreadAttr) return 1f;
        float prime = m_SpreadAttr.PrimeValue.RawFloat;
        float final = m_SpreadAttr.FinalValue.RawFloat;
        if (prime <= 0f || final <= 0f) return 1f;
        return final / prime;
    }

    private void BindSpreadAttr(WeaponPlayerController weapon)
    {
        UnbindSpreadAttr();
        if (!weapon.IsValid()) return;
        var attr = weapon.cfg[WeaponAttrType.BulletsSpreadAngle];
        if (!attr) return;
        m_SpreadAttr = attr;
        m_SpreadAttr.OnFinalValueChange += OnSpreadAttrChanged;
    }

    private void UnbindSpreadAttr()
    {
        if (!m_SpreadAttr) return;
        m_SpreadAttr.OnFinalValueChange -= OnSpreadAttrChanged;
        m_SpreadAttr = null;
    }

    /// <summary>属性值变了（升级/模组写回）时同步准星尺寸</summary>
    private void OnSpreadAttrChanged(PEInt value) => ApplySightScale();


    /// <summary>击中目标回调</summary>
    protected void Hit(GameObject victim, GameObject attacker,bool ricochet)
    {
            if (victim != attacker && attacker == m_Weapons.Owner)
            {
                m_ActiveSightGo.SetTrigger(Constants.k_AnimOnDamagedParameter);
                if(ricochet&& m_RicochetGo!=null) m_RicochetGo.SetTrigger(Constants.k_AnimOnDamagedParameter);
            }
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
