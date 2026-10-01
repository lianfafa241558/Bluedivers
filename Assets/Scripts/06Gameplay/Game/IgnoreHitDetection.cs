using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>单向护盾</summary>
    [AddComponentMenu("单位/单向护盾")]
    public class IgnoreHitDetection : MonoBehaviour
    {
        [InspectorName("单向盾")]
        public bool Unidirectional;
    }
}
