using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonPressDetector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler//, IDragHandler
{
    public Action<PointerEventData> OnUpdate, OnDown, OnUp;

    [Range(0, 2)]
    [SerializeField]
    private float EnterScale = 1;
    [Range(0, 10)]
    [SerializeField]
    private float ScaleSpeed = 1;

    [SerializeField]
    private bool isButtonPressed = false;
    private float downTime, upTime;
    // 鼠标按下时
    public void OnPointerDown(PointerEventData eventData)
    {
        downTime = Time.time;
        isButtonPressed = true;
        OnDown?.Invoke(eventData);
    }

    // 鼠标抬起时
    public void OnPointerUp(PointerEventData eventData)
    {
        isButtonPressed = false;
        upTime = Time.time;
        OnUp?.Invoke(eventData);
        transform.localScale = Vector3.one;
    }
    /*
    public void OnDrag(PointerEventData eventData)
    {
        
    }
    */
    public bool Get() => isButtonPressed;
    public bool GetUp() => upTime == Time.time;
    public bool GetDown() => downTime == Time.time;

    private void Update()
    {
        OnUpdate?.Invoke(null);
        if (EnterScale != 1)
        {
            if (isButtonPressed && Mathf.Abs(transform.localScale.x - EnterScale)>0.01f)
            {
                transform.localScale = Vector2.Lerp(transform.localScale, Vector2.one * EnterScale, Time.unscaledDeltaTime * ScaleSpeed);
            }
            else if (!isButtonPressed && Mathf.Abs(transform.localScale.x - 1) > 0.01f)
            {
                transform.localScale = Vector2.Lerp(transform.localScale, Vector2.one, Time.unscaledDeltaTime * ScaleSpeed);
            }
        }
    }
}