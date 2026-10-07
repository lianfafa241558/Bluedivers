using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 【网络位姿 → 表现】把"别人（远程实体）的位置"平滑地搬到本物体的 <c>transform</c> 上。
    ///
    /// <para>▍用法：网络侧（09 的桥）收到快照后调 <see cref="ApplyPose"/>；本组件在 <c>Update</c> 里按
    /// "渲染时刻 = 现在 - <see cref="RenderDelay"/>"在缓冲里取两点插值并写 <c>transform</c>
    /// ⇒ 任何远程实体（盟友 / 将来的远程敌人、道具、掉落物）都复用同一个组件，
    /// 各自在 Inspector 上配自己的延迟与频率即可。</para>
    ///
    /// <para>▍⚠ 为什么必须"延迟 + 多点缓冲"，而不是"收到就写 transform"：房主下行约 20Hz（包间隔 ~50ms），
    /// 若只留"上一包 / 最新包"两个点，渲染时刻会**永远落在最新一段的左端之前** ⇒ 插值参数被夹到 0，
    /// 表现退化成"每个包跳一下"（实测就是这样：盟友不是平滑移动，而是快速地闪现位移）。
    /// 数值对照：两点缓冲时每帧最大位移 = 一整包的距离（0.533m），多点缓冲 = 8/60 = 0.133m（＝理论连续值）。
    /// 详见 <see cref="UpdateRenderPose"/>。</para>
    ///
    /// <para>▍⚠ 本组件写的 <c>transform</c> **就是碰撞/查询位置**（<c>Actor.LogicPos</c> 直接取 <c>transform.position</c>，
    /// <c>Actor.Update</c> 再把它同步进 <c>Range</c> 并派发 <c>UnitEventSub.UnitPosChange</c>）⇒ 这里只在
    /// "已知两个真实位置之间"插值（渲染的一定是主人**真实走过**的位置），**绝不做外推**（dead reckoning）：
    /// 外推会凭空造出主人从未占据过的位置，把碰撞体推进墙里/空中。</para>
    /// </summary>
    [DefaultExecutionOrder(-50)]   // 先于消费 transform 的组件（如 FriendController 用位移算动画速度）
    [AddComponentMenu("网络/位姿插值(NetTransformView)")]
    public class NetTransformView : MonoBehaviour
    {
        [InspectorName("渲染延迟(秒)")]
        [Tooltip("渲染的时刻往后延迟这么多，用缓冲里的两个快照插值；约 2 个快照间隔（下行 20Hz ⇒ 0.10s）")]
        public float RenderDelay = 0.10f;

        [InspectorName("快照缓冲长度")]
        [Tooltip("缓冲要能覆盖住 RenderDelay：点数 ≥ RenderDelay/快照间隔 + 2（20Hz + 0.10s ⇒ 至少 4）")]
        public int BufferSize = 6;

        [InspectorName("吸附阈值(米)")]
        [Tooltip("与**上一个快照**的距离超过它就直接吸附（首次出现 / 真传送 / 久丢包后跳变），避免'滑行穿墙'")]
        public float SnapDistance = 3f;

        [InspectorName("朝向插值速度")]
        [Tooltip("朝向单独用 Slerp 平滑（位置插值自带延迟，朝向若直接取插值会显得'转头迟'）")]
        public float YawLerpSpeed = 12f;

        // ---- 快照缓冲：保留最近若干个"到达时刻 + 位姿"，渲染时按"现在 - RenderDelay"在两点之间插值 ----
        // ⚠ 只留"上一包 / 最新包"两个点是**不够的**：包间隔只有 ~50ms（房主下行 20Hz），远小于 RenderDelay(100ms)
        //   ⇒ 渲染时刻永远落在最新一段的**左端之前**，插值参数被夹到 0，表现退化成"每个包跳一下"。
        //   必须攒够历史点，让渲染时刻落在某一段 [t0,t1] 内部，插值才真正生效。
        private Vector3[] _bufPos;
        private float[] _bufYaw;
        private float[] _bufPitch;
        private float[] _bufTime;
        private int _bufCount;

        /// <summary>本次吸附标记（消费型，见 <see cref="ConsumeSnap"/>）。</summary>
        private bool _snapped;

        /// <summary>缓冲里有没有位姿（false = 还没收到过任何快照 ⇒ 本组件不动 <c>transform</c>）。</summary>
        public bool HasPose => _bufCount > 0;

        /// <summary>
        /// 本帧插值出来的**俯仰角**（度）—— 消费方（<c>FriendController</c>）把它应用到**武器挂点**上。
        ///
        /// <para>▍为什么不写进 <c>transform.rotation</c>：那是 yaw-only 的身体朝向；玩家的身体同样只吃 yaw，
        /// 俯仰只作用在武器（<c>FirstPersonSocket</c>）上 ⇒ 写进模型旋转会变成"整个人前倾后仰"。</para>
        /// </summary>
        public float RenderedPitch { get; private set; }

        /// <summary>
        /// 【网络 → 表现】追加一条位姿快照。
        /// <para>⚠ 本方法只往缓冲里追加；真正写 <c>transform</c> 在 <see cref="UpdateRenderPose"/> 里按"渲染时刻"插值。</para>
        /// </summary>
        /// <param name="teleport">true = 不插值，直接吸附</param>
        /// <param name="pitch">上身/武器的俯仰角（度，见 <see cref="RenderedPitch"/>）；0 = 不俯仰</param>
        public void ApplyPose(Vector3 pos, float yaw, float pitch, bool teleport)
        {
            EnsureBuffer();

            // ⚠ 吸附判定要和"上一个快照"比，**不能和当前 transform 比**：渲染位置本来就有 RenderDelay 的滞后，
            //   跑动时两者天然差 1~3 米，拿当前位置比会把正常跑动误判成传送 ⇒ 频繁清零缓冲、表现成闪现。
            bool snap = teleport || _bufCount == 0 ||
                        Vector3.Distance(_bufPos[_bufCount - 1], pos) > SnapDistance;
            if (snap)
            {
                _bufCount = 0;
                Push(pos, yaw, pitch, Time.time);
                transform.position = pos;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                RenderedPitch = pitch;    // 吸附时俯仰也直接对齐，别慢慢转过去
                _snapped = true;          // 消费方据此丢掉"跳变产生的假速度"（动画别瞬间飙到最快）
                return;
            }

            Push(pos, yaw, pitch, Time.time);
        }

        /// <summary>兼容旧调用（不带俯仰 ⇒ 按 0 处理）。</summary>
        public void ApplyPose(Vector3 pos, float yaw, bool teleport) => ApplyPose(pos, yaw, 0f, teleport);

        /// <summary>清空缓冲与吸附标记（对象复用时调用，避免残留上一个主人的位姿）。</summary>
        public void ResetPose()
        {
            _bufCount = 0;
            _snapped = false;
        }

        /// <summary>
        /// 本帧是否发生过<b>吸附</b>（读一次即清）。消费方（如 <c>FriendController</c>）据此丢弃跳变帧的假速度。
        /// </summary>
        public bool ConsumeSnap()
        {
            bool v = _snapped;
            _snapped = false;
            return v;
        }

        /// <summary>把一条快照追加进缓冲（满了就整体前移一格，容量很小，直接搬）。</summary>
        private void Push(Vector3 pos, float yaw, float pitch, float time)
        {
            if (_bufCount >= _bufPos.Length) ShiftOne();
            _bufPos[_bufCount] = pos;
            _bufYaw[_bufCount] = yaw;
            _bufPitch[_bufCount] = pitch;
            _bufTime[_bufCount] = time;
            _bufCount++;
        }

        /// <summary>丢掉缓冲里最老的一个点（调用方保证 <c>_bufCount >= 2</c>）。</summary>
        private void ShiftOne()
        {
            System.Array.Copy(_bufPos, 1, _bufPos, 0, _bufCount - 1);
            System.Array.Copy(_bufYaw, 1, _bufYaw, 0, _bufCount - 1);
            System.Array.Copy(_bufPitch, 1, _bufPitch, 0, _bufCount - 1);
            System.Array.Copy(_bufTime, 1, _bufTime, 0, _bufCount - 1);
            _bufCount--;
        }

        /// <summary>惰性建缓冲：不依赖 <c>Awake</c> 与调用方的先后顺序（快照可能在 Awake 之前就到）。</summary>
        private void EnsureBuffer()
        {
            if (_bufPos != null && _bufPos.Length >= 4) return;

            // 至少 4 个点：按 20Hz 下行算 = 0.15s 历史，够覆盖 0.10s 的 RenderDelay（3 个点只有 0.10s，丢一包就断粮）
            int cap = Mathf.Max(4, BufferSize);
            _bufPos = new Vector3[cap];
            _bufYaw = new float[cap];
            _bufPitch = new float[cap];
            _bufTime = new float[cap];
            _bufCount = 0;
        }

        private void Awake()
        {
            EnsureBuffer();
        }

        private void Update()
        {
            UpdateRenderPose();
        }

        /// <summary>
        /// 按"渲染时刻 = 现在 - <see cref="RenderDelay"/>"在缓冲里插值。
        /// <para>缓冲保证渲染时刻落在某一段 <c>[t0, t1]</c> 内部 ⇒ 位置连续；数据断流时退化为"停在最后已知位置"。</para>
        /// </summary>
        private void UpdateRenderPose()
        {
            if (_bufCount == 0) return;

            float renderTime = Time.time - RenderDelay;

            // 丢掉"整段都在渲染时刻之前"的老点，只留一个左端点
            while (_bufCount >= 2 && _bufTime[1] <= renderTime) ShiftOne();

            Vector3 pos;
            float yaw;
            float pitch;
            if (_bufCount < 2)
            {
                // 只有一个点（刚吸附 / 刚出现），或断流把点耗完了 ⇒ 停在最后已知位姿
                pos = _bufPos[0];
                yaw = _bufYaw[0];
                pitch = _bufPitch[0];
            }
            else
            {
                float span = _bufTime[1] - _bufTime[0];
                float t = span <= 0.0001f ? 1f : Mathf.Clamp01((renderTime - _bufTime[0]) / span);
                pos = Vector3.Lerp(_bufPos[0], _bufPos[1], t);
                yaw = Mathf.LerpAngle(_bufYaw[0], _bufYaw[1], t);
                pitch = Mathf.LerpAngle(_bufPitch[0], _bufPitch[1], t);
            }

            transform.position = pos;

            // 朝向单独用 Slerp 平滑：位置插值自带延迟，朝向若直接取插值会显得"转头迟"
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.Euler(0f, yaw, 0f), Time.deltaTime * YawLerpSpeed);

            // 俯仰同样平滑（消费方每帧读 RenderedPitch 应用到武器挂点；本组件不写进身体旋转）
            RenderedPitch = Mathf.LerpAngle(RenderedPitch, pitch, Time.deltaTime * YawLerpSpeed);
        }
    }
}
