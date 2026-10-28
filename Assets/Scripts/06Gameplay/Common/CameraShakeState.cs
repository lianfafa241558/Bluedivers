using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 屏震状态（纯 C#，不挂组件）：trauma 累加 + 平方衰减 + Perlin 抖动，输出俯仰/水平/翻滚三个角度偏移。
    /// <para>▍为什么是"状态"而不是组件：相机姿态每帧被玩家控制器**按绝对值**写入，
    /// 组件在 LateUpdate 里直接改 transform 会和它抢所有权（谁后执行谁生效，还容易留下累积残留）。
    /// 所以屏震只产出**偏移量**，由 <see cref="BaseSelfController"/> 统一叠加到相机输出。</para>
    /// <para>▍复用：持有点是 <see cref="BaseSelfController"/> ⇒ 玩家/载具/炮台自动都有；
    /// 开火用 <see cref="BaseSelfController.AddCameraShake"/>，以世界点为源的（冲击波/爆炸）用
    /// <see cref="BaseSelfController.BroadcastCameraShake"/>。</para>
    /// </summary>
    public sealed class CameraShakeState
    {
        /// <summary>trauma(0~1)：多次震会叠加，输出用平方 ⇒ 起手猛、尾巴干净</summary>
        private float _trauma;

        /// <summary>噪声时间轴</summary>
        private float _time;

        /// <summary>每个实例的噪声种子：否则所有相机抖得一模一样</summary>
        private readonly float _seed;

        /// <summary>俯仰偏移(度)</summary>
        public float Pitch { get; private set; }

        /// <summary>水平偏移(度)</summary>
        public float Yaw { get; private set; }

        /// <summary>翻滚偏移(度)：屏幕"歪一下"，是屏震最有辨识度的部分</summary>
        public float Roll { get; private set; }

        /// <summary>三个方向的最大偏移(度)</summary>
        public float MaxPitch = 2.5f, MaxYaw = 2.5f, MaxRoll = 2f;

        /// <summary>衰减速度(trauma/秒)：越大震得越短</summary>
        public float Decay = 1.8f;

        /// <summary>实例计数：只用来给每台相机的噪声错开相位</summary>
        private static int s_seedCounter;

        public CameraShakeState()
        {
            // ⚠ 构造函数里**不能**调 Random.Range：本类由 MonoBehaviour 的字段初始化器 new 出来，
            // 那时还在构造期，Unity 会抛"不允许从构造函数调用 Range"（VehicleController 实测）。
            // 用纯算数种子即可 —— 只要各实例不同，噪声不同步就够了。
            _seed = (s_seedCounter++ * 0.6180339f % 1f) * 100f;
        }

        /// <summary>叠加一次震动强度(0~1)</summary>
        public void Add(float strength01)
        {
            if (strength01 <= 0f) return;
            _trauma = Mathf.Clamp01(_trauma + strength01);
        }

        /// <summary>立刻停止（切场景 / 重生）</summary>
        public void Reset()
        {
            _trauma = 0f;
            _time = 0f;
            Pitch = Yaw = Roll = 0f;
        }

        public void Tick(float dt)
        {
            if (_trauma <= 0f)
            {
                Pitch = Yaw = Roll = 0f;
                return;
            }

            _trauma = Mathf.Max(0f, _trauma - Decay * dt);
            _time += dt;

            float s = _trauma * _trauma;
            Pitch = Noise(0.11f) * MaxPitch * s;
            Yaw = Noise(0.53f) * MaxYaw * s;
            Roll = Noise(0.87f) * MaxRoll * s;
        }

        /// <summary>Perlin(-1~1)：三个轴用不同 offset，避免同步成一条斜线</summary>
        private float Noise(float offset) => Mathf.PerlinNoise(_seed + _time * 22f, offset) * 2f - 1f;
    }
}
