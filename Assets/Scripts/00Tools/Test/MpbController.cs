using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Utils
{

    public class MpbController
    {
        private MaterialPropertyBlock mpb;
        private List<Renderer> arr;

        public MpbController(Transform trans)
        {
            arr = trans.GetComponentsInChildren<Renderer>().ToList();
            mpb = new();
        }
        public MpbController(Renderer[] arr)
        {
            this.arr = arr.ToList();
            mpb = new();
        }
        public MpbController(Renderer item)
        {
            this.arr = new () { item };
            mpb = new();
        }

        public void Add(Transform trans)
        {
            arr.AddRange(trans.GetComponentsInChildren<Renderer>());
            arr = arr.Distinct().ToList();
        }
        public void Remove(Transform trans)
        {
            var re = trans.GetComponentsInChildren<Renderer>();
            foreach(var item in re) arr.Remove(item);
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
        public MpbController Set(string name, Texture value)
        {
            mpb.SetTexture(name, value);
            return this;
        }
        public MpbController Set(string name, Vector4 value)
        {
            mpb.SetVector(name, value);
            return this;
        }

        public void Apply()
        {
            for (int i = 0, l = arr.Count; i < l; ++i)
            {
                if (arr[i] != null) arr[i].SetPropertyBlock(mpb);
                else Debug.LogError("mpb的第"+i+"个渲染器为空");
            }
        }
    }
}