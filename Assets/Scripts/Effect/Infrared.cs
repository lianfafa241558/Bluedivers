using System.Net;
using TMPro;
using Unity.FPS.Game;
using UnityEngine;

public class Infrared : MonoBehaviour
{

    [SerializeField]
    WeaponBaseController weapon;

    [Range(-45f, 45f)]
    public float Angle;

    [HideInInspector]
    public Transform RayGo;
    [HideInInspector]
    public LineRenderer line;

    [SerializeField]
    protected Transform sphere;
    [SerializeField]
    protected TextMeshPro disance;

    void Start()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        if (transform.childCount > 0)
        {
            sphere = transform.GetChild(0);
            sphere.gameObject.SetActive(true);
            //gameObject.layer = 0;
            sphere.gameObject.layer = 0;
        }
    }


    void Update()
    {
        line.SetPosition(0, transform.position);
        Vector3 vector = Quaternion.Euler(0, 0, Angle) * transform.forward;
        var maxRange = weapon ? weapon.CurrentWeaponRange : 300;
        if (Physics.Raycast(new Ray(transform.position, vector),out var hit, maxRange, FpsHelper.GetHittableLayers(99)))
        {
            line.SetPosition(1, hit.point);
            if(sphere) sphere.position = hit.point;
            RayGo = hit.transform;
            if (disance)
            {
                var dis = (hit.point - transform.position).magnitude;
                disance.text = Mathf.FloorToInt(dis) + "m";
                disance.transform.localScale = Mathf.Sqrt(dis)*Vector3.one;
            }
        }
        else
        {
            line.SetPosition(1, transform.position + vector * maxRange);
            if (sphere) sphere.position = transform.position+ maxRange * Vector3.down;
            if (disance) { 
                disance.text = maxRange + "m";
                disance.transform.localScale = Mathf.Sqrt(maxRange) * Vector3.one;
            }
            RayGo = null;
        }
        
    }
}
