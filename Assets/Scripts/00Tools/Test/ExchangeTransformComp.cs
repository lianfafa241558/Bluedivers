using UnityEngine;
using UnityEngine.Events;

namespace Unity.BaseTool {
    /// <summary>
    /// 用来把换到置换区的物体换回来用的(具有这个组件的预制体必须一开始是隐藏的)
    /// </summary>
    public class ExchangeTransformComp : MonoBehaviour {
        public UnityAction<ExchangeTransformComp> Revert;
        private Transform target;
        /// <summary>
        /// 他触发启动的时候说明该换回来了
        /// </summary>
        private void OnEnable() {
            Exchange(target);
            Revert?.Invoke(this);
        }

        /// <summary>
        /// 完成两个物体位置的交换(把目标物体换到置换区)
        /// </summary>
        public void Exchange(Transform go) {
            var exchangeArea = transform.parent;
            var pos = transform.position;
            var rotation = transform.rotation;
            var scale = transform.localScale;
            target = go;
            transform.SetParent(go.parent);
            transform.position = go.position;
            transform.rotation = go.rotation;
            transform.localScale = go.localScale;
            go.SetParent(exchangeArea);
            go.position = pos;
            go.rotation = rotation;
            go.localScale = scale;
        }



    }
}