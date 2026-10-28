using UnityEngine;

namespace FPSGame.Weapon
{
    /// <summary>
    /// 相机后坐的状态机（纯 C#，不挂组件）：**偏移量与视角角分开存**。
    /// <para>▍为什么要分开：回正只把这里的偏移拉回 0，玩家压枪改的是 `m_CameraVerticalAngle`
    /// ⇒ 两者叠加。老实现把回正直接加进视角角，玩家往下压时回正又往上顶，压枪永远不跟手。</para>
    /// <para>▍模型：每发把"目标偏移"推出去一段，当前偏移用帧率无关的指数去追；
    /// 停火满 <see cref="WeaponRecoilProfile.RecoveryDelay"/> 后目标开始线性归零。</para>
    /// </summary>
    public sealed class WeaponRecoilState
    {
        /// <summary>连发计数：决定水平锯齿方向与递增幅度</summary>
        private int _shot;

        /// <summary>当前偏移(度)；负 pitch = 抬头</summary>
        private float _pitch, _yaw;

        /// <summary>目标偏移(度)：每发推它，回正拉它回 0</summary>
        private float _pitchTarget, _yawTarget;

        /// <summary>距上一发的空闲时间(秒)：到 RecoveryDelay 才开始回正</summary>
        private float _idle;

        /// <summary>当前俯仰偏移(度，负 = 抬头)</summary>
        public float Pitch => _pitch;

        /// <summary>当前水平偏移(度)</summary>
        public float Yaw => _yaw;

        /// <summary>切枪/重生/上车时清干净，避免带着上一把枪的偏移进新武器</summary>
        public void Reset()
        {
            _shot = 0;
            _pitch = _yaw = _pitchTarget = _yawTarget = _idle = 0f;
        }

        /// <summary>开一发：把目标偏移推出去一段（水平按"左右交替 + 幅度递增 + 随机"）</summary>
        /// <param name="strength">总强度倍率，一般传 <see cref="WeaponPlayerController.RecoilForceFinal"/></param>
        /// <param name="aiming">是否处于瞄准状态（吃 profile 的 AdsScale）</param>
        public void AddShot(WeaponRecoilProfile p, float strength, bool aiming)
        {
            if (p == null || strength <= 0f) return;

            float s = strength * (aiming ? p.AdsScale : 1f);
            _shot++;
            _idle = 0f;

            _pitchTarget -= p.PitchKick * s;

            // 水平：奇偶左右交替，每两发幅度长大一档，再乘一个随机量
            float dir = (_shot & 1) == 1 ? 1f : -1f;
            float grow = 1f + p.YawStep * (_shot >> 1);
            float jitter = 1f + (Random.value * 2f - 1f) * p.YawRandom;
            _yawTarget += p.YawKick * s * dir * grow * jitter;

            // 软上限：只夹目标，不夹当前值 ⇒ 超上限时"停止累积"而不是突然跳变
            _pitchTarget = Mathf.Clamp(_pitchTarget, -p.MaxPitch, p.MaxPitch);
            _yawTarget = Mathf.Clamp(_yawTarget, -p.MaxYaw, p.MaxYaw);
        }

        /// <summary>每帧推进：当前值追目标，停火到点后目标归零</summary>
        public void Tick(float dt, WeaponRecoilProfile p)
        {
            if (p == null) return;
            if (dt <= 0f) return;

            _idle += dt;

            // 帧率无关：1-exp(-k·dt)，别用 Lerp(a,b,k·dt)（大 dt 时会抖）
            float k = 1f - Mathf.Exp(-p.KickSharpness * dt);
            _pitch = Mathf.Lerp(_pitch, _pitchTarget, k);
            _yaw = Mathf.Lerp(_yaw, _yawTarget, k);

            if (_idle < p.RecoveryDelay) return;

            _pitchTarget = Mathf.MoveTowards(_pitchTarget, 0f, p.RecoverySpeed * dt);
            _yawTarget = Mathf.MoveTowards(_yawTarget, 0f, p.RecoverySpeed * p.YawRecoveryRatio * dt);
        }
    }
}
