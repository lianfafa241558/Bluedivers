
using UnityEngine;

namespace FPSGame.Gameplay
{

    public partial class PlayerController
    {

        /// <summary>当前正在出图的相机：倒地看 <c>PlayerDownCamera</c>，平时看 <c>PlayerCamera</c>。</summary>
        /// <para>▍为什么需要它：倒地时主相机是**整台 GameObject 被 SetActive(false)**，
        /// 写在它身上的剔除配置（<c>Camera.layerCullDistances</c>）与挂在它上面的剔除组件都随之停摆
        /// ⇒ 需要一份"现在到底哪台在渲染"的口径，供 <see cref="GameplayCamera"/> 与剔除组件取。</para>
        public Camera ActiveCamera
        {
            get
            {
                if (PlayerDownCamera != null && PlayerDownCamera.isActiveAndEnabled) return PlayerDownCamera;
                return PlayerCamera;
            }
        }

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
}
