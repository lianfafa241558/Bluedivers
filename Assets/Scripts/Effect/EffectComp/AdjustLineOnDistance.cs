using UnityEngine;

/// <summary>
/// 根据玩家所距离的距离来放大/缩小线的宽度(顺带了一个顶部碰撞)
/// </summary>
public class AdjustLineOnDistance : MonoBehaviour
{

    public float baseWidth=0.1f,disScale=0.01f;

    public bool useForward;//使用前方向作为方向

    LineRenderer m_line;
    RaycastHit hit;
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
        Vector3 dir = useForward ? transform.forward : transform.up;
        Vector3 dir2= useForward ? Vector3.forward : Vector3.up;
        if (Physics.Raycast(transform.position + dir, dir, out hit, 500))
        {
            m_line.SetPosition(1, dir2 * (hit.distance+1));
        }
        else
        {
            m_line.SetPosition(1, dir2 * 500);
        }
    }
}
