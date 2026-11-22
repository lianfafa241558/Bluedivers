using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Utils
{
    /// <summary>
    /// Start 时从候选列表里随机启用一项、其余禁用，用于同一位置的多套外观/模型随机。
    /// 只影响 SetActive，不改数据、不发网络事件 —— 各个客户端各摇各的，不要拿它做影响战斗的随机。
    /// </summary>
    [AddComponentMenu("工具/随机显示其中一项")]
    public class ShowRandomOne : MonoBehaviour
    {
        [SerializeField]
        [InspectorName("候选对象")]
        private List<Transform> _candidates = new List<Transform>();

        /// <summary>本次被选中的项，未执行或列表为空时为 null。</summary>
        public Transform Current { get; private set; }

        private void Start()
        {
           
            // 先数有效项再摇号：直接对原列表摇会把 null 空槽也摇中，结果一个都不显示
            int validCount = 0;
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i] != null) validCount++;
            }

            int pick = Random.Range(0, validCount);
            Current = null;

            for (int i = 0; i < _candidates.Count; i++)
            {
                Transform item = _candidates[i];
                if (item == null) continue;

                bool isPicked = pick == 0;
                if (isPicked)
                {
                    Current = item;
                }

                // 选中项在 prefab 里常是关的，所以一律显式开/关，不依赖初始状态
                item.gameObject.SetActive(isPicked);
                pick--;
            }
        }
    }
}
