using System.Collections;
using System.Collections.Generic;
using FPSGame.Gameplay;
using UnityEngine;

namespace FPSGame.Effect
{
/// <summary>
/// 昼夜切换灯组
/// </summary>
[AddComponentMenu("场景效果/昼夜切换灯组", 30)]
public class NightLight : MonoBehaviour
{
    [SerializeField]
    GameObject[] arr;


    private void Awake()
    {
        GlobalEventBus.OnDaySwitch += OnDatSwitch;
    }
    private void OnDestroy()
    {
        GlobalEventBus.OnDaySwitch -= OnDatSwitch;
    }

    private void OnDatSwitch(bool isNoon)
    {
        foreach (var item in arr)
        {
            item.SetActive(!isNoon);
        }
    }
}
}
