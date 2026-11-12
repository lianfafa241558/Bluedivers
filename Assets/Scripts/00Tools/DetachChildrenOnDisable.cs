using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Utils
{
    /// <summary>
    /// 自身 OnDisable 时把列表里各 Transform 的直接子物体脱成世界根（保留世界位姿）。
    /// 用于禁用/隐藏时让挂件、特效留在原地；不用 Transform.DetachChildren —— 它会丢世界坐标。
    /// </summary>
    [AddComponentMenu("工具/禁用时脱离子物体")]
    public class DetachChildrenOnDisable : MonoBehaviour
    {
        [SerializeField]
        [InspectorName("要脱离子物体的父级")]
        private List<Transform> _targets = new List<Transform>();

        private void OnDisable()
        {
            // 销毁流程也会进 OnDisable，此序列化列表可能已被置空
            if (_targets == null) return;

            for (int i = 0; i < _targets.Count; i++)
            {
                Transform target = _targets[i];
                if (target == null) continue;   // 父级已不在 ⇒ 其子物体必然已随之销毁

                // 倒序：每次 SetParent 都会让 childCount 减一，正序遍历会漏项
                for (int c = target.childCount - 1; c >= 0; c--)
                {
                    target.GetChild(c).SetParent(null, true);
                }
            }
        }
    }
}
