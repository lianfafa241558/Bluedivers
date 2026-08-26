
using UnityEngine;

public partial class PlayerController
{

    private void LookPointHandle()
    {
        // 从屏幕中心(视口0.5,0.5)发射射线采样目标点
        Ray ray = PlayerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        float rayDistance = 500f;
        Vector3 pos= ray.origin + ray.direction * rayDistance;
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, FpsHelper.GetHittableLayers(99), QueryTriggerInteraction.Collide))
        {
            pos = hit.point;
        }
        FpsHelper.SetPlayerCameraLookPoint(pos);
        //Debug.DrawLine(PlayerCamera.transform.position, pos, Color.red, 1f);
    }
}