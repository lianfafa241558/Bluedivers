using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Utils
{

    public class MpbController
    {
        private MaterialPropertyBlock mpb;
        private Renderer[] arr;

        public MpbController(Transform trans)
        {
            List<Renderer> re = new();
            re.Add(trans.GetComponentInChildren<Renderer>());
            for (int i = 0; i < trans.childCount; i++)
            {
                re.Add(trans.GetChild(i).GetComponentInChildren<Renderer>());
            }
            arr = re.ToArray();
            mpb = new();
        }
        public MpbController(Renderer[] arr)
        {
            this.arr = arr;
            mpb = new();
        }
        public MpbController(Renderer item)
        {
            this.arr = new Renderer[] { item };
            mpb = new();
        }

        public MpbController Set(string name, float value)
        {
            mpb.SetFloat(name, value);
            return this;
        }
        public MpbController Set(string name, int value)
        {
            mpb.SetInt(name, value);
            return this;
        }
        public MpbController Set(string name, Color value)
        {
            mpb.SetColor(name, value);
            return this;
        }
        public MpbController Set(string name, Sprite value)
        {
            mpb.SetTexture(name, value.texture);
            return this;
        }
        public MpbController Set(string name, Vector4 value)
        {
            mpb.SetVector(name, value);
            return this;
        }

        public void Apply()
        {
            for (int i = 0, l = arr.Length; i < l; ++i)
            {
                arr[i].SetPropertyBlock(mpb);
            }
        }
    }
}