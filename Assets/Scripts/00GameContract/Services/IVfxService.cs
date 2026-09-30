using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 特效 服务契约（2026-09-30 为拆出 <c>05_UnitCore</c> 而建）。
    ///
    /// <para>▍为什么需要它：<c>Health_AboState</c> 满槽时要挂/收异常状态特效（`VFXManager.Creat/Release`），
    /// 而 `VFXManager` 在 01Manager ⇒ 单位内核反向依赖。</para>
    ///
    /// <para>▍两种形态（P5-0 实测补充）：<c>GameObject</c> 模板（普通特效/弹痕）与
    /// <c>Component</c> 模板（<c>ProjectileBase</c> 这类武器抛射物——下层不能在契约里点名具体玩法类型）。
    /// <c>VFXManager</c> 另有 <c>ProjectileBase</c> 专用重载，由 <c>Component</c> 版本内部转调。</para>
    /// </summary>
    public interface IVfxService
    {
        /// <summary>按模板生成特效实例（返回实例根物体，供稍后 <see cref="Release"/> 回收）。</summary>
        GameObject Creat(GameObject tmp, Vector3 pos = default, Quaternion rotation = default, Transform parent = default);

        /// <summary>回收特效实例。</summary>
        void Release(GameObject go);

        /// <summary>
        /// 按**组件**模板池化生成（目前只支持 <c>ProjectileBase</c>）。
        /// 用泛型让调用方拿回**具体类型**（`Creat(bulletPrefab, pos, rot)` 直接得到 <c>ProjectileBase</c>，无需强转）。
        /// </summary>
        T Creat<T>(T template, Vector3 pos, Quaternion rotation) where T : Component;

        /// <summary>回收"按组件模板生成"的实例（与其配对：<c>ProjectileBase</c> 回抛射物池，其余回普通池）。</summary>
        void Release(Component instance);
    }
}
