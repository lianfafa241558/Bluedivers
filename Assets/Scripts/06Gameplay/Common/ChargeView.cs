using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Gameplay
{

/// <summary>
/// 武器蓄力的视觉表现。
/// </summary>
[AddComponentMenu("特效/蓄力表现")]
public class ChargeView : MonoBehaviour
{
    [SerializeField]
    float StartScale,MaxScale;
    [SerializeField]
    Light m_light;

    private float lightBaseRange;

    void Awake()
    {
        if (m_light) lightBaseRange = m_light.range;
        UpdateCharget(0);
    }


    public void UpdateCharget(float value)
    {
        transform.localScale = Mathf.Lerp(StartScale, MaxScale, value) * Vector3.one;
        if (m_light) m_light.range = lightBaseRange * Mathf.Lerp(StartScale, MaxScale, value);
    }

}
}
