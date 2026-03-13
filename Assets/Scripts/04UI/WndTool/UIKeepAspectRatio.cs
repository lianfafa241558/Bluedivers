using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;
using Utils;

/// <summary>
/// 保持UI的宽高比(用于各种神必的布局)
/// </summary>
public class UIKeepAspectRatio : MonoBehaviour
{
    [SerializeField]
    Vector2 StandardSize;
    [SerializeField]
    bool ReferenceYAxis=true;

    RectTransform rect;
    private void Start()
    {
        rect = transform as RectTransform;
    }

    private void FixedUpdate()
    {
        //宽高比发生变化时
        if (Mathf.Abs(Tool.Round(rect.sizeDelta.y/ rect.sizeDelta.x,2)-Tool.Round(StandardSize.y / StandardSize.x, 2))>0.01f)
        {

            if (ReferenceYAxis)
            {
                var scaleY = rect.sizeDelta.y / StandardSize.y;
                rect.sizeDelta = StandardSize * scaleY;
            }
            else
            {
                var scaleX = rect.sizeDelta.x / StandardSize.x;
                rect.sizeDelta = StandardSize * scaleX;
            }
            
            //Debug.LogError("修改为"+ StandardSize * Mathf.Max(scaleX, scaleY));
        }


    }


}
