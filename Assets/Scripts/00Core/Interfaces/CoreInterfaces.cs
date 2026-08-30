using PEMaths;
using UnityEngine;

namespace Core.Interface
{
    public interface I_GlobaManager
    {
        void Init();
        void UnInit();
    }

    public interface I_Entity
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
    public interface IRecyclable
    {
        public void OnShow();

        public void OnHide();

    }

    /// <summary>
    /// 应用物理效果接口
    /// </summary>
    public interface IPhysical
    {
        /// <summary>应用力</summary>
        void ApplyForce(PEVector3 vector);

        /// <summary>应用重力</summary>
        void ApplyGravity();
    }

}
