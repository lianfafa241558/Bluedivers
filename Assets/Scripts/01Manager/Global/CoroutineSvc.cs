using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 静态协程服务，提供全局的协程调用接口
/// </summary>
public class CoroutineSvc
{
    private static MonoBehaviour _coroutineRunner;

    // 静态启动协程
    public static void StartCoroutine(IEnumerator coroutine)
    {
        if (_coroutineRunner == null)
        {
            // 自动初始化
            var go = new GameObject("CoroutineManager");
            _coroutineRunner = go.AddComponent<CoroutineRunner>();
            GameObject.DontDestroyOnLoad(go);
        }
        _coroutineRunner.StartCoroutine(coroutine);
    }

    private class CoroutineRunner : MonoBehaviour { }

}
