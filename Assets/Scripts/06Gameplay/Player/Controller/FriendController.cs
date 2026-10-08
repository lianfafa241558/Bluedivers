using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Game;      // Actor / ActorState（单位内核，05_UnitCore）
using FPSGame.Weapon;    // WeaponPlayerController（武器槽位装配用）
using PEMaths;           // PEInt（写镜像血量/护盾）
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 远程玩家（盟友）控制器：**不吃输入、不建相机**，管"盟友身份 + 身体模型 + 移动动画"。
    ///
    /// <para>▍职责边界：**位姿插值不在本类**，已抽到同物体上的 <see cref="NetTransformView"/>
    /// （通用能力：将来的远程敌人 / 道具 / 掉落物共用同一套"延迟 + 多点缓冲"）。
    /// 本类保留：Sid/名字、模型加载后挂载（加载动作归 09 的桥）、用"位移速度"驱动移动动画。</para>
    ///
    /// <para>▍为什么必须有它：本地玩家走 <c>PlayerController</c>（<c>BaseSelfController.Update</c> 会读
    /// <c>PlayerInputHandler</c> 转视角、并驱动相机），而远程玩家的控制源是网络 ⇒ 两者只有"外观"相同。</para>
    ///
    /// <para>▍⚠ 载体（预制体）的三条硬要求 —— 都是实测出来的，别在运行时改：</para>
    /// <list type="number">
    ///   <item><c>Actor.type</c> 必须**在预制体上**就是 <c>UnitTypeEnum.Friend</c>。理由：<c>Actor.Type</c> 只读；
    ///         且 <c>Actor.WaitSetPos</c> 在 <c>Awake</c> 里**同步**派发 <c>UnitEventSub.PlayerCreate</c>
    ///         （舰桥阶段 <c>IsMainStage()</c> 恒 true，协程第一段就跑完，不等任何 yield）
    ///         ⇒ 运行时再改类型已经太晚：远程玩家会被当成"本地玩家"注册（抢 <c>ActorsManager.Player</c>、上小地图玩家点）。</item>
    ///   <item>不能带 <c>PlayerController</c> / <c>PlayerInputHandler</c> / <c>PlayerOperationController</c>
    ///         —— 否则会抢输入、抢相机、抢 <c>CharacterController</c>。</item>
    ///   <item>不能带 Camera / AudioListener —— 玩家预制体里有 3 个相机（含 <c>MainCamera</c> tag）与 2 个
    ///         AudioListener，多实例会互相打架。</item>
    /// </list>
    /// </summary>
    [AddComponentMenu("玩家/盟友控制器")]
    [RequireComponent(typeof(NetTransformView))]
    [RequireComponent(typeof(FriendWeaponView))]
    public class FriendController : MonoBehaviour
    {
        [SerializeField]
        [InspectorName("身体挂点")]
        [Tooltip("运行时会在这里挂 StudentModle/<角色id>；留空则直接挂在自己身上")]
        private Transform modelRoot;

        /// <summary>网络身份（= 该玩家在房主那边的会话 sid）。</summary>
        public uint NetSid { get; private set; }

        /// <summary>显示名（资料里带来的玩家名；调试 / 抬头显示用）。</summary>
        public string PlayerName { get; private set; }

        /// <summary>当前挂着的角色 id（= 上一次 <see cref="AttachModel"/> 传入的值；空 = 还没挂过）。
        /// <para>供 09 侧的桥在"名单同步"里判断是不是同一个角色，避免每次都重新加载 + 实例化模型。</para></summary>
        public string RoleId => _roleId;

        private Transform _model;
        private string _roleId;

        /// <summary>同物体上的位姿插值组件（由 <see cref="RequireComponent"/> 保证存在；见其类注释）。</summary>
        private NetTransformView _view;

        /// <summary>同物体上的武器槽位组件（表现层；由 <see cref="RequireComponent"/> 保证存在）。</summary>
        private FriendWeaponView _weapons;

        private Animator _anim;
        private Actor _actor;
        private bool _lastDead;
        private Vector3 _lastPos;

        // 动画参数：与本地玩家**同一套**（玩家在 PlayerController 里用的是硬编码 "Speed"/"IsMove"/"IsDeath"）。
        // ⚠ 不能用 Constants.k_AnimMoveSpeedParameter（= "MoveSpeed"）：学生模型控制器
        //   （Noa/Aris/Nagisa.controller）里只有 Speed / IsMove / IsReady / IsDeath
        //   ⇒ 参数名不匹配时动画**完全不播**，看起来就是"盟友纯平移"。
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimIsMove = Animator.StringToHash("IsMove");
        private static readonly int AnimIsDeath = Animator.StringToHash("IsDeath");

        /// <summary>【桥在创建时调用一次】写入身份。</summary>
        public void SetIdentity(uint sid, string playerName)
        {
            NetSid = sid;
            PlayerName = playerName;
        }

        /// <summary>
        /// 【桥调用】挂上该玩家的身体模型（<c>Prefabs/StudentModle/&lt;角色id&gt;</c> 的实例）。
        /// <para>▍为什么要由桥来加载：本类在 <c>06_Gameplay</c>，而 <c>ResSvc</c> 在 <c>09_Managers</c>
        /// —— 玩法层看不见它（跨层方向），所以"加载"归桥、"挂载"归这里。</para>
        /// <para>▍同角色重复调用会直接忽略（名单每次同步都会走一遍）。</para>
        /// </summary>
        public void AttachModel(Transform model, string roleId)
        {
            if (model == null) return;

            if (_roleId == roleId && _model != null)
            {
                Destroy(model.gameObject);   // 已经是最新角色：把多出来的实例丢掉
                return;
            }
            if (_model != null) Destroy(_model.gameObject);

            _model = model;
            _roleId = roleId;

            var parent = modelRoot != null ? modelRoot : transform;
            model.SetParent(parent, false);
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.gameObject.SetActive(true);

            // 把角色模型上的身份信息搬到 Actor 上（与 PlayerController 挂模型时同一套做法）：
            // 头像/常色/小头像/角色 id 都会被 HUD（PlayerWnd 的盟友行）、小地图、字幕标记读。
            // ⚠ **名字不搬**：盟友的显示名是他自己的玩家名（桥从资料写入），不是角色名。
            var baseObj = model.GetComponent<BaseObject>();
            if (baseObj != null)
            {
                if (_actor == null) _actor = GetComponent<Actor>();
                if (_actor != null)
                {
                    _actor.Id = baseObj.Id;
                    _actor.Portrait = baseObj.Portrait;
                    _actor.ExtraPortrait = baseObj.ExtraPortrait;
                    _actor.Color = baseObj.Color;
                }
            }

            // 角色确定了 ⇒ 广播一次（"按角色 id 判断"的系统要在这时才拿得到 Id，
            // 例如 Furniture_NPCChat 要把"被玩家占用的角色 NPC"藏起来）
            if (_actor != null) UnitEventBus.FriendRoleChanged(_actor);

            _anim = model.GetComponentInChildren<Animator>(true);
            ResetAnimState();      // 新模型先给一套"站立不动"基线，否则参数是默认 0，可能停在异常姿态
            if (_weapons != null) _weapons.BindModel(model);   // 手部 IK 要重新解析模型内的 IK
            _lastPos = transform.position;
        }

        /// <summary>动画参数复位成"站立不动"（与玩家静止时的取值一致：IsMove=false、Speed=1）。</summary>
        private void ResetAnimState()
        {
            if (_anim == null) return;
            _anim.SetBool(AnimIsMove, false);
            _anim.SetFloat(AnimSpeed, 1f);
            _lastDead = false;
            _anim.SetBool(AnimIsDeath, false);
        }

        private void Awake()
        {
            _view = GetComponent<NetTransformView>();
            _weapons = GetComponent<FriendWeaponView>();
            _anim = GetComponentInChildren<Animator>(true);
            _actor = GetComponent<Actor>();
            _lastPos = transform.position;
        }

        // ==================== 武器（转发给 FriendlyWeaponView；桥只认识本类） ====================

        /// <summary>【桥调用】按角色的武器预制体清单装配槽位并应用改装（资料变化时重调）。</summary>
        public void SetWeapons(IList<WeaponPlayerController> prefabs, int[][] upgrades)
        {
            if (_weapons != null) _weapons.Setup(prefabs, upgrades);
        }

        /// <summary>【桥调用】切换显示的武器槽（网络同步落点）。</summary>
        public void SetActiveWeaponSlot(int slotIndex)
        {
            if (_weapons != null) _weapons.SetActiveSlot(slotIndex);
        }

        /// <summary>【桥调用】开火表现（枪口闪光 + 枪响 + 弹道 + 枪械动画）。</summary>
        /// <param name="direction">开火瞬间的射击方向（世界空间）；零向量 = 用枪口朝向</param>
        /// <param name="aimPoint">开枪者准心实指的目标点（世界空间）；零向量 = 未知（见 <see cref="FriendWeaponView.PlayShoot"/>）</param>
        public void PlayShoot(Vector3 direction = default, Vector3 aimPoint = default)
        {
            if (_weapons != null) _weapons.PlayShoot(direction, aimPoint);
        }

        // ==================== 生命状态（只做镜像 + 表现） ====================

        /// <summary>预制体上的生命组件（若有）；只读它做"镜像"，不参与本地伤害结算。</summary>
        private Health _health;

        /// <summary>
        /// 【桥调用】应用同步过来的生命状态：血 / 盾的当前值与上限 + 是否倒地。
        ///
        /// <para>▍为什么是"镜像"而不是"自己算"：伤害与死亡判定由**各自主机权威**，
        /// 盟友实体在这里没有 <c>Damageable</c>（本地敌人扣不到它的血），所以写进来的值不会被本地战斗改写。</para>
        ///
        /// <para>▍倒地用写 <c>ActorState</c> 的方式表达（本地系统与动画都读它，走一条路）。</para>
        /// </summary>
        public void ApplyVital(float hp, float hpMax, float shield, float shieldMax, bool down)
        {
            if (_health == null) _health = GetComponent<Health>();
            if (_health != null)
            {
                if (hpMax > 0f) _health.MaxHealth = Mathf.RoundToInt(hpMax);
                if (shieldMax > 0f) _health.MaxShield = Mathf.RoundToInt(shieldMax);
                _health.CurrentHealth = (PEInt)hp;
                _health.CurrentShield = (PEInt)shield;
                _health.showHealth = Mathf.RoundToInt(hp);   // 编辑器可视化/调试用
            }

            if (_actor == null) _actor = GetComponent<Actor>();
            if (_actor != null) _actor.ActorState = down ? ActorState.Dead : ActorState.Normal;
        }

        /// <summary>【桥调用】应用同步过来的弹药系数（0~1，口径 = 对方 <c>TotalRemainAmmoRatio()</c>）。
        /// <para>▍只存着给 HUD 读（<c>PlayerWnd</c> 的盟友状态行），不参与任何本地战斗计算。</para></summary>
        public void ApplyAmmo(float ratio)
        {
            AmmoRatio = Mathf.Clamp01(ratio);
        }

        /// <summary>盟友的剩余弹药比例（0~1），由 09 的桥按同步值写入。</summary>
        public float AmmoRatio { get; private set; }

        /// <summary>
        /// 【桥调用】应用一条位姿快照 —— 转发给同物体上的 <see cref="NetTransformView"/>。
        /// <para>▍为什么保留这个转发（而不是让 09 的桥直接调 <c>NetTransformView</c>）：让桥只认识"盟友"这一个入口，
        /// 位姿怎么插值是本层（玩法层）内部的事；将来换插值实现也不用动桥。</para>
        /// </summary>
        /// <param name="teleport">true = 不插值，直接吸附</param>
        /// <param name="pitch">上身/武器的俯仰角（度）；0 = 不俯仰（旧版发送端）</param>
        public void ApplyPose(Vector3 pos, float yaw, float pitch, bool teleport)
        {
            if (_view != null) _view.ApplyPose(pos, yaw, pitch, teleport);
            else Debug.LogWarning("[FriendController] 缺少 NetTransformView ⇒ 位姿被丢弃（预制体上应带上它）。", this);
        }

        /// <summary>兼容旧调用（不带俯仰）。</summary>
        public void ApplyPose(Vector3 pos, float yaw, bool teleport) => ApplyPose(pos, yaw, 0f, teleport);

        private void Update()
        {

            // ⚠ 吸附（首次出现 / 传送）会让 transform 一跳 ⇒ 那一帧的"位移速度"是假的，先丢掉再算动画速度，
            //   否则动画会瞬间飙到最快。（职责拆开前是 ApplyPose 里同步回写 _lastPos，现在改用这个标记。）
            if (_view != null && _view.ConsumeSnap()) _lastPos = transform.position;

            // ⚠ 顺序保证：NetTransformView 标了 [DefaultExecutionOrder(-50)]，一定先写完 transform
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var vel = (transform.position - _lastPos) / dt;
            vel.y = 0f;
            _lastPos = transform.position;

            DriveAnim(vel);

            // 武器挂点跟着同步来的俯仰走：玩家那侧是 FirstPersonSocket 吃相机俯仰，
            // 盟友这边没有相机 ⇒ 由武器挂点复刻（否则别人看你抬头时模型仍平举枪）
            if (_weapons != null && _view != null) _weapons.SetAimPitch(_view.RenderedPitch);
        }

        /// <summary>
        /// 驱动移动/死亡动画 —— **照抄本地玩家的口径**（`PlayerController.cs:368-380` / `:518` / `:536`）：
        /// 速度 &gt; 0.5 才算移动；`Speed = 速度 ÷ 5 × 前后方向符号`；静止时 `Speed = 1`；死亡走 `IsDeath`。
        ///
        /// <para>▍为什么用"每帧位移"算速度而不是让网络包带速度：位姿里只有位置，插值出来的位移与玩家的
        /// <c>CharacterVelocity</c> 同源，表现才对得上（前进/倒退的符号也一并还原）。</para>
        /// </summary>
        private void DriveAnim(Vector3 vel)
        {
            if (_anim == null) return;

            // 死亡姿态：与玩家同一套 IsDeath（玩家在 OnDie/OnRevive 里设置）
            if (_actor == null) _actor = GetComponent<Actor>();
            bool dead = _actor != null && _actor.ActorState == ActorState.Dead;
            if (dead != _lastDead)
            {
                _lastDead = dead;
                _anim.SetBool(AnimIsDeath, dead);
            }
            if (dead)
            {
                _anim.SetBool(AnimIsMove, false);
                return;
            }

            float speed = vel.magnitude;
            if (speed > 0.5f)   // 与玩家同一阈值
            {
                _anim.SetBool(AnimIsMove, true);
                // 前后符号：玩家用 InverseTransformDirection(velocity).z 的符号（前进为正 / 倒退为负）
                _anim.SetFloat(AnimSpeed, speed / 5f * Mathf.Sign(transform.InverseTransformDirection(vel).z));
            }
            else
            {
                _anim.SetBool(AnimIsMove, false);
                _anim.SetFloat(AnimSpeed, 1f);
            }
        }
    }
}
