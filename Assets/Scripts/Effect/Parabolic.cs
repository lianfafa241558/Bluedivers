using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;

public class Parabolic : MonoBehaviour
{

    [Range(0.1f, 2f)]
    [CustomLabel("每段距离间隔")]
    public float lenght = 1f;
    //[HideInInspector]
    public Transform RayGo;
    [HideInInspector]
    public LineRenderer line;
    protected Transform sphere;
    private WeaponController m_weapon;

    void Start()
    {
        line = GetComponent<LineRenderer>();
        m_weapon = GetComponentInParent<WeaponController>();
        sphere = transform.GetChild(0);
        sphere.gameObject.layer = 0;
        //gameObject.layer = 0;
        sphere.gameObject.SetActive(true);
    }
    Vector3 lastPos;
    Vector3[] posList = new Vector3[50];
    void Update()
    {
        if (lastPos!= transform.position)
        {
            float speed = m_weapon.CurrentSpeed;
            float gravity = m_weapon.CurrentGravity;
            float paragraph = lenght / speed;

            int count = 1;
            RayGo = null;
            posList[0] = transform.position+ m_weapon.ProjectilePrefab.InheritedMuzzleVelocity * paragraph;

            Vector3 velocity = transform.forward * speed;
            for (int i = 1; i <50; ++i)
            {
                velocity += Vector3.down * gravity * paragraph;

                if (Physics.Raycast(posList[i-1], velocity * paragraph, out var hit, speed * paragraph*2, FpsHelper.GetHittableLayers(speed)))
                {
                    RayGo = hit.transform;
                    posList[i] = hit.point;
                    count = i+1;
                    break;
                }
                posList[i] = posList[i - 1] + velocity * paragraph;
            }
            line.positionCount = count;
            //写入数据
            for (int i = 0; i < count; ++i)
            {
                line.SetPosition(i, posList[i]);
            }
            //保底有1位（自己的位置）
            sphere.position = posList[count - 1];

        }
        lastPos = transform.position;

    }
}
