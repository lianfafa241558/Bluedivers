using Core;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
namespace Unity.FPS.AI
{
    /// <summary>
    /// 有这个的单位可以拉烟
    /// </summary>
    public class CallReinforcement : TickBehaviour
    {
        [CustomLabel("触发时间")]
        public int DetectedTime;
        [CustomLabel("触发范围")]
        public float DetectedRange;

        public AudioClip cilp;
        public GameObject ps;



        int m_DetectedCount;
        bool m_HaveTarget;
        protected EnemyController m_EnemyController;
        protected override void Start()
        {
            base.Start();
            m_EnemyController = GetComponent<EnemyController>();

            m_EnemyController.OnDetectedTarget += OnDetectedTarget;
            m_EnemyController.OnLostTarget += OnLostTarget;

        }


        public override bool Tick()
        {
            if(!m_HaveTarget) return true;
            bool find = false;
            var players = ActorsManager.Players;
            for (int i=0;i< players.Count; ++i)
            {
                var end = players[i].CenterPos;
                var start = m_EnemyController.AimPoint.position;
                if (Vector3.Distance(start,end) < DetectedRange)
                {
                    Vector3 direction = end - start;
                    // 检测射线碰撞，最大距离为两点间实际距离
                    if (!Physics.Raycast(new(start, direction.normalized), out RaycastHit hit, direction.magnitude, LayerDefinition.GroundLayers))
                    {
                        find = true;
                        break;
                    }

                }
            }
            if(find)
            {
                if (++m_DetectedCount> DetectedTime)
                {
                    Vector3 pos = m_EnemyController.AimPoint.position;
                    if (BattleManager.Instance.CreatWave(pos, false))
                    {
                        m_DetectedCount = 0;
                        _ = AudioManager.PlaySound(new(cilp, pos, 60, AudioGroups.Enemy, 1));
                        VFXManager.Creat(ps, pos);
                    }
                }
            }
            else
            {
                m_DetectedCount = 0;
            }
            return true;
        }



        /// <summary>
        /// 发现目标
        /// </summary>
        protected virtual void OnDetectedTarget()
        {
            m_HaveTarget = true;
        }

        /// <summary>
        /// 丢失目标
        /// </summary>
        protected virtual void OnLostTarget()
        {
            m_HaveTarget = false;
            m_DetectedCount = 0;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, DetectedRange);

        }

    }

}