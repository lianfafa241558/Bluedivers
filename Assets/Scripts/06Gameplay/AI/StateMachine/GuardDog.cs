using System;
using System.Collections.Generic;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>
    /// 巡逻犬的巡逻与攻击行为。
    /// </summary>
    [AddComponentMenu("单位/_状态机/巡逻犬")]
    internal class GuardDog : StateMachineFrame<GuardDog.AIState>, IEquippable
    {

        internal enum AIState
        {
            WaitOwner,
            Idle,
            Follow,
            Attack,
            Return,
        }


        // 避障：预判 0.35s 的行程（最低 0.25m），速度越大探得越远，否则贴脸才反应
        private const float AvoidLookTime = 0.35f;
        private const float AvoidLookMin = 0.25f;
        private const float DetourTime = 0.35f;
        private const float StuckWindow = 0.25f;
        private const float StuckRatio = 0.3f;

        [InspectorName("移动速度")]
        public float moveSpeed = 5f;
        [InspectorName("跟随范围")]
        public float floowRaudius;
        [InspectorName("出击范围")]
        public float attackRaudius;

        //public bool AttackStop;

        [SerializeField]
        private EquippableFlagEnum flag;
        [SerializeField]
        Vector3 targetPoint;
        private Vector3 _formationOffset;
        public float _formationOffsetAngle;
        #region


        public string ID =>m_actor.Id;

        public IActor Owner { get;private set; }
        private Func<IEnumerable<IEquippable>> GetEquippableList;
        public event Action<IEquippable> OnEquipDestroy;

        public bool HaveFlag(EquippableFlagEnum flag)
        {
            return this.flag.HasFlag(flag);
        }
        public void OnInstall(IActor actor, Func<IEnumerable<IEquippable>> getEquippableList)
        {
            GetEquippableList = getEquippableList;
            Owner = actor;
            AiState = AIState.Idle;
           

        }

        public void OnUninstall()
        {
            GetEquippableList = null;
            Owner = null;
            AiState = AIState.WaitOwner;
        }

        public bool NeedUninstall(IEquippable newEquip)
        {
            var list = new List<IEquippable>(GetEquippableList()).FindAll(item => item.ID.Contains("Dog"));
            var index = list.FindIndex(item => ReferenceEquals(item, this));
            Debug.Log($"当前装备列表中共有{list.Count}个GuardDog，当前装备位于第{index}个");
            // 基础方向在Owner 右侧 45度，第n个偏移角度= 360/n
            int count = list.Count;
            float angle = 360f / count * (index + 1);
            Vector3 dir = Quaternion.AngleAxis(45f + angle, Vector3.up) * Owner.Forward;
            _formationOffset = dir.normalized * (floowRaudius - 2);
            _formationOffsetAngle = Mathf.DeltaAngle(0, 45f + angle);
            return newEquip.ID == ID;//ID相同时卸载
        }

        private void OnDestroy()
        {
            OnEquipDestroy?.Invoke(this);
        }

        #endregion
        #region 继承
        private CharacterController _controller;
        private Vector3 _moveVelocity = Vector3.zero;
        private Vector3 _smoothVelocity;      // SmoothDamp 的速度缓存，别和 _moveVelocity 混用
        private Vector3 _lastPos;             // 卡住检测：上一帧位置
        private float _stuckTimer;
        private float _stuckDistance;         // 窗口内累计实际位移
        private Vector3 _lastHitNormal;       // CC 最近一次撞击的法线（忽略地面）
        private Vector3 _detourNormal;        // 脱困用的障碍法线
        private float _detourTimer;           // >0 表示正在侧移脱困
        private float _detourSign = 1f;       // 1=右绕 -1=左绕，沿用上次方向防左右横跳
        private float _climbTimer;            // >0 表示正在抬升脱困（狗是飞行单位，能直接上）

        protected override void Init()
        {
            _controller = GetComponent<CharacterController>();
            _lastPos = transform.position;
        }

        protected override void Uninit()
        {

        }
        protected override Dictionary<AIState, AiStateHook> InitState()
        {
            return new() {
                [AIState.WaitOwner] = new() {
                    onEnter = StopWeapon,
                },
                [AIState.Idle] = new() {
                    onEnter = StopWeapon,
                    onUpdate = IdleUpdate,
                },
                [AIState.Follow] = new() {
                    onUpdate = FollowUpdate,
                },
                [AIState.Attack] = new() {
                    onUpdate = AttackUpdate,
                    onLateUpdate = AttackLateUpdate,
                },
                [AIState.Return] = new() {
                    onEnter = StopWeapon,
                    onUpdate = ReturnUpdate,
                },
            };
        }


        protected override void OnDetectedTarget()
        {
            m_TimeStartedDetection = Time.time;
            if (AiState != AIState.Return) AiState = AIState.Attack;
        }

        protected override void OnLostTarget()
        {
            m_TimeLostDetection = Time.time;
            if(AiState != AIState.Return) AiState = AIState.Follow;
        }
        #endregion

        bool InStopRaudius() => Vector3.Distance(m_actor.Pos, Owner.Pos) <= floowRaudius-2;
        bool InFloowRaudius() => Vector3.Distance(m_actor.Pos, Owner.Pos) <= floowRaudius;
        bool InAttackRaudius() => Vector3.Distance(m_actor.Pos, Owner.Pos) <= attackRaudius;

        #region WaitOwner

        #endregion

        #region Idle

        void StopWeapon()
        {
            turrets.ForEach(TryStop);
        }


        void IdleUpdate()
        {
            targetPoint = Owner.transform.TransformPoint(_formationOffset);
            MoveToTarget(0.05f);
            RotateToTarget(Owner.Pos + Owner.Forward * 10);
            if (!InFloowRaudius()) AiState = AIState.Follow;
        }
        #endregion

        #region Follow
        void FollowUpdate()
        {
            targetPoint = Owner.transform.TransformPoint(_formationOffset);
            MoveToTarget();
            RotateToTarget(Owner.Pos + Owner.Forward*10);

            if (Vector3.Distance(transform.position, targetPoint) < 0.25f) AiState = AIState.Idle;
        }

        #endregion

        #region Attack
        void AttackUpdate()
        {
            // 用表面距离(水平)判停：射程比目标半径小时，按中心点算永远"没到位" ⇒ 一直往前挤且永不开火
            float dis = DetectionTargetSurfaceDis(m_actor.Pos, true);
            float stopRange = DetectionModule.AttackRange;
            bool mustStop = dis< stopRange;

            targetPoint = mustStop?m_actor.Pos: DetectionTargetPos;
            MoveToTarget();

            if (AimTargrt() && mustStop && DetectionModule.IsSeeingTarget)
            {
                turrets.ForEach(item => {
                    if (item.IsLockTarget(DetectionTargetPos))
                    {
                        TryAtack(item);
                    }
                });
            }
            if (!InAttackRaudius()) AiState = AIState.Return;
        }

        protected bool AimTargrt()
        {
            bool mustShoot = false;
            foreach (var item in turrets)
            {
                if (mustShoot |= Time.time > m_TimeStartedDetection + item.detectionFireDelay) break;
            }

            //设置锁定点
            turrets.ForEach(item => item.Look(DetectionTargetPos));
            return mustShoot;
        }

        /// <summary>炮台锁头</summary>
        void AttackLateUpdate()
        {
            turrets.ForEach(item => item.Aiming(Time.time - m_TimeStartedDetection));
        }



        #endregion

        #region Return
        void ReturnUpdate()
        {
            targetPoint = Owner.transform.TransformPoint(_formationOffset);
            MoveToTarget();
            RotateToTarget(Owner.Pos + Owner.Forward * 10);
            //这个过程中不会索敌
            if (InStopRaudius())AiState= AIState.Idle;

        }

        #endregion



        public void TryAtack(Turret turret)
        {
            if (!turret.weapon) return;
            if (!turret.CanFireAt(DetectionTargetPos)) return;
            // 同样按表面距离判射程：否则短射程武器对大型单位(半径>射程)一辈子开不了火
            float dis = DetectionTargetSurfaceDis(turret.barrel.position);
            if (dis <= turret.weapon.CurrentWeaponExtremeRange)
            {
                turret.weapon.ShootInputs(true, true, false);
            }
        }
        public void TryStop(Turret turret)
        {
            if (!turret.weapon) return;
            turret.weapon.ShootInputs(false, false, true);
        }


        /// <summary>
        /// 向目标移动（三维：水平避障 + 垂直爬升，狗是飞行单位）
        /// </summary>
        private void MoveToTarget(float scale = 1)
        {
            if (targetPoint == default || _controller == null) return;

            Vector3 toTarget = targetPoint - transform.position;
            float distance = toTarget.magnitude;
            // 提速档只看水平距离：阵型点是水平的，别让高度差把速度拉满
            float planarDis = new Vector2(toTarget.x, toTarget.z).magnitude;

            // 到位只"停"，不再整段瞬移：原来逐帧 TryMove(整个偏移) 会把自己往墙里钉
            if (distance < 0.25f)
            {
                _moveVelocity = Vector3.zero;
                _smoothVelocity = Vector3.zero;
                _climbTimer = 0f;
                return;
            }

            // Idle 时用低速缓动，但若距离较远（玩家移动导致），自动提高
            float speedScale = scale;
            if (planarDis > 1.5f && scale < 1f)
            {
                speedScale = Mathf.Lerp(scale, 1f, Mathf.InverseLerp(1.5f, 5f, planarDis));
            }

            float speed = moveSpeed * speedScale;
            Vector3 desired = toTarget / distance;                    // 含 Y：允许直上直下
            Vector3 planar = new Vector3(desired.x, 0f, desired.z);

            Vector3 moveDir = Vector3.zero;
            bool forwardBlocked = false;
            if (planar.sqrMagnitude > 1e-4f)
            {
                moveDir = Steer(planar.normalized, speed, out forwardBlocked);
            }

            // 目标在上方而前方被挡（楼前）：先原地爬升到位再推进。
            // 不这么做就会贴楼面横爬，被窗台/雨棚/挑檐反复钩住。
            // ⚠ 必须确认头顶有净空：屋檐/楼板下升不上去，此时若也停横向就彻底死锁
            bool canClimb = HasHeadroom();
            bool climbOver = forwardBlocked && toTarget.y > 0.5f && canClimb;
            if (climbOver) moveDir = Vector3.zero;

            float verticalSpeed = VerticalSpeed(desired.y, speed, climbOver, canClimb);
            Vector3 targetVelocity = moveDir * speed + Vector3.up * verticalSpeed;

            // SmoothDamp 计算期望速度（平滑的是速度，方向突变由 Steer 兜住）
            _moveVelocity = Vector3.SmoothDamp(_moveVelocity, targetVelocity, ref _smoothVelocity, 0.2f, speed * 1.5f);
            _controller.TryMove(_moveVelocity * Time.deltaTime);
            UpdateStuck();
        }

        /// <summary>
        /// 垂直速度：脱困时优先直上；爬越给保底速度（纯按比例会越接近越慢，最后挂在楼檐下）。
        /// 头顶有楼板/天花板就不升，免得顶着板子抖。
        /// </summary>
        private float VerticalSpeed(float desiredRatio, float speed, bool climbOver, bool canClimb)
        {
            if (_climbTimer > 0f)
            {
                _climbTimer -= Time.deltaTime;
                return canClimb ? speed : 0f;
            }

            if (Mathf.Abs(desiredRatio) < 0.02f) return 0f;
            if (desiredRatio > 0f && !canClimb) return 0f;

            float ratio = climbOver ? Mathf.Max(Mathf.Abs(desiredRatio), 0.35f) : Mathf.Abs(desiredRatio);
            return Mathf.Sign(desiredRatio) * speed * ratio;
        }

        /// <summary>CC 只在真被顶住时回调，拿来取障碍法线；地面接触(y≈1)不算障碍</summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y < 0.5f) _lastHitNormal = hit.normal;
        }

        /// <summary>
        /// 把"朝目标的水平方向"换成"这一帧实际该走的水平方向"。
        /// CC 的 slide 在正对凸面/夹缝时位移≈0，光靠它永远顶在墙上，必须主动改朝向。
        /// </summary>
        /// <param name="forwardBlocked">正前方是否被挡（调用方据此决定要不要先爬升）</param>
        private Vector3 Steer(Vector3 desired, float speed, out bool forwardBlocked)
        {
            forwardBlocked = false;
            BodyCapsule(out Vector3 bottom, out Vector3 top);
            float radius = _controller.radius * 0.9f;                 // 略缩，避免贴面时误判
            // 从身体轴线出发 ⇒ 至少要走一个半径才能碰到障碍；再按当前速度预判一段提前绕
            float castDis = _controller.radius + Mathf.Max(AvoidLookMin, speed * AvoidLookTime);

            // 正在侧移脱困：不回头追目标（否则又顶上去）；此时也不再叠加"先爬升"逻辑
            if (_detourTimer > 0f)
            {
                _detourTimer -= Time.deltaTime;
                Vector3 tangent = Vector3.Cross(Vector3.up, _detourNormal).normalized * _detourSign;
                return (tangent + desired * 0.25f).normalized;
            }

            if (IsFree(bottom, top, radius, desired, castDis)) return desired;
            forwardBlocked = true;

            // 前方被挡：左右 30°/60°/90° 依次探，取偏转最小且通畅的一侧
            for (int i = 1; i <= 3; i++)
            {
                Vector3 right = Quaternion.AngleAxis(30f * i, Vector3.up) * desired;
                Vector3 left = Quaternion.AngleAxis(-30f * i, Vector3.up) * desired;
                bool freeRight = IsFree(bottom, top, radius, right, castDis);
                bool freeLeft = IsFree(bottom, top, radius, left, castDis);
                if (freeRight && freeLeft) return _detourSign > 0f ? right : left;
                if (freeRight) { _detourSign = 1f; return right; }
                if (freeLeft) { _detourSign = -1f; return left; }
            }

            // 三档全堵（墙角/两辆车之间）：上方有净空就抬升越障，否则沿切线侧移
            if (BeginDetour(desired)) return Vector3.zero;
            return Vector3.Cross(Vector3.up, _detourNormal).normalized * _detourSign;
        }

        /// <summary>自身碰撞体的胶囊轴线（CC 永远是世界朝向，不跟 transform 旋转）</summary>
        private void BodyCapsule(out Vector3 bottom, out Vector3 top)
        {
            Vector3 center = transform.position + _controller.center;
            float half = Mathf.Max(0f, _controller.height * 0.5f - _controller.radius);
            bottom = center - Vector3.up * half;
            top = center + Vector3.up * half;
        }

        /// <summary>该方向探测距离内是否无阻挡（起点与自身碰撞体重叠的会被 Physics 忽略）</summary>
        private static bool IsFree(Vector3 bottom, Vector3 top, float radius, Vector3 dir, float distance)
        {
            return !Physics.CapsuleCast(bottom, top, radius, dir, out _, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>头顶还有没有净空能抬升（在楼板/天花板底下就升不上去）</summary>
        private bool HasHeadroom()
        {
            BodyCapsule(out Vector3 bottom, out Vector3 top);
            float up = _controller.radius + _controller.skinWidth * 2f + 0.25f;
            return IsFree(bottom, top, _controller.radius * 0.9f, Vector3.up, up);
        }

        /// <summary>
        /// 进入脱困。返回 true = 选择抬升越障（飞行单位有净空时最快，也最像"飞过去"）；
        /// false = 沿障碍切线侧移，法线优先用 CC 撞出来的真实值。
        /// </summary>
        private bool BeginDetour(Vector3 desired)
        {
            if (HasHeadroom())
            {
                _climbTimer = DetourTime;
                return true;
            }

            _detourTimer = DetourTime;
            if (_lastHitNormal.sqrMagnitude > 0.01f)
            {
                _detourNormal = _lastHitNormal.normalized;
                _lastHitNormal = Vector3.zero;
            }
            else
            {
                _detourNormal = -desired;
            }

            // 切线有两个方向，选跟目标夹角小的；夹角接近时沿用上次，避免左右摇摆
            float dot = Vector3.Dot(Vector3.Cross(Vector3.up, _detourNormal), desired);
            if (Mathf.Abs(dot) > 0.1f) _detourSign = dot > 0f ? 1f : -1f;
            return false;
        }

        /// <summary>卡住兜底：窗口内实际位移远小于"速度×时间"说明被顶住了（探测漏判时的保险）</summary>
        private void UpdateStuck()
        {
            // 按三维位移统计：爬升时原地不动也是"在移动"，压平 Y 会把悬停误判成卡住
            _stuckDistance += Vector3.Distance(transform.position, _lastPos);
            _lastPos = transform.position;

            _stuckTimer += Time.deltaTime;
            if (_stuckTimer < StuckWindow) return;

            // 期望位移按"当前下发速度"算：Idle 只走 5% 速度，用 moveSpeed 会一直误判卡住
            float expected = _moveVelocity.magnitude * _stuckTimer * StuckRatio;
            if (expected > 0.01f && _stuckDistance < expected)
            {
                Vector3 toTarget = targetPoint - transform.position;
                toTarget.y = 0f;   // 侧移脱困只关心水平朝向（切线由水平法线叉乘得到）
                if (toTarget.sqrMagnitude > 0.0001f) BeginDetour(toTarget.normalized);
            }

            _stuckTimer = 0f;
            _stuckDistance = 0f;
        }

        /// <summary>
        /// 匀速向目标旋转
        /// </summary>
        private void RotateToTarget(Vector3 target)
        {
            // 只按水平方向转身：爬升后目标可能在正下方，带 Y 算 LookRotation 会让机体俯冲翻转
            Vector3 direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    90 * Time.deltaTime
                );
            }

            turrets.ForEach(item => item.Synchro());
        }



    }
}
