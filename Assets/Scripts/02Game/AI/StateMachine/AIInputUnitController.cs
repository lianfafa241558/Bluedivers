using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using Unity.FPS.Game;
using UnityEngine;

namespace FPSGame.AI
{

    /// <summary>
    /// 属于外挂组件，其他人不引用。
    /// 泛型状态机基类的"带炮台"扩展：在 AIInputBaseController<T> 基础上增加 turrets 炮台瞄准能力。
    /// 状态机框架（StateInfo/SwitchState/InvokeCurrentState）继承自 AIInputBaseController<T>。
    /// </summary>
    public abstract class AIInputUnitController<T> : AIInputBaseController<T> where T : System.Enum
    {
        [InspectorName("炮台")]
        [SerializeField]
        protected List<Turret> turrets = new();

        protected override void Start()
        {
            base.Start();
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].Init(transform);
            }
        }


        void LateUpdate()
        {
            UpdateTurretAiming();
        }


        /// <summary>炮台锁头(LateUpdate)</summary>
        protected virtual void UpdateTurretAiming()
        {
            float blendTime = Time.time - m_TimeStartedDetection;
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].Aiming(blendTime);
            }
        }

        protected abstract bool AimTargrt();

        /// <summary> 设置锁定目标</summary>
        protected void CalculationAimTargrt(Vector3 targetPos)
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].Look(targetPos);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 绘制炮塔限制角度与朝向（仅编辑器，选中时显示）
        /// </summary>
        protected virtual void OnDrawGizmosSelected()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                Turret t = turrets[i];
                if (!t) continue;
                t.DrawGizmosSelected();
            }
        }
#endif

    }
}
