using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NightLight : MonoBehaviour
{
    [SerializeField]
    GameObject[] arr;


    private void Awake()
    {
        GlobalEventSub.OnDaySwitch += OnDatSwitch;
    }
    private void OnDestroy()
    {
        GlobalEventSub.OnDaySwitch -= OnDatSwitch;
    }

    private void OnDatSwitch(bool isNoon)
    {
        foreach (var item in arr)
        {
            item.SetActive(!isNoon);
        }
    }
}
