using UnityEngine;

/// <summary>
/// 根据玩家所距离的距离来放大/缩小线的宽度
/// </summary>
public class AdjustLineOnDistance : MonoBehaviour
{

    public float baseWidth=0.1f,disScale=0.01f;
    LineRenderer m_line;

    void Start()
    {
        m_line = GetComponent<LineRenderer>();
    }
    // Update is called once per frame
    void Update()
    {
        if (!Camera.main) return;
        //例:scale=0.01;距离100宽度+10*0.01=0.1
        m_line.startWidth = m_line.endWidth = baseWidth+Mathf.Sqrt(Vector3.Distance(Camera.main.transform.position,transform.position))* disScale;
    }
}
