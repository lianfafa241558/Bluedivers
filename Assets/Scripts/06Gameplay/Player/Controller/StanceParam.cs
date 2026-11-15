using System;
using FPSGame.Attributes;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>一种姿态的全部可调参数。
    /// <para>▍为什么并成结构体 + 列表：一个姿态要同时改**胶囊高度、镜头高度/前移、武器挂点、移速、散布、后坐、
    /// 俯仰限位、被侦测距离**八九个值。原来是 <c>_crouchXxx</c> / <c>_proneXxx</c> 成对字段 ⇒ 加个姿态就得复制一整排字段，
    /// 还容易漏掉某一个（漏了就变成"蹲着还能全速跑"这类半截状态）。现在"一种姿态 = 表里的一条"。</para>
    /// <para>▍⚠ 0 的含义各不相同，见各字段注释：高度类 0 = 沿用站立基线，倍率类 0 = 按 1 处理，限位 0 = 不限。</para>
    /// </summary>
    [Serializable]
    public struct StanceParam
    {
        [InspectorName("姿态")]
        public PlayerStance Stance;

        [InspectorName("胶囊高度")]
        [Tooltip("<=0 用站立基线。⚠ 运行时还会夹到 radius*2 以上，否则 Unity 会把胶囊压成球")]
        public float CapsuleHeight;

        [InspectorName("镜头高度")]
        [Tooltip("第一人称相机离地高度（米）；<=0 用站立基线")]
        public float CameraHeight;

        [InspectorName("镜头前移")]
        [Tooltip("相机沿玩家本地 +Z 额外前移多少米：趴伏时头在身体前方，只降高度相机会埋进脖子/胸口")]
        public float CameraForward;

        [InspectorName("武器挂点下沉")]
        [Tooltip("FirstPersonSocket 往下挪多少米（它挂在玩家根节点、不吃相机高度，必须单独挪）")]
        public float SocketDrop;

        [InspectorName("移速倍率")]
        [Tooltip("<=0 按 1 处理")]
        public float SpeedScale;

        [InspectorName("散布倍率")]
        public float SpreadScale;

        [InspectorName("后坐倍率")]
        public float RecoilScale;

        [InspectorName("俯仰限位")]
        [Tooltip("仰角/俯角都卡在 ±该值（度）；<=0 用默认值（第一人称 89/70、第三人称取表里的上仰/下俯角）。" +
                 "第三人称只收紧不放宽：与「最大上仰角/最大下俯角」取更严的一个")]
        public float PitchLimit;

        [InspectorName("被侦测距离倍率")]
        [Tooltip("敌人视觉发现本单位的距离倍率（1 = 站直）；越小越难被发现 ⇒ 趴下 < 蹲下 < 站立")]
        public float DetectRangeScale;

        /// <summary>某姿态的默认参数：表里漏配该姿态时兜底；远端盟友没有这张表，也直接用它换算"被侦测距离"。</summary>
        public static StanceParam Default(PlayerStance stance)
        {
            switch (stance)
            {
                case PlayerStance.Crouch:
                    return new StanceParam
                    {
                        Stance = PlayerStance.Crouch,
                        CapsuleHeight = 1.2f,
                        CameraHeight = 0.82f,
                        SocketDrop = 0.60f,
                        SpeedScale = 0.5f,
                        SpreadScale = 0.7f,
                        RecoilScale = 0.75f,
                        DetectRangeScale = 0.7f,
                    };
                case PlayerStance.Prone:
                    return new StanceParam
                    {
                        Stance = PlayerStance.Prone,
                        CapsuleHeight = 0.8f,
                        CameraHeight = 0.40f,
                        CameraForward = 0.28f,
                        SocketDrop = 1.00f,
                        SpeedScale = 0.2f,
                        SpreadScale = 0.5f,
                        RecoilScale = 0.55f,
                        PitchLimit = 25f,
                        DetectRangeScale = 0.4f,
                    };
                default:
                    return new StanceParam
                    {
                        Stance = PlayerStance.Stand,
                        SpeedScale = 1f,
                        SpreadScale = 1f,
                        RecoilScale = 1f,
                        DetectRangeScale = 1f,
                    };
            }
        }
    }
}
