using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonPressDetector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public Action<PointerEventData> Update,Down,Up;

    [SerializeField]
    private bool isButtonPressed = false;
    private float downTime, upTime;
    // 当按钮被按下时调用
    public void OnPointerDown(PointerEventData eventData)
    {
        downTime = Time.time;
        isButtonPressed = true;
        Down?.Invoke(eventData);
    }

    // 当按钮抬起时调用
    public void OnPointerUp(PointerEventData eventData)
    {
        isButtonPressed = false;
        upTime = Time.time;
        Up?.Invoke(eventData);
    }
    public void OnDrag(PointerEventData eventData)
    {
        Update?.Invoke(eventData);
    }

    public bool Get() => isButtonPressed;
    public bool GetUp() => upTime == Time.time;
    public bool GetDown() => downTime==Time.time;


}
