using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>单向护盾</summary>
    public class IgnoreHitDetection : MonoBehaviour
    {
        [InspectorName("单向盾")]
        public bool Unidirectional;
    }
}