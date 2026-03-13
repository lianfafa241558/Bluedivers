using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Utils
{
    public static class TransformUtils
    {
        public static Transform GetChild(this Transform transform, params int[] values)
        {
            //故意让他报错显示的
            //可以阻止，但是还是会null或者出现不正确的结果，所以还不如让他报错
            Transform re = transform;
            for (int i = 0; i < values.Length; ++i)
            {

                if (re != null)
                {
                    re = re.GetChild(values[i]);
                }
                else
                {
                    Debug.LogError("找不到" + transform + "的" + values + " 停留在" + re);
                    re = null;
                }
            }
            return re;
        }
        public static Transform GetChild(this Component comp, params int[] values)
        {
            return GetChild(comp.transform, values);
        }
        public static RectTransform GetRectChild(this Transform transform, params int[] values)
        {
            return (RectTransform)GetChild(transform, values);
        }
        public static RectTransform GetRectChild(this Component comp, params int[] values)
        {
            return (RectTransform)GetChild(comp.transform, values);
        }


        public static RectTransform GetRect(this Transform transform)
        {
            return (RectTransform)transform;
        }
        public static bool TryGetComponentInChildren<T>(this Transform transform, out T component) where T : MonoBehaviour
        {
            component = transform.GetComponentInChildren<T>();
            return component;
        }

        public static bool TryGetComponentInParent<T>(this Transform transform, out T component) where T : MonoBehaviour
        {
            component = transform.GetComponentInParent<T>();
            return component;
        }

        /// <summary>
        /// 搜索组件
        /// </summary>
        /// <param name="isAll">一般来说不重复的组件填false（如Wnd），可能会重复的组件true(如碰撞箱)</param>
        public static List<T> GetComponentsInChildren<T>(this Transform transform, int maxLevel, bool isAll = false, int nowLevel = 0) where T : class
        {
            List<T> results = new();
            if (typeof(T).IsInterface)
            {
                var allComponents = transform.GetComponents<Component>();
                foreach (var comp in allComponents)
                {
                    if (comp.GetType().GetInterfaces().Contains(typeof(T)))
                    {
                        //Debug.LogError("找到了了"+comp.gameObject);
                        results.Add(comp as T);
                        if (!isAll) break;
                    }
                }
            }
            else
            {
                if (isAll)
                {
                    return transform.GetComponents<T>().ToList();
                }
                else
                {
                    if (transform.TryGetComponent(out T comp))
                    {
                        return new() { comp };
                    }
                }
            }

            if (nowLevel == maxLevel) return results;
            foreach (Transform child in transform)
            {
                results.AddRange(GetComponentsInChildren<T>(child, maxLevel, isAll, nowLevel + 1));
            }

            return results;
        }


        public static void ForEach(this Transform transform, System.Action<Transform, int> action)
        {
            for (int i = 0, l = transform.childCount; i < l; ++i)
            {
                action.Invoke(transform.GetChild(i), i);
            }
        }
        public static void ForEach(this Transform transform, System.Action<Transform> action)
        {
            for (int i = 0, l = transform.childCount; i < l; ++i)
            {
                action.Invoke(transform.GetChild(i));
            }
        }

        public static void ForEach<T>(this Transform transform, System.Action<T, int> action)
            where T : MonoBehaviour
        {
            for (int i = 0, l = transform.childCount; i < l; ++i)
            {
                action.Invoke(transform.GetChild(i).GetComponent<T>(), i);
            }
        }
        public static void ForEach<T>(this Transform transform, System.Action<T> action)
                where T : MonoBehaviour
        {
            for (int i = 0, l = transform.childCount; i < l; ++i)
            {
                action.Invoke(transform.GetChild(i).GetComponent<T>());
            }
        }

        public static Transform Find(this Transform transform, System.Predicate<Transform> match)
        {
            for (int i = 0, l = transform.childCount; i < l; ++i)
            {
                if (match(transform.GetChild(i)))
                {
                    return transform.GetChild(i);
                }
            }
            return null;
        }



        public static List<Transform> FindAll(this Transform trans, System.Predicate<Transform> match)
        {
            List<Transform> list = new();
            for (int i = 0; i < trans.childCount; i++)
            {
                if (match(trans.GetChild(i)))
                {
                    list.Add(trans.GetChild(i));
                }
            }
            return list;
        }


        public static bool InRectTransform(Vector2 pos, RectTransform rect)
        {
            Vector2 start = new Vector2(rect.position.x, rect.position.y) + rect.rect.position;
            return Tool.In2D(pos, start, start + rect.rect.size);
        }

        public static void Clear<T>(this T[] arr)
        {
            System.Array.Clear(arr, 0, arr.Length);
        }



        public static GameObject SceenFind(string objectName)
        {
            // 获取场景中所有物体
            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(true);
            foreach (GameObject obj in allObjects)
            {
                if (obj.name == objectName)
                {
                    return obj; // 返回找到的物体
                }
            }
            return null; // 如果没有找到，返回null
        }

        /// <summary>
        /// 递归的设置子物体层级
        /// </summary>
        /// <param name="go"></param>
        /// <param name="layer"></param>
        /// <param name="maxLevel"></param>
        /// <param name="nowLevel"></param>
        public static void SetChildLayer(this GameObject go, int layer, int maxLevel, int nowLevel = 0)
        {
            go.layer = layer;
            if (nowLevel == maxLevel) return;
            foreach (Transform child in go.transform)
            {
                SetChildLayer(child.gameObject, layer, maxLevel, nowLevel + 1);
            }
        }
        /// <summary>
        /// 使用内置方法的设置子物体层级
        /// </summary>
        /// <param name="go"></param>
        /// <param name="layer"></param>
        /// <param name="allChildren"></param>
        public static void SetChildLayer(this GameObject go, int layer, bool allChildren)
        {
            go.layer = layer;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(allChildren))
            {
                t.gameObject.layer = layer;
            }
        }





    }
}