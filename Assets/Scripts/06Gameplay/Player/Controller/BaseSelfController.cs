using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.GameContract;
using PEMaths;
using FPSGame.Game;
using UnityEngine;
using FPSGame.AI;
using FPSGame.Weapon;

namespace FPSGame.Gameplay
{

/// <summary>
/// 基础的玩家操作控制器，可以旋转，无法移动和跳跃炮台)
/// </summary>
[RequireComponent(typeof(PlayerInputHandler), typeof(AudioSource))]
public class BaseSelfController : MonoBehaviour, IUnit, FPSGame.GameContract.IUnitScale
{
    /// <summary>体型（供 05_UnitCore 的表现逻辑按体型缩放特效等；属性查询留在本层，见 FPSGame.GameContract.IUnitScale）。</summary>
    float FPSGame.GameContract.IUnitScale.VisualScale => ((IUnit)this).GetAttribute(UnitAttrType.Size)?.FinalValue.RawFloat ?? 1f;

    public Vector3 CenterPos => Actor.CenterPos;
    public string PlayerName => Actor.ShowName;
    public string Id => Actor.Id;
    public Sprite Portrait => Actor.Portrait;
    public Sprite Halo => Actor.ExtraPortrait;
    public Color Color => Actor.Color;

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

    public IHealth Health { get; private set; }
    public PlayerInputHandler InputHandler { get; private set; }

    protected Animator m_Anim { get; set; }
    public Actor Actor { get;private set; }

    /// <summary>相机后坐状态机：偏移量与视角角**分开存**（见 <see cref="WeaponRecoilState"/>）</summary>
    readonly WeaponRecoilState m_Recoil = new();

    /// <summary>当前武器的后坐力参数：由开火方注入（<see cref="PlayerController.WeapomRecoil"/>）；null = 后坐力不推进</summary>
    public WeaponRecoilProfile RecoilProfile { get; set; }

    /// <summary>屏震状态（trauma 模型）：开火/冲击波/受击都往里加，输出叠加进相机偏移</summary>
    readonly CameraShakeState m_Shake = new();

    /// <summary>屏震状态：外部可直接 <see cref="CameraShakeState.Add"/>（一般用 <see cref="AddCameraShake"/> 就够）</summary>
    public CameraShakeState CameraShake => m_Shake;

    /// <summary>场景内所有"有相机的操作者"（玩家/载具/炮台）：给 <see cref="BroadcastCameraShake"/> 用</summary>
    static readonly List<BaseSelfController> s_ShakeHolders = new();

    //旋转角度记录
    protected float m_CameraHorizontalAngle = 0;//(目前只有)死亡才用
    [SerializeField]
    protected float m_CameraVerticalAngle = 0;

    /// <summary>相机垂直角（俯仰；正 = 抬头）**含后坐力、不含屏震**。**公开只读**：联机要把"我抬没抬头"告诉别人
    /// （盟友模型的武器挂点靠它俯仰，见 <c>PoseSnapshot.Pitch</c>）。
    /// <para>▍为什么不含屏震：屏震是**纯表现**，若混进这里，武器挂点朝向会跟着抖 ⇒ 子弹也跟着抖（不该影响精度）。
    /// 相机渲染要自己再叠 <see cref="ShakePitch"/>，见各应用点。</para></summary>
    public float CameraPitch => Mathf.Clamp(m_CameraVerticalAngle + m_Recoil.Pitch, -UpperRotationLimit, LowerRotationLimit);

    /// <summary>后坐力俯仰偏移(度，负 = 抬头)：第三人称有自己的一套限位，需要单独取用</summary>
    public float RecoilPitch => m_Recoil.Pitch;

    /// <summary>后坐力水平偏移(度)：叠加到相机 yaw 上，但**不参与移动方向**；武器挂点用它 ⇒ 弹道跟枪口一致</summary>
    public float RecoilYaw => m_Recoil.Yaw;

    /// <summary>屏震俯仰偏移(度)：**只给相机渲染用**，别加到武器挂点/弹道上</summary>
    public float ShakePitch => m_Shake.Pitch;

    /// <summary>屏震水平偏移(度)：同样只给相机渲染用</summary>
    public float ShakeYaw => m_Shake.Yaw;

    /// <summary>屏震翻滚偏移(度)：后坐力不做 roll，只有屏震有</summary>
    public float CameraRoll => m_Shake.Roll;


    protected Dictionary<UnitAttrType, GameAttribute> attrs;

    protected virtual void Awake()
    {
        InputHandler = GetComponent<PlayerInputHandler>();
        Health = GetComponent<IHealth>();
        Actor = GetComponent<Actor>();
        m_Anim = GetComponent<Animator>();
        if (!AudioSource) AudioSource = GetComponent<AudioSource>();

        // 屏震名单注册放 Awake，别放 OnEnable：PlayerController/VehicleController 各有**私有**的
        // OnEnable，会静默盖掉基类的（Unity 反射取最派生的那个）⇒ 玩家永远进不了名单。
        // 已销毁的条目由 BroadcastCameraShake 的 null 检查顺手剔除，不必在 OnDisable 退订。
        // 去重：对象池复用/重复 Awake 时同一个实例别登记两次（否则屏震加倍）
        if (!s_ShakeHolders.Contains(this)) s_ShakeHolders.Add(this);

        InitAttribute();
    }

    protected virtual void Update()
    {
        // 后坐力/屏震必须先于旋转推进：第三人称走的是 HandleRotationThirdPerson，不会进 HandleRotation，
        // 放在 HandleRotation 里会导致切第三人称后偏移卡死
        float dt = Time.deltaTime;
        m_Recoil.Tick(dt, RecoilProfile);
        m_Shake.Tick(dt);
        HandleRotation();
    }

    /// <summary>开一发后坐（相机层）：<paramref name="strength"/> 一般传
    /// <see cref="WeaponPlayerController.RecoilForceFinal"/> 或载具的 <see cref="VehicleWeaponsManager.RecoilForce"/></summary>
    public void AddRecoil(float strength, bool aiming) => m_Recoil.AddShot(RecoilProfile, strength, aiming);

    /// <summary>清空后坐偏移（重生 / 切枪 / 上载具）</summary>
    public void ResetRecoil() => m_Recoil.Reset();

    /// <summary>叠加一次屏震(0~1)：开火/受击这类"自己身上发生"的直接调它</summary>
    public void AddCameraShake(float strength01) => m_Shake.Add(strength01);

    /// <summary>
    /// 向场景内**所有相机持有者**广播一次屏震（玩家/载具/炮台），按距离线性衰减。
    /// <para>▍可复用入口：冲击波、爆炸、火车经过、Boss 落地这类"以世界点为源"的事件都走它 —— 一行调用，
    /// 不用自己去场景里找相机。半径外不震，避免地图另一头也在抖。</para>
    /// </summary>
    public static void BroadcastCameraShake(Vector3 pos, float radius, float strength01)
    {
        if (radius <= 0f || strength01 <= 0f) return;

        for (int i = s_ShakeHolders.Count - 1; i >= 0; --i)
        {
            var holder = s_ShakeHolders[i];
            if (!holder)
            {
                // 销毁没走 OnDisable 的极少数情况（场景卸载时被直接干掉）
                s_ShakeHolders.RemoveAt(i);
                continue;
            }

            float dist = Vector3.Distance(holder.transform.position, pos);
            if (dist >= radius) continue;
            holder.m_Shake.Add(strength01 * (1f - dist / radius));
        }
    }


    public virtual void InitAttribute()
    {
        attrs = UnitAttributeFactory.CreateBaseUnit(new Dictionary<UnitAttrType, PEInt> {
            [UnitAttrType.Speed] = 0,
            [UnitAttrType.AngularSpeed] = (PEInt)RotationSpeed,
            [UnitAttrType.Size] = (PEInt)Actor.HalfRange,
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

        //后坐力不在这里算：它是 m_Recoil 里的独立偏移，由 Update 推进、CameraPitch/RecoilYaw 叠加输出

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
}
