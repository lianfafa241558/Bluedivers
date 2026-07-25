using System.Collections.Generic;
using FPSGame.Attribute;
using PEMaths;
using Unity.FPS.Game;
using UnityEngine;

/// <summary>
/// 基础的玩家操作控制器，可以旋转，无法移动和跳跃炮台)
/// </summary>
[RequireComponent(typeof(PlayerInputHandler), typeof(AudioSource))]
public class BaseSelfController : MonoBehaviour, IUnit
{
    public Vector3 CenterPos => m_Actor.CenterPos;
    public string PlayerName => m_Actor.ShowName;
    public string Id => m_Actor.Id;
    public Sprite Portrait => m_Actor.Portrait;
    public Sprite Halo => m_Actor.ExtraPortrait;
    public Color Color => m_Actor.Color;

    [Foldout("一般", true)]

    [SerializeField]
    [InspectorName("音频源")]
    protected AudioSource AudioSource;


    [Foldout("旋转", true)]

    [SerializeField]
    [InspectorName("镜头旋转速度")]
    protected float RotationSpeed = 200f;

    [SerializeField]
    [InspectorName("瞄准时的旋转速度倍率")]
    [Range(0.1f, 1f)]
    protected float AimingRotationMultiplier = 0.4f;

    [SerializeField]
    [InspectorName("垂直旋转的上限")]
    protected float UpperRotationLimit=89;
    [SerializeField]
    [InspectorName("垂直旋转的下限")]
    protected float LowerRotationLimit=70;

    public Health Health { get; private set; }
    public PlayerInputHandler InputHandler { get; private set; }

    protected Animator m_Anim { get; set; }
    protected Actor m_Actor { get; set; }

    //后坐力
    protected float VerticalNewRecoil, VerticalRecoil;

    //旋转角度记录
    protected float m_CameraHorizontalAngle = 0;//(目前只有)死亡才用
    [SerializeField]
    protected float m_CameraVerticalAngle = 0;


    protected Dictionary<UnitAttrType, GameAttribute> attrs;

    protected virtual void Awake()
    {
        InputHandler = GetComponent<PlayerInputHandler>();
        Health = GetComponent<Health>();
        m_Actor = GetComponent<Actor>();
        m_Anim = GetComponent<Animator>();
        if (!AudioSource) AudioSource = GetComponent<AudioSource>();
        InitAttribute();
    }

    protected virtual void Update()
    {
        HandleRotation();
    }
    public virtual void InitAttribute()
    {
        attrs = UnitAttributeFactory.CreateBaseUnit(new Dictionary<UnitAttrType, PEInt> {
            [UnitAttrType.Speed] = 0,
            [UnitAttrType.AngularSpeed] = (PEInt)RotationSpeed,
            [UnitAttrType.Size] = (PEInt)m_Actor.HalfRange,
        });
    }

    /// <summary>
    /// 旋转和后坐力控制
    /// </summary>
    protected virtual void HandleRotation()
    {

        //以输入速度围绕其局部Y轴旋转变化
        transform.Rotate(new Vector3(0f, (InputHandler.GetLookInputsHorizontal() * GetAttribute(UnitAttrType.AngularSpeed).FinalValue.RawFloat *Time.deltaTime), 0f), Space.World);

        //为相机的垂直角度添加垂直输入
        m_CameraVerticalAngle += InputHandler.GetLookInputsVertical() * GetAttribute(UnitAttrType.AngularSpeed).FinalValue.RawFloat * Time.deltaTime;


        //后坐力恢复
        if (VerticalRecoil > 0)
        {
            var speed = Mathf.Lerp(Time.deltaTime, VerticalRecoil, Time.deltaTime * 2);
            VerticalRecoil -= speed;
            m_CameraVerticalAngle += speed;
        }

        //后坐力
        if (VerticalNewRecoil > 0)
        {
            var speed = Mathf.Lerp(Time.deltaTime, VerticalNewRecoil, Time.deltaTime * 10);
            VerticalNewRecoil -= speed;
            speed = Mathf.Min(speed, 12 - VerticalRecoil);
            VerticalRecoil += speed;
            m_CameraVerticalAngle -= speed;
        }

        //此处只设置，子类要抬头就自己把相机根据这个设置
        m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -UpperRotationLimit, LowerRotationLimit);//-89,70

    }

    public GameAttribute GetAttribute(UnitAttrType type)
    {
        if (attrs.TryGetValue(type, out var attr))
        {
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

}
