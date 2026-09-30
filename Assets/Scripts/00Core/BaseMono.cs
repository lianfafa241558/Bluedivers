using PEMaths;
using UnityEngine;

namespace FPSGame.Core
{
    /// <summary>
    /// 场景对象（MonoBehaviour）的通用基类：提供位置/朝向/逻辑坐标的统一访问入口。
    /// <para>从 <c>GameRootBase.cs</c> 拆出（2026-09-30 文件职责整理）。</para>
    /// </summary>
    public abstract class BaseMono : MonoBehaviour
    {
        public virtual Vector3 Pos
        {
            get => transform.position;
            set
            {
                transform.position = value;
            }
        }

        public virtual PEVector2 LogicPos
        {
            get => (PEVector2)transform.position;
            set
            {
                transform.position = value.RawVector2;
            }
        }

        public virtual PEVector3 Logic3Pos
        {
            get => (PEVector3)transform.position;
            set
            {
                transform.position = value.RawVector3;
            }
        }

        public Vector3 Angles => transform.eulerAngles;

        public virtual Vector3 Forward => transform.forward;

        public virtual Vector3 CenterPos => this == null ? default : transform.position + Vector3.up * 2;

        public void LookAt(BaseMono mono)
        {
            if (!mono) return;
            transform.LookAt(mono.transform);
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        }

        public void LookAt(Vector3 vector)
        {
            transform.LookAt(vector);
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        }
    }
}
