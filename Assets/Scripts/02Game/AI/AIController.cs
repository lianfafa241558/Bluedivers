using System.Collections;
using System.Collections.Generic;
using GameContract;
using PEMaths;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using Utils;

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
    public float BirthDuration { get; }

    public string ID { get; }

    //public WeaponCurrentAttribute Speed { get;}

    public void Kill();

}



/// <summary>
/// 这个只是基础控制器
/// </summary>
public class AIController : MonoBehaviour, I_AIController
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
    float I_AIController.BirthDuration => this.BirthDuration;
    //public WeaponCurrentAttribute Speed => speed;

    //感觉可能需要加是第X号武器进行攻击的参数
    public UnityAction<WeaponBaseController> OnAttack { get => onAttack; set => onAttack = value; }
    public UnityAction OnDetectedTarget { get => onDetectedTarget; set => onDetectedTarget = value; }
    public UnityAction OnLostTarget { get => onLostTarget; set => onLostTarget = value; }
    public UnityAction<Collider> OnDamaged { get => onDamaged; set => onDamaged = value; }
    public UnityAction OnDie { get => onDie; set => onDie = value; }

    event UnityAction<WeaponBaseController> onAttack;
    event UnityAction onDetectedTarget;
    event UnityAction onLostTarget;
    event UnityAction<Collider> onDamaged;
    event UnityAction onDie;


    [CustomLabel("死亡后延迟，GameObject被销毁（以允许动画）")]
    public float DeathDuration = 0f;

    [CustomLabel("诞生后延迟（以允许动画）")]
    public float BirthDuration = 0f;

    //protected WeaponCurrentAttribute speed;

    protected Health m_Health;
    protected I_Actor m_Actor;

    [HideInInspector]
    public float birthTime;

    private void Awake()
    {
        InitComponent();
    }

    protected virtual void InitComponent()
    {
        m_Health = GetComponent<Health>();
        m_Actor = GetComponent<Actor>();
    }

    protected virtual void Start()
    {
        birthTime = Time.time;

        //订阅伤害和死亡行动
        m_Health.OnDie += _OnDie;
        m_Health.OnDamaged += _OnDamaged;
    }

    protected virtual void _OnDie(GameObject source)
    {
        OnLostTarget?.Invoke();
        OnDie?.Invoke();

        m_Health.OnDie -= _OnDie;
        m_Health.OnDamaged -= _OnDamaged;

        Tool.Destroy(gameObject, DeathDuration);
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

    public void Kill()
    {
        m_Health.Kill();
    }

}
