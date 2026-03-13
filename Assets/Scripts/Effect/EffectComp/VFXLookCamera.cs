using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VFXLookCamera : MonoBehaviour
{
    void Update()
    {
        if (Camera.main)
        {
            //transform.LookAt(Camera.main.transform);
            transform.forward = -Camera.main.transform.forward;
        }
    }
}
