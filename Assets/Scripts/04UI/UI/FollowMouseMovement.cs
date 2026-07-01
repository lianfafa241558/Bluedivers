using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using WndTools;

public class FollowMouseMovement : MonoBehaviour
{
    [SerializeField]
    private bool Rect;
    public float Offest=10,Speed=1;
    private RectTransform bg;
    private Vector3 startPos;

    void Start()
    {
        
        if (Rect)
        {
            bg = transform.RectTransform();
            startPos = bg.anchoredPosition;
        }
        else
        {
            startPos = transform.localPosition;
        }
    }

    void Update()
    {
        Vector3 pos = Input.mousePosition / Tool.ScreenSize2D * -2 + Vector2.one;
        pos = new(Mathf.Clamp(pos.x,-1,1), Mathf.Clamp(pos.y,-1,1));
        if (Rect)
        {
            bg.anchoredPosition = Vector2.Lerp(bg.anchoredPosition, pos * Offest, Speed * Time.deltaTime);
        }
        else
        {
            pos = new(pos.x,pos.y,0);
            transform.localPosition = Vector3.Lerp(transform.localPosition, startPos+ pos * Offest, Speed * Time.deltaTime);
        }

    }
}
