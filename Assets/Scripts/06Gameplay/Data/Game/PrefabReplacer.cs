using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 把物体替换成另一个预制体，用于地图变体。
    /// </summary>
    [AddComponentMenu("工具/预制体替换")]
    public class PrefabReplacer : MonoBehaviour
    {
        [System.Serializable]
        public struct ReplacementDefinition
        {
            public GameObject SourcePrefab;
            public GameObject TargetPrefab;
        }

        public bool SwitchOrder;
        public List<ReplacementDefinition> Replacements = new List<ReplacementDefinition>();
    }
}
