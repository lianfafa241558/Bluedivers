using Core;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace Unity.FPS.AI
{
    public class DetectionModuleTurret : DetectionModule
    {
        [CustomLabel("响应玩家标记")]
        public bool RespondMark;

        protected override void Start()
        {
            base.Start();
            GlobalEventManager.OnMark += OnMark;
        }
        private void OnDestroy()
        {
            GlobalEventManager.OnMark -= OnMark;
        }

        void OnMark(GameObject owner, GameObject target, Vector3 point)
        {
            if (m_Actor.Owner.IsValid() && owner != m_Actor.Owner.gameObject) return;
            if (!RespondMark||!target) return;
            //Debug.LogError("尝试寻找标记对象"+"目标"+ target+"距离"+ Vector3.Distance(transform.position, point),this);
            //Debug.LogError("具有组件"+ target.GetComponentInParent<Actor>(), this);

            if (Vector3.Distance(target.transform.position, point) < DetectionRange
                && target.transform.TryGetComponentInParent(out Actor actor)
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