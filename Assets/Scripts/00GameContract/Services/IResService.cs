using System;
using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 资源加载 服务契约（由 <c>ResSvc</c> 实现并注册，2026-09-30 为 P5 加入）。
    ///
    /// <para>▍只开玩法层/Effect 真正用到的 4 个入口（实测）：<c>LoadSprite</c>(1)、<c>CreatPrefab</c>(4)、
    /// <c>AsyncLoadScene</c>/<c>AsyncContinueLoadScene</c>(2)。<c>ResSvc</c> 其余成员（<c>CreatPrefabUI</c>/
    /// <c>AsyncLoadSceneProgress</c>/各类 <c>LoadObject</c>）暂不开，等有下层调用方再加。</para>
    /// </summary>
    public interface IResService
    {
        /// <summary>按路径实例化预制体（<paramref name="cache"/> = 走缓存池）。</summary>
        GameObject CreatPrefab(string path, bool cache = false, Vector3 pos = default(Vector3));

        /// <summary>按路径加载 Sprite（内部会自动加 <c>Images/</c> 前缀）。</summary>
        Sprite LoadSprite(string path, bool cache = false);

        /// <summary>异步切场景。</summary>
        void AsyncLoadScene(string mapName, Action loaded, bool showLoadWnd = false, bool waitExtra = false, bool allowSkip = false);

        /// <summary>继续（放行）被暂停的异步切场景流程。</summary>
        void AsyncContinueLoadScene();
    }
}
