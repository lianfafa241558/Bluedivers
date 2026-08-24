using Core;

using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FPSGame.AI
{
    public class DetectionModuleTurret : DetectionModule
    {
        [InspectorName("响应玩家标记")]
        public bool RespondMark;

        private void OnEnable()
        {
            GlobalEventSub.OnMark += OnMark;
        }
        private void OnDisable()
        {
            GlobalEventSub.OnMark -= OnMark;
        }

        void OnMark(GameObject owner, GameObject target, Vector3 point)
        {
            Debug.Log("标记"+ (m_Actor.Owner as Actor));
            if (m_Actor.Owner.IsValid() && owner != m_Actor.Owner.gameObject) return;
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