#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class DisplayBone : MonoBehaviour
{
    public float size=0.1f;
    void OnDrawGizmos()
    {
       
        var list = transform.GetComponentsInChildren<Transform>();
        foreach (var item in list)
        {
            
            if (Selection.activeGameObject == item.gameObject)
            {
                Gizmos.color = new Color(1, 1, 0, 1); 
            }
            else
            {
                Gizmos.color = new Color(1, 1, 1, 1); 
            }
            if (item.parent) Gizmos.DrawLine(item.position, item.parent.position);
            Gizmos.DrawWireSphere(item.position, size);


            //是子物体就不显示
            if (Selection.activeGameObject==null||!item.IsChildOf(Selection.activeGameObject.transform))
            {
                // 保存当前矩阵
                Matrix4x4 originalMatrix = Gizmos.matrix;

                // 设置Gizmos使用物体的变换矩阵
                Gizmos.matrix = item.localToWorldMatrix;

                Gizmos.color = new Color(1, 0.5f, 0.5f, 1);
                var box = item.GetComponents<BoxCollider>();
                for(int i = 0; i < box.Length; ++i)
                {
                    Gizmos.DrawWireCube(box[i].center, box[i].size);
                }
                var sphere = item.GetComponents<SphereCollider>();
                for (int i = 0; i < sphere.Length; ++i)
                {
                    Gizmos.DrawWireSphere(sphere[i].center, sphere[i].radius);
                }

                var capsule = item.GetComponents<CapsuleCollider>();
                for (int i = 0; i < capsule.Length; ++i)
                {
                    // 绘制胶囊线框
                    DrawCapsuleWireframe(sphere[i].center, capsule[i].height, capsule[i].radius, capsule[i].direction, item);
                }
                Gizmos.matrix = originalMatrix;
            }



        }
    }


    void DrawCapsuleWireframe(Vector3 center, float height, float radius, int direction, Transform objTransform)
    {
        // 根据胶囊方向计算半高
        float halfHeight = height * 0.5f;
        Vector3 upAxis = GetDirectionVector(direction);

        // 计算球体中心位置
        Vector3 topSphereCenter = center + upAxis * (halfHeight - radius);
        Vector3 bottomSphereCenter = center - upAxis * (halfHeight - radius);

        // 绘制顶部半球
        Gizmos.DrawWireSphere(topSphereCenter, radius);

        // 绘制底部半球  
        Gizmos.DrawWireSphere(bottomSphereCenter, radius);

        // 绘制连接柱体
        DrawCylinderConnections(topSphereCenter, bottomSphereCenter, radius, upAxis, objTransform);
    }

    Vector3 GetDirectionVector(int direction)
    {
        switch (direction)
        {
            case 0: return Vector3.right;    // X轴
            case 1: return Vector3.up;       // Y轴  
            case 2: return Vector3.forward; // Z轴
            default: return Vector3.up;
        }
    }

    void DrawCylinderConnections(Vector3 topCenter, Vector3 bottomCenter, float radius, Vector3 upAxis, Transform objTransform)
    {
        // 计算垂直于upAxis的两个轴
        Vector3 rightAxis = (Mathf.Abs(Vector3.Dot(upAxis, Vector3.right))) < 0.9f ? Vector3.right : Vector3.forward;
        Vector3 forwardAxis = Vector3.Cross(upAxis, rightAxis).normalized;
        rightAxis = Vector3.Cross(forwardAxis, upAxis).normalized;

        // 绘制四个连接边
        int segments = 8;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            Vector3 offset = rightAxis * Mathf.Cos(angle) * radius + forwardAxis * Mathf.Sin(angle) * radius;

            Gizmos.DrawLine(topCenter + offset, bottomCenter + offset);
        }
    }
}

#endif