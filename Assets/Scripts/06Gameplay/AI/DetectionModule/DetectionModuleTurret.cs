using FPSGame.Core;
using FPSGame.Core.Interface;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.AI
{
    /// <summary>
    /// 炮台专用的目标感知模块。
    /// </summary>
    [AddComponentMenu("AI/炮台感知模块")]
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

        void OnMark(GameObject owner, GameObject target, Vector3 point)
        {
            Debug.Log("标记"+ (m_Actor.Owner as Actor));
            if (m_Actor.Owner.IsValidMono() && owner != m_Actor.Owner.gameObject) return;
            if (!RespondMark||!target) return;
            //Debug.LogError("尝试寻找标记对象"+"目标"+ target+"距离"+ Vector3.Distance(transform.position, point),this);
            //Debug.LogError("具有组件"+ target.GetComponentInParent<Actor>(), this);
            //超范围丢失目标是他自己的事情，但是肯定得锁
            if (/*Vector3.Distance(target.transform.position, point) < DetectionRange
                && */target.transform.TryGetComponentInParent(out Actor actor)
                && actor != m_Actor//不锁自己
                && FpsHelper.VaildTarget(actor)
            )
            {

                SetTargetActor(actor);
                OnDetect();
                
            }

        }
      

    }
}
