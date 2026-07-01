using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FadingLight : MonoBehaviour
{
    [SerializeField]
    private AnimationCurve intensityCurve;

    [SerializeField]
    private AnimationCurve rangeCurve;
    [SerializeField]
    private Light _light;

    private float startTime;

    private void OnEnable()
    {
        startTime = Time.time;
    }

    private void Update()
    {
        _light.intensity = intensityCurve.Evaluate(Time.time- startTime);
        _light.range = rangeCurve.Evaluate(Time.time - startTime);
    }

}
