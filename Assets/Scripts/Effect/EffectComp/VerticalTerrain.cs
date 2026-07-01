using GameContract;
using UnityEngine;

namespace EffectComp
{
    /// <summary>
    /// 使单位强制垂直地面
    /// </summary>
    public class VerticalTerrain : MonoBehaviour
    {
        public bool Disposable;
        private LayerMask terrainLayer;

        private void Start()
        {
            terrainLayer = LayerDefinition.GroundLayers;
            
            if (Disposable)
            {
                SetNormal(1);
                Destroy(this);
            }
        }



        void Update()
        {
            SetNormal(Time.deltaTime * 3);

        }


        public void SetNormal(float tick)
        {
            //RaycastHit hit;
            //Vector3 rayDirection = Vector3.down;
            /*
            if (Physics.Raycast(transform.position, rayDirection, out hit, 20, terrainLayer))
            {
                //Debug.DrawLine(transform.position, hit.point);
                //Debug.DrawRay(hit.point, hit.normal,Color.red);
                Vector3 groundNormal = hit.normal;
                Vector3 right = Vector3.Cross(groundNormal, transform.forward).normalized;
                Vector3 forward = Vector3.Cross(right, groundNormal).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(forward, groundNormal);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 3f);
                //Debug.DrawRay(hit.point, transform.forward, Color.blue);
            }*/

            //if (Physics.Raycast(transform.position, rayDirection, out hit, 20, terrainLayer))
            //{
            // Vector3 groundNormal = hit.normal;
            Vector3 groundNormal = TerrainUtils.GetNormal(transform.position);
            // 获取父对象的前方向
            Vector3 parentForward = transform.parent != null ? transform.parent.forward : transform.forward;

            // 确保父对象前方向与地面法线不平行
            if (Vector3.Dot(groundNormal, parentForward) < 0.9f)
            {
                // 计算物体应该朝向的方向（垂直于地面且与父对象前方向一致）
                Vector3 forward = Vector3.Cross(groundNormal, Vector3.Cross(parentForward, groundNormal)).normalized;

                // 确保forward方向正确
                if (Vector3.Dot(forward, parentForward) < 0)
                    forward = -forward;

                // 创建目标旋转
                Quaternion targetRotation = Quaternion.LookRotation(forward, groundNormal);

                // 平滑旋转
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, tick);
            }
            //Debug.DrawLine(transform.position, transform.position + groundNormal, Color.red, Time.deltaTime * 2);

            //Debug.DrawLine(transform.position, transform.position + transform.rotation * Vector3.down,Color.blue,Time.deltaTime*2);
            //}

        }

        
    }
}