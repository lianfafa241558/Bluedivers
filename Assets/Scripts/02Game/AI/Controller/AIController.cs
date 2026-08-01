using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Core;
using GameContract;
using PEMaths;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Utils;

public interface IUnit
{
    public GameAttribute GetAttribute(UnitAttrType type);
    public T GetAttribute<T>(UnitAttrType type) where T: GameAttribute;

    public void InitAttribute();

}

/// <summary>
/// 武器还没抽象化，所以塞不进去程序集
/// </summary>
public interface I_AIController
{
    public UnityAction<WeaponBaseController> OnAttack { get; set; }
    public UnityAction OnDetectedTarget { get; set; }
    public UnityAction OnLostTarget { get; set; }
    public UnityAction<Collider> OnDamaged { get; set; }
    public UnityAction OnDie { get; set; }

    public Vector3 HpPos { get; }
    public Vector3 Pos { get; set; }
    public Vector3 CenterPos { get; }

    Vector3 Velocity { get; }
    public float BirthDuration { get; set; }

    public string ID { get; }

    //public WeaponCurrentAttribute Speed { get;}

    public void Kill(bool isRemove);
    /// <summary>
    /// 使这个单位警惕
    /// </summary>
    public void Beware(Vector3 point,bool spread);


}



/// <summary>
/// 这个只是基础单位控制器
/// </summary>
public abstract class AIController : MonoBehaviour, I_AIController, IUnit
{

    public Transform AimPoint => m_Actor.AimPoint;
    public Vector3 CenterPos => m_Actor.CenterPos;
    public virtual Vector3 Pos {
        get => m_Actor.Pos;
        set
        {
            m_Actor.transform.position = value;
        }
    }
    public Vector3 HpPos => m_Actor.HpPos;
    public string ID => m_Actor.Id;
    float I_AIController.BirthDuration
    {
        get => this.BirthDuration;
        set => this.BirthDuration=value;
    }

    public virtual Vector3 Velocity => Vector3.zero;

    //public WeaponCurrentAttribute Speed => speed;

    //感觉可能需要加是第X号武器进行攻击的参数
    public UnityAction<WeaponBaseController> OnAttack { get => onAttack; set => onAttack = value; }
    public UnityAction OnDetectedTarget { get => onDetectedTarget; set => onDetectedTarget = value; }
    public UnityAction OnLostTarget { get => onLostTarget; set => onLostTarget = value; }
    public UnityAction<Collider> OnDamaged { get => onDamaged; set => onDamaged = value; }
    public UnityAction OnDie { get => onDie; set => onDie = value; }

    event UnityAction<WeaponBaseController> onAttack;//这里没有注册攻击事件
    event UnityAction onDetectedTarget;
    event UnityAction onLostTarget;
    event UnityAction<Collider> onDamaged;
    event UnityAction onDie;


    [InspectorName("死亡后延迟，GameObject被销毁（以允许动画）")]
    public float DeathDuration = 0f;

    [InspectorName("诞生后延迟（以允许动画）")]
    public float BirthDuration = 0f;

    //protected WeaponCurrentAttribute speed;

    protected Health m_Health;
    protected I_Actor m_Actor;

    [HideInInspector]
    public float birthTime;

    /// <summary>是被移除而不是正常死亡/summary>
    [HideInInspector]
    public bool IsRemove;


    protected Dictionary<UnitAttrType, GameAttribute> attrs;

    private void Awake()
    {
        InitComponent();
        InitAttribute();
    }

    protected virtual void InitComponent()
    {
        m_Health = GetComponent<Health>();
        m_Actor = GetComponent<Actor>();
    }

    protected virtual void Start()
    {
        birthTime = Time.time;

        //订阅伤害和死亡行为
        m_Health.OnDie += _OnDie;
        m_Health.OnDamaged += _OnDamaged;
    }

    protected virtual void _OnDie(GameObject source)
    {
        if (!IsRemove)
        {
            OnDie?.Invoke();
            OnLostTarget?.Invoke();
            Invoke(nameof(DisableCollider), 1f);
            //GetComponent<Collider>().enabled = false;
        }

        m_Health.OnDie -= _OnDie;
        m_Health.OnDamaged -= _OnDamaged;

        Tool.Destroy(gameObject, IsRemove?0: DeathDuration);

    }

    protected virtual void _OnLostTarget()
    {
        OnLostTarget?.Invoke();
    }

    protected virtual void _OnDetectedTarget()
    {
        OnDetectedTarget?.Invoke();
    }

    protected virtual void _OnDamaged(PEInt damage, GameObject damageSource, Collider collider, bool noSource)
    {
        OnDamaged?.Invoke(collider);
    }

    public void Kill(bool IsRemove)
    {
        this.IsRemove = IsRemove;
        m_Health.Kill();
    }
    /// <summary>
    /// 没有侦测组件的控制器什么都不做
    /// </summary>
    /// <param name="point"></param>
    public virtual void Beware(Vector3 point,bool spread)
    {

    }

    private void DisableCollider()
    {
        foreach (var item in GetComponentsInChildren<Collider>())
        {
            item.enabled = false;
        }
    }

    public GameAttribute GetAttribute(UnitAttrType type)
    {
        if (attrs.TryGetValue(type,out var attr)){
            return attr;
        }
        return null;
    }

    public T GetAttribute<T>(UnitAttrType type) where T : GameAttribute
    {
        if (attrs.TryGetValue(type, out var attr))
        {
            return attr as T;
        }
        return null;
    }

    public virtual void InitAttribute()
    {
        attrs = UnitAttributeFactory.CreateBaseUnit(new Dictionary<UnitAttrType, PEInt> {
            [UnitAttrType.Speed] = 0,
            [UnitAttrType.AngularSpeed] = 0,
        }); ;
    }
}
