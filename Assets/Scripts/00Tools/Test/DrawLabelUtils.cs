using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Core
{
#if UNITY_EDITOR
    /// <summary>
    /// 方便显示文本的类，这个类只会在编辑器环境下被创建
    /// </summary>
    public class DrawLabelUtils : MonoBehaviour {
        private class DrawTextInfo {
            public float deathTime;
            public Vector3 pos;
            public Color color;
            public string text;
        }
        List<DrawTextInfo> TextInfos;

        private class DrawShapeInfo {
            public float deathTime;
            public Vector3 pos;
            public Color color;
            public ShapeType shape;
            public Vector3 size;
        }

        List<DrawShapeInfo> ShapeInfos;
        GUIStyle style;

        public void DrawLabel(Vector3 pos, Color color, string text, float time) {
            TextInfos.Add(new() {
                deathTime = Time.time + time,
                pos = pos,
                color = color,
                text = text,
            });
        }
        public void DrawShape(ShapeType shape, Vector3 pos, Color color, Vector3 size, float time) {
            ShapeInfos.Add(new() {
                deathTime = Time.time + time,
                shape = shape,
                pos = pos,
                color = color,
                size = size,
            });
        }

        private void Start()
        {
            TextInfos = new();
            ShapeInfos = new();
            style = new GUIStyle() {
                normal = new GUIStyleState() {
                    textColor = Color.red,  // 文字颜色
                },
                fontSize = 24,              // 字体大小
                //fontStyle = FontStyle.Bold, // 加粗
                alignment = TextAnchor.MiddleCenter // 居中对齐
            };

        }

        private void OnDrawGizmos()
        {
            float time = Time.time;
            for(int i = TextInfos.Count - 1; i >= 0; --i)
            {
                if (TextInfos[i].deathTime < time)
                {
                    TextInfos.RemoveAt(i);
                }
                else
                {
                    style.normal.textColor = TextInfos[i].color;

                    UnityEditor.Handles.Label(TextInfos[i].pos, TextInfos[i].text, style);

                }
            }
            for (int i = ShapeInfos.Count - 1; i >= 0; --i)
            {
                if (ShapeInfos[i].deathTime < time)
                {
                    ShapeInfos.RemoveAt(i);
                }
                else
                {
                    Gizmos.color = ShapeInfos[i].color;
                    switch (ShapeInfos[i].shape)
                    {
                        case ShapeType.Circle:
                            Gizmos.DrawWireSphere(ShapeInfos[i].pos, ShapeInfos[i].size.x);
                            break;
                        case ShapeType.Rectangle:
                            Gizmos.DrawWireCube(ShapeInfos[i].pos, ShapeInfos[i].size);
                            break;
                    }

                }
            }
        }

    }
#endif
}