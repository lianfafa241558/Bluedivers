// 由 AirdropController（01Manager/Battle）解嵌套下沉而来（2026-10-01，P5-3）。
// 下沉理由：AirdropController 属 09_Managers，而玩法层（Mission/Player/AI/Interactable…）到处用这两个类型
// ⇒ 不搬出来玩法层无法成集。**放玩法层而不是契约层**：AirdropData 依赖 `AirdropData_SO`（04_Data），
// 而 04_Data 引用了 01_GameContract ⇒ 契约不能反过来引 04_Data（成环）。
// ⚠ 不要再把它们塞回 AirdropController；`AirdropController.WaitRelease` 仍在原处（管理器侧运行时状态）。

using FPSGame.Core;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Data;

namespace FPSGame.Gameplay
{

    [System.Serializable]
    public class AirdropData {
        public event System.Action<AirdropData, AirdropState> OnStateChange;

        public AirdropData_SO cfg;
        public bool isGift;
        public float time;
        public int count;
        public bool isTmp;

        [InspectorName("冷却时间")]
        public int cool;
        [InspectorName("部署时间")]
        public int arriveTime;
        [InspectorName("部署次数")]
        public int arriveCount;

        public AirdropData(AirdropData_SO cfg,bool isGift)
        {
            this.cfg = cfg;
            this.isGift = isGift;
            count = cfg.arriveCount;
            cool = cfg.cool;
            arriveTime = cfg.arriveTime;
            arriveCount = cfg.arriveCount;
        }

        public AirdropData(AirdropData_SO cfg):this(cfg,true)
        {
            isTmp = true;
            State = AirdropState.Arrive;
        }


        /// <summary>
        /// 允许使用的计数器 0=隐藏和无法使用 只对cfg.Authorize有效
        /// </summary>
        public int authorizeCounter;

        /// <summary>
        /// UI显示的时间进度[0-1]
        /// </summary>
        public float TimeScale
        {
            get
            {
                float re;
                switch (state)
                {
                    case AirdropState.Cool:
                        re= time/Mathf.Max(cool,0.1f);
                        break;
                    case AirdropState.Arrive:
                        re = time / Mathf.Max(arriveTime, 0.1f);
                        break;
                    case AirdropState.Sustain:
                        re = time / Mathf.Max(cfg.sustainTime, 0.1f);
                        break;
                    case AirdropState.Unavailable:
                        return 1;
                    default:
                        return 0;
                }
                return Mathf.Clamp01(re);
            }
        }

        public bool IsAuthorize => !cfg.authorize || authorizeCounter > 0;

        /// <summary>
        /// UI 是否应该显示此战备。
        /// 有授权：始终显示；
        /// 无授权但 unAuthorizeVisible：显示（虚化）；
        /// 无授权且无 unAuthorizeVisible：隐藏。
        /// </summary>
        public bool IsVisible => IsAuthorize || cfg.unAuthorizeVisible;

        /// <summary>
        /// 根据玩家死亡状态判断当前战备是否可用。
        /// deathEnable 战备：dead 时也可用（活着时正常可用）；
        /// 普通战备：dead 时不可用，非 dead 时可用。
        /// </summary>
        public bool IsCurrentlyAvailable(IActor player)
        {
            if (State == AirdropState.Unavailable)
                return false;
            if (!IsAuthorize)
                return false;
            bool isDead = player != null && player.ActorState == ActorState.Dead;
            if (isDead && !cfg.deathEnable)
                return false; // 死亡时，只有 deathEnable 战备可用
            return true;
        }

        /// <summary>
        /// 是否仅因死亡状态而不可用（授权和 State 都 OK，只是死亡且没有 deathEnable）。
        /// 用于 UI 判断：授权不满足时隐藏，死亡不可用时虚化显示。
        /// </summary>
        public bool IsOnlyDeathMismatch(IActor player)
        {
            if (State == AirdropState.Unavailable)
                return false;
            if (!IsAuthorize)
                return false;
            bool isDead = player != null && player.ActorState == ActorState.Dead;
            return isDead && !cfg.deathEnable;
        }

        [SerializeField]
        private AirdropState state;
        public AirdropState State { 
            get => state; 
            set 
            {
                state = value;
                switch (value)
                {

                    case AirdropState.Cool:
                        time = Mathf.Max(cool- cfg.sustainTime - arriveTime,0.5f);//真的吗（woc好像是真的）
                        break;
                    case AirdropState.Arrive:
                        time = arriveTime;
                        break;
                    case AirdropState.Sustain:
                        time = cfg.sustainTime;
                        break;
                    case AirdropState.Ready:

                        break;
                    case AirdropState.Wait:

                        break;
                    case AirdropState.Unavailable:

                        break;
                }
                OnStateChange?.Invoke(this, value);
            }
        }

        /// <summary>
        /// 现在使用逻辑层更新
        /// </summary>
        public void Update()
        {
            if (time >= 0)
            {
                if ((time -= Constants.LoginFrame.RawFloat) < 0)
                {
                    switch (State)
                    {
                        case AirdropState.Cool:
                            State = AirdropState.Ready;
                            break;
                        case AirdropState.Arrive:
                            State = AirdropState.Sustain;
                            break;
                        case AirdropState.Sustain:
                            if (arriveCount>0 &&--count<=0)
                            {
                                State = AirdropState.Unavailable;
                            }
                            else
                            {
                                //Debug.LogError(cfg.name+"正常进CD");
                                State = AirdropState.Cool;
                            }
                            break;
                    }
                }
            }
        }
    }

    public enum AirdropState {
        /// <summary>就绪</summary>
        [InspectorName("就绪")] Ready,
        /// <summary>冷却</summary>
        [InspectorName("冷却")] Cool,
        /// <summary>等待释放</summary>
        [InspectorName("等待释放")] Wait,
        /// <summary>即将抵达</summary>
        [InspectorName("即将抵达")] Arrive,
        /// <summary>正在持续</summary>
        [InspectorName("正在持续")] Sustain,
        /// <summary>不可用</summary>
        [InspectorName("不可用")] Unavailable,
    }

    /// <summary>
    /// 空投"当前待释放"共享状态（2026-10-01 从 <c>AirdropController</c> 的静态字段搬来）。
    ///
    /// <para>▍为什么必须搬：该字段的类型 <see cref="AirdropData"/> 现在属**玩法层**，而读写它的既有两类代码是
    /// 玩法层（<c>PlayerWeaponsManager</c>）与上层（<c>AirdropController</c>/<c>Effect/FlareGun</c>）。
    /// 玩法层若直连 <c>AirdropController</c> 就是上行依赖；而它的值还要被**原样传进**
    /// <c>BattleEventSub.CancelAirdrop(go, data)</c> ⇒ **无法用契约投影**（契约层命名不了玩法层的
    /// <see cref="AirdropData"/>）。⇒ 状态与类型放同一层最省。</para>
    ///
    /// <para>▍<c>AirdropController</c> 里保留了**同名转发属性**，故管理器/表现层的既有调用点零改动。</para>
    /// </summary>
    public static class AirdropReleaseState
    {
        /// <summary>当前等待玩家点选投放点的空投（无则 null）。由 <c>AirdropController</c> 写入，玩法/UI 只读。</summary>
        public static AirdropData WaitRelease;
    }
}
