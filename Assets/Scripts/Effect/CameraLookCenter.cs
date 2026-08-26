
using UnityEngine;

internal class CameraLookCenter : MonoBehaviour
{
    private void LateUpdate()
    {
        transform.position = FpsHelper.PlayerCameraLookPoint;
    }
}