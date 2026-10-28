using System;
using UnityEngine;

namespace FPSGame.Weapon
{
    /// <summary>
    /// 一把枪的**后坐力性格**：相机冲量/回正节奏 + 武器模型回弹。
    /// <para>▍分工：这里是手感参数（数值策划调）；改装只改
    /// <see cref="WeaponPlayerController.RecoilScale"/> 这个总倍率，把本类整体等比缩放 ⇒ 两边不打架。</para>
    /// <para>▍单位：角度=度，位移=米，回正速度=度/秒。</para>
    /// </summary>
    [Serializable]
    public class WeaponRecoilProfile
    {
        // ---------------- 相机（视角被推） ----------------

        /// <summary>每发抬头量(度)：默认 1.5 ≈ 老版本 `RecoilForce × 1.5` 的手感</summary>
        [InspectorName("相机/每发抬头(度)")]
        [Range(0f, 5f)]
        public float PitchKick = 1.5f;

        /// <summary>每发水平量(度)</summary>
        [InspectorName("相机/每发水平(度)")]
        [Range(0f, 3f)]
        public float YawKick = 0.4f;

        /// <summary>水平锯齿递增：第 n 发幅度 = YawKick × (1 + YawStep × n)（左右交替，模拟连射时枪口越摆越大）</summary>
        [InspectorName("相机/水平递增")]
        [Range(0f, 1f)]
        public float YawStep = 0.12f;

        /// <summary>水平随机比例(0~1)：在锯齿方向之外再叠一个随机乘子，避免每梭子横向轨迹一模一样</summary>
        [InspectorName("相机/水平随机")]
        [Range(0f, 1f)]
        public float YawRandom = 0.35f;

        /// <summary>冲量"吃满"速度：越大越脆（一枪一顿），越小越糊（连发连成一片）</summary>
        [InspectorName("相机/冲量速度")]
        [Range(1f, 120f)]
        public float KickSharpness = 40f;

        /// <summary>停火后多久才开始回正(秒)：太短会让"按住不放"变成无后坐</summary>
        [InspectorName("相机/回正延迟(秒)")]
        [Range(0f, 1f)]
        public float RecoveryDelay = 0.1f;

        /// <summary>回正速度(度/秒)</summary>
        [InspectorName("相机/回正速度(度/秒)")]
        [Range(0f, 60f)]
        public float RecoverySpeed = 12f;

        /// <summary>水平回正速度比例(0=水平不回正，1=与垂直一样快)：留一点"枪口飘"更像 FPS</summary>
        [InspectorName("相机/水平回正比例")]
        [Range(0f, 1f)]
        public float YawRecoveryRatio = 0.7f;
         
        /// <summary>抬头上限(度)：原来是硬编码 12，连射会"饱和"；现在超上限只停止累积，不影响回正</summary>
        [InspectorName("相机/抬头上限(度)")]
        [Range(0f, 45f)]
        public float MaxPitch = 12f;

        /// <summary>水平偏移上限(度)</summary>
        [InspectorName("相机/水平上限(度)")]
        [Range(0f, 30f)]
        public float MaxYaw = 6f;

        /// <summary>瞄准时整体系数（0.7 = 开镜后坐力只有腰射的七成）</summary>
        [InspectorName("相机/瞄准系数")]
        [Range(0f, 1f)]
        public float AdsScale = 0.7f;

        // ---------------- 武器模型（枪被推） ----------------

        /// <summary>每发后推(米)：与 <see cref="PlayerWeaponsManager.MaxRecoilDistance"/> 一起决定最大后退</summary>
        [InspectorName("模型/每发后推(米)")]
        [Range(0f, 0.5f)]
        public float WeaponKickBack = 0.05f;

        /// <summary>每发上跳(度)：枪口抬起，回弹用弹簧</summary>
        [InspectorName("模型/每发上跳(度)")]
        [Range(0f, 15f)]
        public float WeaponKickPitch = 2.5f;

        /// <summary>每发随机侧翻(度)：左/右随机，避免模型只有"直上直下"</summary>
        [InspectorName("模型/每发侧翻(度)")]
        [Range(0f, 10f)]
        public float WeaponKickRoll = 1.2f;

        /// <summary>模型回位刚度：越大越快弹回原位</summary>
        [InspectorName("模型/回位刚度")]
        [Range(1f, 60f)]
        public float WeaponSpring = 14f;

        // ---------------- 连射散布 / 屏震 ----------------

        /// <summary>每"次"射击的散布膨胀(度)：0 = 关闭（等于老行为）。多弹丸/齐射只算一次</summary>
        [InspectorName("散布/每发膨胀(度)")]
        [Range(0f, 5f)]
        public float SpreadBloomPerShot = 0f;

        /// <summary>散布膨胀上限(度)：机枪按住不放最多散到这里</summary>
        [InspectorName("散布/膨胀上限(度)")]
        [Range(0f, 30f)]
        public float SpreadBloomMax = 4f;

        /// <summary>散布回落速度(度/秒)：停火/松手后按这个速度收回去</summary>
        [InspectorName("散布/回落速度(度/秒)")]
        [Range(0f, 60f)]
        public float SpreadBloomDecay = 8f;

        /// <summary>每发屏震(0~1)：**默认 0 = 不震**。撞击感不足时先加这个(0.1~0.3)，
        /// 别去调相机后坐（那个会改弹道，屏震只晃镜头）。⚠ 连发武器会累积 trauma，注意别超过衰减速度</summary>
        [InspectorName("相机/每发屏震(0~1)")]
        [Range(0f, 1f)]
        public float ShakePerShot = 0f;
    }
}
