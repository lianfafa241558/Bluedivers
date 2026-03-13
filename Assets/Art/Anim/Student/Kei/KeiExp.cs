using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;

public class KeiExp : MonoBehaviour
{

    public new Renderer renderer;
    public float time = 0;
    public int exp=0;

    private MpbController mpb;

    void Start()
    {
        mpb = new(renderer);
    }

    void Update()
    {
        if ((time += Time.deltaTime)>3)
        {
            time = 0;
            mpb.Set("_Expression", ++exp).Apply();
        }
    }
}
