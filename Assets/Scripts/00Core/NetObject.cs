using UnityEngine;

namespace FPSGame.Core
{
    /// <summary>
    /// 「可被网络同步创建」的预制体标记：编号即 <c>SpawnableTable_SO</c> 的主键（房主只发 int，成员靠表还原 prefab）。
    ///
    /// <para>▍为什么不复用 <see cref="BaseObject.Id"/>（2026-11-10 实测）：它既会重复（`Exosuit` 同时是
    /// `Turret_Manual_AT` 与 `Exosuit` 两把完全不同的预制体）又大量为空，且一个预制体里父子可能有多个
    /// BaseObject ⇒ 它当不了表主键。</para>
    ///
    /// <para>⚠ 只挂在预制体**根**上（扫描工具只认根组件）；编号一旦随协议发出就不能再改值 —— 改了等于改协议，
    /// 旧端会按老编号造出别的东西。</para>
    /// </summary>
    [AddComponentMenu("单位/网络对象编号")]
    public class NetObject : MonoBehaviour
    {
        [InspectorName("网络编号")]
        [SerializeField] private int _id;

        /// <summary>跨端一致的预制体编号（0 = 未配置，扫描时跳过）。</summary>
        public int Id => _id;
    }
}
