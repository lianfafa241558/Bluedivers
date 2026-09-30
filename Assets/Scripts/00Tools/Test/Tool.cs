using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;
using FPSGame.Core;
using FPSGame.Attributes;

namespace FPSGame.Utils
{
    public static partial class Tool
    {

#if UNITY_EDITOR
        public static void SetState(this AudioSource source, bool state)
        {
            if (state) source.Play();
            else source.Stop();
        }


        private static DrawLabelUtils drawLabelUtils;
#endif

        private static ExchangeTransformManager exchangeArea;
        static Tool()
        {
#if UNITY_EDITOR
            if (Application.isPlaying && GameObject.Find("GameRoot"))
            {
                drawLabelUtils = new GameObject("DrawLabelUtils").AddComponent<DrawLabelUtils>();
                drawLabelUtils.transform.parent = GameObject.Find("GameRoot").transform;
            }
#endif
            if (Application.isPlaying && GameObject.Find("GameRoot"))
            {
                exchangeArea = new GameObject("ExchangeArea").AddComponent<ExchangeTransformManager>();
                exchangeArea.transform.parent = GameObject.Find("GameRoot").transform;
                exchangeArea.transform.position = Vector3.down * 1000;
            }
        }

        //语法糖：
        //if(obj is Health hp&&hp.nowhp>0)
        //可以写成:if(obj is Health{nowhp: >0})

        //var array = new[] { 10, 20, 30, 40, 50 };
        //Debug.Log(array[^1]); // 输出最后一个元 ? 50
        //Debug.Log(string.Join(", ", array[1..^1])); // 输出: 20, 30, 40

        public static bool Contains(this LayerMask mask, int layout)
            => (mask.value & layout) != 0;


        public static void Destroy(Object obj, float t = 0)
        {
            //if (!obj.IsValid()) return;
            //Debug.Log("销毁了物体" + obj + "延迟" + t + "秒");
            Object.Destroy(obj, t);
        }


        // ⚠ 这里原有 2 个 internal IsValid 扩展（UnityEngine.Object / System.Object），
        //   与 ObjectIsValid.cs 里的公开版本**完全重复**。2026-09-30 把 ObjectIsValid.cs 下沉进本程序集
        //   （00Tools 根＝Assembly-CSharp，asmdef 看不到它 ⇒ 05_UnitCore 编译不过）时删掉，
        //   否则同命名空间下两份可用的扩展会造成 CS0121 二义性。
    }
}
