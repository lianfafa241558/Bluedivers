using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonEnterDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action<PointerEventData> Enter,Exit,In;
    [Range(0, 2)]
    [SerializeField]
    private float EnterScale = 1;
    [Range(0, 10)]
    [SerializeField]
    private float ScaleSpeed = 1;

    [SerializeField]
    private GameObject[] ControlGo;

    [SerializeField]
    private bool isButtonEnter = false;
    private float enterTime, exitTime;



    public bool InEnter
    {
        get => isButtonEnter;
        set
        {
            isButtonEnter = value;
        }
    }

    public bool Get() => isButtonEnter;
    public bool GetExit() => exitTime == Time.time;
    public bool GetEnter() => enterTime==Time.time;

    public void OnPointerEnter(PointerEventData eventData)
    {
        enterTime = Time.time;
        isButtonEnter = true;
        foreach (var item in ControlGo)
        {
            item.SetActive(true);
        }
        Enter?.Invoke(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isButtonEnter = false;
        exitTime = Time.time;
        foreach (var item in ControlGo)
        {
            item.SetActive(false);
        }
        Exit?.Invoke(eventData);
        //if (EnterScale != 1) transform.localScale /= EnterScale;
    }

    private void Update()
    {
        if(EnterScale != 1)
        {
            if (isButtonEnter && transform.localScale.x < EnterScale)
            {
                transform.localScale = Vector2.Lerp(transform.localScale,Vector2.one* EnterScale,Time.unscaledDeltaTime* ScaleSpeed);
            }
            else if (!isButtonEnter && transform.localScale.x > 1)
            {
                transform.localScale = Vector2.Lerp(transform.localScale, Vector2.one, Time.unscaledDeltaTime * ScaleSpeed);
            }
        }
        if (isButtonEnter)
        {
            In?.Invoke(null);
        }
    }
}
