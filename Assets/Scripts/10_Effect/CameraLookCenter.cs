
using UnityEngine;
using FPSGame.Gameplay;

namespace FPSGame.Effect
{

/// <summary>
/// 让相机始终看向指定中心点。
/// </summary>
[AddComponentMenu("特效/相机朝中心")]
internal class CameraLookCenter : MonoBehaviour
{
    private void LateUpdate()
    {
        transform.position = FpsHelper.PlayerCameraLookPoint;
    }
}
}
