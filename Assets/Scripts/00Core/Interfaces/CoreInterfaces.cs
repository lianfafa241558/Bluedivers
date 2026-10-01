using PEMaths;
using UnityEngine;

namespace FPSGame.Core.Interface
{
    /// <summary>
    /// 使用拓展方法实现null判断，必须对挂在mono的接口才能用
    /// </summary>
    public interface IMonoVaild { }


    public interface I_GlobaManager : IMonoVaild
    {
        void Init();
        void UnInit();
    }

    public interface IEntity: IMonoVaild
    {
        public string ShowName { get; set; }
        public string Id { get; set; }
        public Sprite Portrait { get; set; }
        public Sprite ExtraPortrait { get; set; }

        public Color Color { get; set; }

        PEVector2 LogicPos { get; }

        PEVector3 Logic3Pos { get; }

        Vector3 CenterPos { get; }
        Vector3 Pos { get; set; }
        Vector3 Angles { get; }

        Vector3 Forward { get; }

        /// <summary>单位半径</summary>
        public float HalfRange { get;}

        /// <summary>
        /// 单位半高度
        /// 单位竖直占位区间 = [CenterPos.y - HalfHeight, CenterPos.y + HalfHeight]
        /// 0 表示未配置，需要做竖直判定的逻辑应退化为"不做高度过滤"
        /// </summary>
        public float HalfHeight { get;}

        public Transform transform { get; }
        public GameObject gameObject { get; }
    }
    /// <summary>
    /// 可回收接口 对象
    /// </summary>
    public interface IRecyclable : IMonoVaild
    {
        public void OnShow();

        public void OnHide();

    }

    /// <summary>
    /// 应用物理效果接口
    /// </summary>
    public interface IPhysical:IMonoVaild
    {
        /// <summary>
        /// 施加一个持续力(牛顿)：由实现方在移动循环里按 Δv = (力 / 质量)·dt 积分成速度。
        /// 需要逐帧持续施加；一次性打击(爆炸冲击波/击退)请用 <see cref="ApplyImpulse"/>
        /// </summary>
        void ApplyForce(PEVector3 force);

        /// <summary>施加一个瞬时冲量：Δv = 冲量 / 质量。适合爆炸冲击波、击退等一次性打击</summary>
        void ApplyImpulse(PEVector3 impulse);

        /// <summary>应用重力</summary>
        void ApplyGravity();
    }

    /// <summary>
    /// 接口 null 判定的扩展方法（仅对挂在 MonoBehaviour 上的接口有效）。
    /// <para>⚠ 因为它是扩展方法，调用点写 <c>x.IsValidMono()</c> 不出现类名，
    /// 所以静态搜索找不到引用点，改命名空间时必须由编译器兜底。</para>
    /// </summary>
    public static class ISValidExtensions
    {
        public static bool IsValidMono(this IMonoVaild obj)
        {
            return obj is Object o && o != null;
        }
    }
}
