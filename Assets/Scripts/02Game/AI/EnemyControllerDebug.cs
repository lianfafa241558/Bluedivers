using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*
namespace Unity.FPS.AI
{
    public partial class EnemyController
    {

        [Foldout("调试显示", true)]
        [CustomLabel("表示路径到达范围的球体小工具颜色")]
        public Color PathReachingRangeColor = Color.yellow;

        [CustomLabel("表示攻击范围的球体小工具颜色")]
        public Color AttackRangeColor = Color.red;

        [CustomLabel("表示检测范围的球体小工具颜色")]
        public Color DetectionRangeColor = Color.blue;


        void OnDrawGizmosSelected()
        {
            // Path reaching range
            Gizmos.color = PathReachingRangeColor;
            Gizmos.DrawWireSphere(transform.position, PathReachingRadius);

            if (DetectionModule != null)
            {
                // Detection range
                Gizmos.color = DetectionRangeColor;
                Gizmos.DrawWireSphere(transform.position, DetectionModule.DetectionRange);

                // Attack range
                Gizmos.color = AttackRangeColor;
                Gizmos.DrawWireSphere(transform.position, DetectionModule.AttackRange);
            }
        }

    }
}*/