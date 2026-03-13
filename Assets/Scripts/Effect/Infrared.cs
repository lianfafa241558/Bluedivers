using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Infrared : MonoBehaviour
{


    [Range(-45f, 45f)]
    public float Angle;

    [HideInInspector]
    public Transform RayGo;
    [HideInInspector]
    public LineRenderer line;
    protected Transform sphere;
   

    void Start()
    {
        line = GetComponent<LineRenderer>();
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
        if (Physics.Raycast(new Ray(transform.position, vector),out var hit, 300,FpsHelper.GetHittableLayers(99)))
        {
            line.SetPosition(1, hit.point);
            if(sphere) sphere.position = hit.point;
            RayGo = hit.transform;
        }
        else
        {
            line.SetPosition(1, transform.position + vector * 300);
            if (sphere) sphere.position = transform.position+300*Vector3.down;
            RayGo = null;
        }
        
    }
}
