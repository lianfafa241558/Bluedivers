using UnityEngine;

/// <summary>
/// 可被"提交点"（如凯伊/Kei）交出的手持物。
/// <para>提交点只按 Id 白名单认物件、不认具体类型；命中后调用 <see cref="NotifySubmitTo"/> 通报实例，
/// 随后卸载并销毁该物件。订阅方（如任务）据此计入自己的进度。</para>
/// </summary>
public interface ISubmittableHandItem
{
    /// <summary>被提交点收走时调用（调用之后提交点会卸载并销毁本物件）。</summary>
    /// <param name="user">提交者</param>
    void NotifySubmitTo(GameObject user);
}
