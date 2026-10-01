using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

    public UnityAction<Vector3, Vector3> OnHit { get; set; }
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
    /// <param name="point">警惕点(要去查看的噪声点)</param>
    /// <param name="noise">该噪声的响度(半径/米)：比当前警惕点更轻时不覆盖</param>
    /// <param name="spread">是否把同一噪声扩散给附近队友</param>
    public void Beware(PEVector3 point, PEInt noise, bool spread);

    public IActor Actor { get; }
    public GameAttribute GetAttribute(UnitAttrType type);

    public T GetAttribute<T>(UnitAttrType type) where T : GameAttribute;
}



/// <summary>
/// 这个只是基础单位控制器
/// </summary>
public abstract class AIController : MonoBehaviour, I_AIController, IUnit, FPSGame.GameContract.IUnitScale
{
    /// <summary>体型（供 05_UnitCore 的表现逻辑按体型缩放特效等；属性查询留在本层，见 FPSGame.GameContract.IUnitScale）。</summary>
    float FPSGame.GameContract.IUnitScale.VisualScale => ((IUnit)this).GetAttribute(UnitAttrType.Size)?.FinalValue.RawFloat ?? 1f;

    public IActor Actor => m_Actor;
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

    public UnityAction<Vector3, Vector3> OnHit { get => onHit; set => onHit = value; }
    public UnityAction OnDie { get => onDie; set => onDie = value; }

    event UnityAction<WeaponBaseController> onAttack;//这里没有注册攻击事件
    event UnityAction onDetectedTarget;
    event UnityAction onLostTarget;
    event UnityAction<Collider> onDamaged;
    event UnityAction<Vector3,Vector3> onHit;
    event UnityAction onDie;


    [InspectorName("死亡后延迟，GameObject被销毁（以允许动画）")]
    public float DeathDuration = 0f;

    [InspectorName("诞生后延迟（以允许动画）")]
    public float BirthDuration = 0f;

    //protected WeaponCurrentAttribute speed;

    protected IHealth m_Health;
    protected IActor m_Actor;

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
        m_Health = GetComponent<IHealth>();
        m_Actor = GetComponent<Actor>();
    }

    protected virtual void Start()
    {
        birthTime = Time.time;

        //订阅伤害和死亡行为
        m_Health.OnHit += _OnHit;
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
        m_Health.OnHit -= _OnHit;
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

    /// <summary>受击   来源，受击点,法线，是弱点</summary>
    protected virtual void _OnHit(GameObject _, Vector3 pos, Vector3 normal1, bool _2)
    {
        OnHit?.Invoke(pos, normal1);
    }

    public void Kill(bool IsRemove)
    {
        this.IsRemove = IsRemove;
        m_Health.Kill();
    }
    /// <summary>
    /// 没有侦测组件的控制器什么都不做
    /// </summary>
    /// <param name="point">警惕点(要去查看的噪声点)</param>
    /// <param name="noise">该噪声的响度(半径/米)：比当前警惕点更轻时不覆盖</param>
    /// <param name="spread">是否把同一噪声扩散给附近队友</param>
    public virtual void Beware(PEVector3 point, PEInt noise, bool spread)
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
}
