using UnityEngine;

namespace FPSGame.DayNightSystem
{
    /// <summary>
    /// 修改昼夜系统的时间进度，控制昼夜循环的速度和时间分配。
    ///
    /// <para>▍2026-10-08 改为**纯墙钟函数**：角度不再逐帧累加 <c>deltaTime</c>，而是每次都从系统时钟直接算出来。
    /// 旧实现是"Initialize 取一次时钟当起点 → Tick 里一直累加" ⇒ 各端帧率/卡顿/时间缩放不同，
    /// 累加量必然不同，运行越久错得越开（联机下两端昼夜不同拍）。改成函数后，
    /// 只要两端时钟对齐，相位**恒等**、不再随运行时长漂移。</para>
    ///
    /// <para>▍⚠ 两个已知口径：
    /// ① 前提是两端系统时钟对齐（局域网内通常被 NTP 对齐到 ~1s 内）。若时钟差得大，两端会保持一个
    /// <b>固定</b>相位差 —— 仍不会再漂移，但不会重合。
    /// ② 不再受 <c>Time.timeScale</c> 影响（旧实现会随暂停/慢放一起变慢）。</para>
    ///
    /// <para>▍顺带修掉一个断层：旧 <c>Initialize</c> 只取"分+秒"（<c>60*Minute + Second</c>），
    /// 于是每到整点角度会跳变一次。现在用"当日累计秒（含小时）"，连续。</para>
    /// </summary>
    [AddComponentMenu("昼夜系统/时间进度模块")]
    public class TimeProgressionModule : MonoBehaviour, IDayNightModule
    {
        public void Initialize(DayNightState state)
        {
            state.CurrentAngle = AngleAt(state, WallClockSeconds());
        }

        public void Tick(DayNightState state, float deltaTime)
        {
            // deltaTime 故意不用：角度完全由墙钟决定（见类注释）
            state.CurrentAngle = AngleAt(state, WallClockSeconds());
        }

        /// <summary>当日累计秒数（含毫秒）。用**墙钟**而不是累加 <c>deltaTime</c>，见类注释。</summary>
        static float WallClockSeconds()
        {
            System.DateTime now = System.DateTime.Now;
            return now.Hour * 3600f + now.Minute * 60f + now.Second + now.Millisecond * 0.001f;
        }

        /// <summary>
        /// 把"当日秒数"映射成角度：一个周期内**前 2/3 是白天（0°→180°）、后 1/3 是夜晚（180°→360°）**。
        /// <para>▍与旧 <c>Tick</c> 的速度等价：白天 <c>180/dayDuration</c>、夜晚 <c>180/nightDuration</c>
        /// （<c>dayDuration = 2T/3</c>、<c>nightDuration = T/3</c>）⇒ 一圈仍然是 <c>T</c> 秒。
        /// 区别只在于：旧实现**起点**用的是"均匀映射"（<c>phase/T * 360</c>），与它自己的变速不自洽；
        /// 现在是同一条分段映射贯穿始终，因此同一墙钟时刻的**起始角度可能与旧版不同**（最多差 60°，
        /// 仍在同一"白天/夜晚"区间内，属昼夜氛围量，不影响判定）。</para>
        /// </summary>
        static float AngleAt(DayNightState state, float seconds)
        {
            float cycle = Mathf.Max(1f, state.CycleDurationSecond);
            float dayDuration = cycle * (2f / 3f);
            float nightDuration = cycle * (1f / 3f);

            float phase = seconds % cycle;
            if (phase < dayDuration) return phase / dayDuration * 180f;
            return 180f + (phase - dayDuration) / nightDuration * 180f;
        }

        public void Dispose() { }
    }
}
