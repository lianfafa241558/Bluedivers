using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.AI
{
    /// <summary>
    /// 炮台专用的目标感知模块。
    /// </summary>
    [AddComponentMenu("单位/_侦测器/炮台侦测器")]
    public class DetectionModuleTurret : DetectionModule
    {
        [InspectorName("响应玩家标记")]
        public bool RespondMark;

        private void OnEnable()
        {
            GlobalEventBus.OnMark += OnMark;
        }
        private void OnDisable()
        {
            GlobalEventBus.OnMark -= OnMark;
        }

        /// <summary>【联机·成员端】应用房主下发的目标（<c>null</c> = 丢目标）。
        /// <para>⚠ 必须**连带抛**检测/丢失回调：<c>SetTargetActor</c> 只改数据、不抛
        /// <c>onDetectedTarget/onLostTarget</c> ⇒ 炮塔会停在上一个状态（开火判定与警告响应都不跟着变）。</para>
        /// <para>⚠ 幂等：同一目标重复调用直接返回（交战中房主每秒重发一次，不能每拍重放状态切换）。</para></summary>
        public void ApplyRemoteTarget(IActor actor)
        {
            if (Target.Actor == actor) return;
            SetTargetActor(actor);
            if (actor != null) OnDetect();
            else OnLostTarget();
        }

        void OnMark(GameObject owner, GameObject target, Vector3 point)
        {
            Debug.Log("标记"+ (m_Actor.Origin as Actor));
            if (m_Actor.Origin.IsValidMono() && owner != m_Actor.Origin.gameObject) return;
            if (!RespondMark||!target) return;
            //Debug.LogError("尝试寻找标记对象"+"目标"+ target+"距离"+ Vector3.Distance(transform.position, point),this);
            //Debug.LogError("具有组件"+ target.GetComponentInParent<Actor>(), this);
            //超范围丢失目标是他自己的事情，但是肯定得锁
            if (/*Vector3.Distance(target.transform.position, point) < DetectionRange
                && */target.transform.TryGetComponentInParent(out Actor actor)
                && actor != m_Actor//不锁自己
                && IsValidTarget(actor)   // Hide / 失活的目标同样不锁（见 DetectionModule.IsValidTarget）
            )
            {

                SetTargetActor(actor);
                OnDetect();
                
            }

        }
      

    }
}
