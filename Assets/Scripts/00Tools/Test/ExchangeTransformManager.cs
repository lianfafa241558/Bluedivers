using System.Collections;
using Core;
using UnityEngine;

public class ExchangeTransformManager : MonoBehaviour {
        private ObjectPool<ExchangeTransformComp> pool;

        void Start() {
            pool = new(_Add, _Pop, _Push, 5);

        }
        public void Exchange(Transform source) {
            if(enabled&&gameObject.activeInHierarchy)StartCoroutine(_Exchange(source));
        }


        private IEnumerator _Exchange(Transform source) {
            yield return null;
            var comp = pool.Get();
            comp.Revert += Revert;
            //Debug.LogError("尝试交换"+comp+" "+source);
            comp.Exchange(source);
            comp.enabled = true;
        }


        private void Revert(ExchangeTransformComp comp) {
            comp.Revert -= Revert;
            pool.Release(comp);
        }
        private ExchangeTransformComp _Add() {
            var go = new GameObject("ExchangeTransformComp");
            go.transform.SetParent(transform);
            go.SetActive(false);
            var comp = go.AddComponent<ExchangeTransformComp>();
            comp.enabled = false;
            go.SetActive(true);

            return comp;
        }
        private void _Pop(ExchangeTransformComp comp) {

        }
        private void _Push(ExchangeTransformComp comp) {
            comp.enabled = false;
        }


    }
