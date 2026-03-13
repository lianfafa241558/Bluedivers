using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.AI
{
    /// <summary>
    /// 巡逻功能
    /// </summary>
    public class PatrolPath : MonoBehaviour
    {
        [Tooltip("开始时将被分配到此路径的敌人")]
        public List<EnemyController> EnemiesToAssign = new List<EnemyController>();

        [Tooltip("构成路径的节点")]
        public List<Transform> PathNodes = new List<Transform>();

        void Start()
        {
            foreach (var enemy in EnemiesToAssign)
            {
                enemy.PatrolPath = this;
            }
        }

        public float GetDistanceToNode(Vector3 origin, int destinationNodeIndex)
        {
            if (destinationNodeIndex < 0 || destinationNodeIndex >= PathNodes.Count ||
                PathNodes[destinationNodeIndex] == null)
            {
                return -1f;
            }

            return (PathNodes[destinationNodeIndex].position - origin).magnitude;
        }

        public Vector3 GetPositionOfPathNode(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= PathNodes.Count || PathNodes[nodeIndex] == null)
            {
                return Vector3.zero;
            }

            return PathNodes[nodeIndex].position;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < PathNodes.Count; i++)
            {
                int nextIndex = i + 1;
                if (nextIndex >= PathNodes.Count)
                {
                    nextIndex -= PathNodes.Count;
                }

                Gizmos.DrawLine(PathNodes[i].position, PathNodes[nextIndex].position);
                Gizmos.DrawSphere(PathNodes[i].position, 0.1f);
            }
        }
    }
}