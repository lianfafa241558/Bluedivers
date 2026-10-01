using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;
using FPSGame.Core;
using FPSGame.Attributes;

namespace FPSGame.Utils
{
    public static partial class Tool
{


        public static Vector3 ScreenSize => new(Screen.width, Screen.height, 0);

        public static Vector2 ScreenSize2D => new(Screen.width, Screen.height);

        public static float ScreenAspect => Screen.width / (float)Screen.height;


        /// <summary>
        ///世界坐标转屏幕坐 ?
        /// </summary>
        /// <returns></returns>
        public static Vector3 WorldPosToScreenPos(Vector3 vector)
        {
            var re = Camera.main.WorldToScreenPoint(vector);
            return re * Mathf.Sign(re.z);
        }

        /// <summary>
        /// 鼠标位置转世界坐 ?
        /// </summary>
        /// <returns></returns>
        public static Vector3 MouthPosToWorldPos()
        {
            Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 100);
            return hit.point;
        }
        /// <summary>
        /// 坐标在屏幕内
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        public static bool IsScreenVisible(Vector3 worldPos)
        {
            if (!Camera.main.IsValid()) return false;
            Vector3 viewPos = Camera.main.WorldToViewportPoint(worldPos);
            bool inViewport = viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1;
            bool inFront = Vector3.Dot(Camera.main.transform.forward, (worldPos - Camera.main.transform.position).normalized) > 0;
            return inViewport && inFront;
        }

        /// <summary>
        /// 判断b是否在a的前 ?
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        //public static bool InFront(Transform a, Transform b)=> Vector3.Angle(b.position - a.position, a.forward) < 90f;
        public static bool InFront(Transform a, Transform b) => Vector3.Dot(b.position - a.position, a.forward) > 0;


        public static float NavDis(Vector3 now, Vector3 to)
        {
            NavMeshPath path = new();
            //让y ?，保证寻路走得到
            NavMesh.CalculatePath(now - new Vector3(0, now.y, 0), to - new Vector3(0, to.y, 0), NavMesh.AllAreas, path);
            //agent.CalculatePath(vector,path);
            float dis = 0;
            //=1就是走不到，直接在原 ?
            if (path.corners.Length > 1)
            {
                for (int i = 0; i < path.corners.Length - 1; i++) dis += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                dis += Vector3.Distance(path.corners[path.corners.Length - 1], to);
            }
            Debug.DrawLine(to, to + new Vector3(0, 3, 0), Color.yellow, 5f);

            return dis;
        }
        public static Vector2 RectLocPos(RectTransform rect, Vector2 vector, ShapeType shape = ShapeType.Rectangle)
        {
            Vector2 size = rect.sizeDelta * 0.5f;
            Vector2 pos = vector - rect.pivot;
            pos.y *= -1;
            //Debug.LogWarning("输入"+ vector + "相对位置" + pos);
            switch (shape)
            {
                case ShapeType.Circle:
                    //宽高的平均 ?* 方向
                    return (size.x + size.y) * 0.5f * pos.normalized;
                case ShapeType.Ellipse:
                    //* 方向
                    return size * pos.normalized;
                case ShapeType.Prismatic:
                    //* 方向/方向的长 ?
                    return size * pos / (Mathf.Abs(pos.x) + Mathf.Abs(pos.y));
                default:
                    return size * pos;
            }
        }

        public static Vector2 RectLocPos(RectTransform rect, TextAnchor anchor, ShapeType shape = ShapeType.Rectangle)
        {
            return RectLocPos(rect, new Vector2(((int)anchor % 3) * 0.5f, Mathf.Floor((int)anchor / 3) * 0.5f), shape);
        }


    }
}
