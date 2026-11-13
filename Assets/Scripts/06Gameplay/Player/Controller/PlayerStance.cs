using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>玩家姿态。
    /// <para>▍数值**显式定死**：联机位姿快照按 <c>byte</c> 传这个枚举的整数值（见 <c>PoseSnapshot.Stance</c>），
    /// 插入新值或调换顺序会让新旧两端对不上 ⇒ 只能往后追加。</para></summary>
    public enum PlayerStance : byte
    {
        [InspectorName("站立")] Stand = 0,
        [InspectorName("下蹲")] Crouch = 1,
        [InspectorName("卧倒")] Prone = 2,
    }
}
