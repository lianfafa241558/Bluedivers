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

        /// <summary>任意一个 Renderer 当前可见则返回 true（MPB 对列表整体同步，只要有一个在渲染就需要保持最新）</summary>
        public bool IsAnyVisible()
        {
            for (int i = 0, l = arr.Count; i < l; ++i)
            {
                if (arr[i] != null && arr[i].isVisible) return true;
            }
            return false;
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
        public MpbController Get(string name,out Vector4 value)
        {
            value= mpb.GetVector(name);
            return this;
        }
        public MpbController SetOffsetX(string name, float value)
        {
            var vector = GetVectorOrDefault(name);
            vector.z = value;
            mpb.SetVector(name, vector);
            return this;
        }
        public MpbController SetOffsetY(string name, float value)
        {
            var vector = GetVectorOrDefault(name);
            vector.w = value;
            mpb.SetVector(name, vector);
            return this;
        }

        /// <summary>
        /// MPB 中存在该属性则读出；否则回落到第一个 Renderer 的材质当前值作为初值
        /// （MaterialPropertyBlock.GetVector 对未设置过的属性只会返回零向量，不会读材质）
        /// </summary>
        private Vector4 GetVectorOrDefault(string name)
        {
            if (mpb.HasVector(name)) return mpb.GetVector(name);
            var renderer = arr.Find(r => r != null && r.sharedMaterial != null);
            return renderer != null ? renderer.sharedMaterial.GetVector(name) : Vector4.zero;
        }

        /// <summary>int 版本：调用方缓存 Shader.PropertyToID 后，每帧免去字符串哈希查找</summary>
        public MpbController Set(int name, float value)
        {
            mpb.SetFloat(name, value);
            return this;
        }

        public MpbController SetOffsetX(int name, float value)
        {
            var vector = GetVectorOrDefault(name);
            vector.z = value;
            mpb.SetVector(name, vector);
            return this;
        }

        public MpbController SetOffsetY(int name, float value)
        {
            var vector = GetVectorOrDefault(name);
            vector.w = value;
            mpb.SetVector(name, vector);
            return this;
        }

        private Vector4 GetVectorOrDefault(int name)
        {
            if (mpb.HasVector(name)) return mpb.GetVector(name);
            var renderer = arr.Find(r => r != null && r.sharedMaterial != null);
            return renderer != null ? renderer.sharedMaterial.GetVector(name) : Vector4.zero;
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