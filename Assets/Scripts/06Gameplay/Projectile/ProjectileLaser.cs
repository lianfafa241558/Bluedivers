using System.Collections.Generic;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Weapon;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 激光子弹
    /// </summary>
    [AddComponentMenu("子弹/激光", 30)]
    public class ProjectileLaser : ProjectileBase
    {

        /// <summary>射速</summary>
        public float ShootSpeed { get; private set; }

        [Header("通用")]
        [InspectorName("碰撞半径")]
        public float Radius = 0.01f;
        [InspectorName("使用武器作用范围作为半径")]
        public bool useExplosionRange = false;


        [Header("特效")]
        [InspectorName("创建终点物体")]
        public List<GameObject> EndObject;
        [InspectorName("终点物体")]
        public List<Transform> EndMoveObject;

        [Header("链状闪电")]
        [InspectorName("链状闪电特效")]
        [Tooltip("每一跳的连线特效：在上一跳落点生成、+Z 指向下一跳并铺满两点距离。\n" +
                 "带 LineRenderer 的预制体会被直接喂两端点；留空 = 只结算伤害、不画电弧。\n" +
                 "跳数由武器属性「通用/弹射次数」给，能跳多远由「弹射索敌半径」给")]
        public GameObject ChainVfx;

        [Header("表现")]
        [InspectorName("最低表现距离")]
        [Tooltip("光束线至少画这么长(米)，从枪口沿光束量。\n" +
                 "贴脸命中、或球扫在起点就与碰撞体重叠(退化命中，落点不可信)时，线不会缩成一个点/整条失踪。\n" +
                 "⚠ 只管画线，不改命中点与终点特效；0 = 关闭")]
        public float MinVisualDistance = 1f;

        protected float m_ShootTime;

        protected List<Collider> m_IgnoredColliders;
        protected LineRenderer m_line;
        protected List<Transform> m_EndObject;
        protected Vector3 m_lastPos;

        private float finalRaudis;

        /// <summary>细线射线每帧最多"穿过去"几个不挡线的命中（穿透地形贴地打时会出现连片地面碰撞体）</summary>
        private const int k_ThinSkipLimit = 8;

        /// <summary>光束采样的命中缓冲（每帧复用；Update 里不能分配）</summary>
        private readonly RaycastHit[] m_BeamHits = new RaycastHit[16];
        /// <summary>本节拍已结算过的单位：穿透时同一单位的多个部位只算一次伤害</summary>
        private readonly HashSet<GameObject> m_TickHitActors = new();

        protected virtual void OnEnable()
        {
            OnShoot += _OnShoot;
        }

        protected virtual void _OnShoot()
        {
            //这里可以有武器，但是update里面得脱落
            InitCatapult();          // 弹射次数（没配 = 0 = 老行为）+ 清空已命中表
            finalRaudis = useExplosionRange ? WeaponBase.AttrFinal(WeaponAttrType.ExplosionRange, new(0.1f)).RawFloat : Radius;
            
            ShootSpeed = WeaponBase.AttrFinal(WeaponAttrType.ShootInterval, new(0.1f)).RawFloat;
            m_ShootTime = Time.time;
            //直接设置父级就不用处理移动了；齐射时绑定实际发射的炮口
            transform.parent = Muzzle.IsValid() ? Muzzle : WeaponBase.GetMuzzle(0);

            //忽略发射者的碰撞
            m_IgnoredColliders = new List<Collider>();
            Collider[] weaponColliders = Owner.GetComponentsInChildren<Collider>();
            if (weaponColliders.IsValid()) m_IgnoredColliders.AddRange(weaponColliders);

            m_line = GetComponent<LineRenderer>();
            m_line.positionCount = 2;

            m_TickHitActors.Clear();
            // 开火这一帧只求终点来摆终点特效、不结算（首个伤害节拍在 ShootSpeed 之后，与原逻辑一致）
            m_lastPos = SampleThinBeam(out _, out _, out _, out _, out _);

            m_EndObject = new();
            foreach (var item in EndObject)
            {
                m_EndObject.Add(FPSGame.Core.VfxPool.Creat(item, m_lastPos, default, null).transform);
            }
            foreach (var item in EndMoveObject)
            {
                m_EndObject.Add(item);
            }
        }

        protected virtual void Update()
        {
            Move();
        }


        protected virtual void Move()
        {
            m_line.SetPosition(0, transform.position);

            // 结算节拍（一个节拍 = 一次"发"）。⚠ 必须整段提到扫描之前：放在每个命中内部的话，
            // 第一个命中就把 m_ShootTime 顶掉，穿透的后续命中全被这道闸门挡掉。
            bool settle = Time.time >= m_ShootTime + ShootSpeed;
            if (settle)
            {
                m_ShootTime = Time.time;
                // 每个伤害节拍都重新给满跳数：光束是"按住就一直在打"，不是一颗子弹只弹几次
                RefreshCatapult();
                m_TickHitActors.Clear();
            }
            // 终点与截断只由细线给（球扫只结算伤害，绝不改终点），所以线永远是一条直线，见 SampleBeam 说明
            Vector3 end = SampleBeam(settle, out Vector3 normal, out bool hitSomething, out _);

            // 表现层下限：贴脸命中 / 退化命中时线不能缩成一点。⚠ 只加长画出来的线，
            // end 仍按真实落点去摆终点特效（不然弹痕/命中药效会飘到半空）
            Vector3 lineEnd = end;
            if (MinVisualDistance > 0f &&
                (end - transform.position).sqrMagnitude < MinVisualDistance * MinVisualDistance)
            {
                lineEnd = transform.position + transform.forward * MinVisualDistance;
            }
            m_line.SetPosition(1, lineEnd);

            if (hitSomething)
            {
                SetEnd(m_lastPos, end, normal);
                m_lastPos = end;
            }
            else
            {
                SetLife();
            }
        }

        /// <summary>
        /// 沿整条光束做一次球扫，按距离从近到远决定"结算谁"；扫到哪停、线画到哪，全由细线给。
        ///
        /// <para>▍标旗口径与飞行弹 <see cref="ProjectileStandard"/> 一致：
        /// <c>PenetrateUnits</c> = 打中单位继续往后；<c>PenetrateTerrain</c> = 打中地形（没有 <see cref="Damageable"/>）也继续。
        /// 没勾的那一类就是光束的挡点。</para>
        /// <para>▍⚠ 职责必须拆开（2026-11-03 微波枪实测）：球扫半径 = 武器「作用范围」(米级)，只用来**放宽打人**；
        /// 截断与终点由细线 <see cref="SampleThinBeam"/> 独家决定。两条都踩过坑：拿粗球判地形 ⇒ 提前一个半径撞上
        /// 光束旁边的岩石，目标在二十米外线就断了、伤害永远落不到人身上；拿粗球落点当终点 ⇒ 落点偏轴一个半径，
        /// 近处 4m 就能偏 17°，线像"吸"在目标身上。</para>
        /// <para>▍⚠ 激光是瞬时从头扫到尾的，不像实体弹那样逐帧推进，所以穿透**不需要累计位移**，一次查询就够。</para>
        /// </summary>
        /// <param name="settle">是否结算伤害（false = 只扫出挡点，不结算）</param>
        /// <param name="normal">终点法线</param>
        /// <param name="hitSomething">是否打在实体上（false = 直达最大射程）</param>
        /// <param name="endIsUnit">终点是不是单位（截断原因是"打到人"还是"打到地形"）</param>
        protected Vector3 SampleBeam(bool settle, out Vector3 normal, out bool hitSomething, out bool endIsUnit)
        {
            Vector3 dir = transform.forward;
            Vector3 origin = transform.position + MinRange * dir;

            // ① 细线定挡点（与视觉同一条射线）：线画到哪、地形在哪截断，都只看它
            Vector3 end = SampleThinBeam(out normal, out hitSomething, out RaycastHit blockHit,
                out bool blockIsUnit, out float blockDist);
            endIsUnit = blockIsUnit;

            // ② 粗球只负责"擦边也算打中"：挡点之后的一律不算（被掩体挡住的目标不该掉血）
            int count = Physics.SphereCastNonAlloc(origin, finalRaudis, dir, m_BeamHits, MaxRange,
                FpsHelper.GetHittableLayers(DamageData.GetSpeed(1).RawInt));

            // 一根光束通常只有 0~3 个命中，插入排序比 Sort + 比较器省
            for (int i = 1; i < count; ++i)
            {
                RaycastHit cur = m_BeamHits[i];
                int j = i - 1;
                while (j >= 0 && m_BeamHits[j].distance > cur.distance) { m_BeamHits[j + 1] = m_BeamHits[j]; --j; }
                m_BeamHits[j + 1] = cur;
            }

            bool chainUsed = false;
            for (int i = 0; i < count; ++i)
            {
                RaycastHit hit = m_BeamHits[i];
                if (hit.distance > blockDist) break;
                // 被忽略的命中（自身碰撞 / IgnoreHitDetection / 无伤害组件触发器）既不结算也不截断，
                // 否则穿透会被这类杂物挡在半路
                if (!IsHitValid(hit)) continue;

                IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                // 粗球擦到的地形不结算也不截断：截断在上面的细线里已经定了
                if (!damageable.IsValid()) continue;

                // ⚠ distance=0 = "球在起点就已经和它重叠"，此时 point 是 (0,0,0) 而不是真落点。这类命中多是
                //   身边贴着的东西（自己的墙/友军），不在光束上 ⇒ 只有细线正好打在同一个碰撞体上才算真命中
                //   （近身目标走这条，它的落点由细线给，不用这里的 point）。
                if (hit.distance <= 0f && blockHit.collider != hit.collider) continue;

                // ⚠ 只结算、**不改终点**：球扫落点是"球面接触点"，最多偏轴一个半径（作用范围 2.1m 的枪在 4m 处
                //   能偏 17°），拿它当终点线会朝目标"吸"过去（2026-11-03 用户实测）。终点永远由细线给。
                if (settle) HitUnit(hit, damageable, ref chainUsed);
            }

            // 地形/道具挡点也要走一次 OnHit：爆炸、弹痕、命中音效都挂在 OnHit 那条链上
            if (settle && hitSomething && !endIsUnit) ApplyHit(blockHit, false);
            return end;
        }

        /// <summary>
        /// 沿光束打一根**细射线**，求"线该画到哪、挡点是谁"（表现与玩法共用，本身不结算）。
        ///
        /// <para>▍为什么不复用球扫的终点：<see cref="finalRaudis"/> 被"作用范围当半径"撑大后（几米级），
        /// 球扫会提前一个半径擦到光束旁边的岩石、贴地时甚至起始就与地面重叠 ⇒ 命中距离 ≈ 0，
        /// 光束一开火就画到脚下、人也永远打不到。所以截断与视觉走细线，粗球只判"打到谁"。</para>
        /// <para>▍穿得过去的东西要跳过接着打（<see cref="BlocksBeam"/> 与球扫同一条截断口径）：
        /// 这条线必须跟玩法一致，勾了穿透却把线停在墙上 = 打穿了还画断在半路。</para>
        /// <para>▍起点与射程口径跟 <see cref="SampleBeam"/> 一致（<see cref="MinRange"/> 前移 + <see cref="MaxRange"/>），
        /// 落空时按"枪口 + 最大射程"收尾，与原来的线一致。</para>
        /// </summary>
        /// <param name="normal">终点法线（落空 = -forward）</param>
        /// <param name="hitSomething">是否真的打到挡点（false = 直达最大射程）</param>
        /// <param name="blockHit">挡点的原始命中（地形要拿它结算爆炸/弹痕/音效）</param>
        /// <param name="blockIsUnit">挡点是单位还是地形</param>
        /// <param name="blockDist">挡点离球扫起点（枪口 + <see cref="MinRange"/>）的沿光束距离；落空 = <see cref="MaxRange"/></param>
        private Vector3 SampleThinBeam(out Vector3 normal, out bool hitSomething,
                                       out RaycastHit blockHit, out bool blockIsUnit, out float blockDist)
        {
            Vector3 dir = transform.forward;
            Vector3 end = transform.position + dir * MaxRange;
            normal = -dir;
            hitSomething = false;
            blockHit = default;
            blockIsUnit = false;
            blockDist = MaxRange;

            Vector3 cursor = transform.position + MinRange * dir;
            float left = MaxRange;
            float travelled = 0f;
            // 单条射线只能看到"第一个"命中，穿过去的那些要逐段往前推进重打（正常 0~1 次，不会是性能点）
            for (int i = 0; i < k_ThinSkipLimit && left > 0f; ++i)
            {
                if (!Physics.Raycast(cursor, dir, out RaycastHit hit, left, FpsHelper.GetHittableLayers(DamageData.GetSpeed(1).RawInt))) break;

                if (IsHitValid(hit) && BlocksBeam(hit.collider))
                {
                    // 起点已经嵌在这个碰撞体里时 distance=0、point 不可靠（贴墙开火）⇒ 退化成起点
                    if (hit.distance <= 0f) { hit.point = cursor; hit.normal = -dir; }
                    end = hit.point;
                    normal = hit.normal;
                    hitSomething = true;
                    blockHit = hit;
                    blockIsUnit = hit.collider.GetComponent<IDamageable>().IsValid();
                    blockDist = travelled + hit.distance;
                    break;
                }

                // 越过这一条继续找；+1cm 免得原地反复命中同一个碰撞体表面
                float step = hit.distance + 0.01f;
                left -= step;
                cursor += dir * step;
                travelled += step;
            }
            return end;
        }

        /// <summary>
        /// 这一条命中会不会让线停下来 —— 与 <see cref="SampleBeam"/> 里 break 的条件**同一条规则**：
        /// 单位看 <c>PenetrateUnits</c>、地形（没有 <see cref="Damageable"/>）看 <c>PenetrateTerrain</c>，
        /// 勾了就是穿过去。两处口径必须一致，否则线会跟伤害结算错位。
        /// </summary>
        private bool BlocksBeam(Collider col)
        {
            bool isUnit = col.GetComponent<IDamageable>().IsValid();
            return isUnit ? !HasBulletFlag(BulletFlag.PenetrateUnits)
                          : !HasBulletFlag(BulletFlag.PenetrateTerrain);
        }

        /// <summary>结算一次单位命中：同一单位本节拍只算一次（多个部位/护甲碰撞体不重复吃伤害）。</summary>
        private void HitUnit(RaycastHit hit, IDamageable damageable, ref bool chainUsed)
        {
            GameObject actorGo = damageable.ActorGo;
            if (actorGo == null) actorGo = hit.collider.gameObject;
            if (!m_TickHitActors.Add(actorGo)) return;

            // 链状闪电只从本节拍第一个吃伤害的单位起跳，穿透的后续目标不再重开一条链
            ApplyHit(hit, !chainUsed);
            chainUsed = true;
        }

        /// <summary>结算一次命中：伤害 + 命中表现。</summary>
        protected virtual void ApplyHit(RaycastHit hit, bool allowChain)
        {
            // 处理在碰撞箱内部的问题
            if (hit.distance <= 0f)
            {
                hit.point = transform.position;
                hit.normal = -transform.forward;
            }
            //Debug.LogError("击中"+ hit.collider.name);
            OnHit?.Invoke(BuildHitData(hit.point, hit.normal, hit.collider));

            // 链状闪电：主光束照旧跟着枪，伤害从命中点往最近的敌人逐跳传导
            if (allowChain) ChainLightning(hit.point, hit.collider);
        }

        /// <summary>
        /// 链状闪电：从主命中点开始，一跳一跳打向"最近的另一个"目标，直到次数用完或身边没别人。
        ///
        /// <para>▍跳的落点必须取**目标身上的真实碰撞体**：伤害管线是按 <c>collider.GetComponent&lt;IDamageable&gt;()</c>
        /// 找部位结算的，只给位置就等于打在空气上（见 <see cref="FpsHelper.Hit"/>）。</para>
        /// <para>▍伤害直接走 <see cref="ProjectileBase.OnHit"/>：本机那颗真弹照常结算，对端那颗表现弹
        /// 只播表现（<c>PlayImpactFxRemote</c>）⇒ 两端各算各的，不会翻倍。</para>
        /// </summary>
        private void ChainLightning(Vector3 hitPos, Collider hitCollider)
        {
            Vector3 from = hitPos;
            Collider fromCollider = hitCollider;

            while (TryCatapult(from, fromCollider, out var target))
            {
                if (!TryPickPart(target, from, out var partPos, out var partNormal, out var partCollider)) break;

                SpawnChainVfx(from, partPos);
                OnHit?.Invoke(BuildHitData(partPos, partNormal, partCollider));

                from = partPos;
                fromCollider = partCollider;   // 下一跳的"命中物"= 刚打中的部位（它会被记进已命中表）
            }
        }

        /// <summary>在目标身上挑离上一跳最近的肢体；法线朝回来向（命中特效/弹痕不会埋进身体里）。</summary>
        private static bool TryPickPart(IActor target, Vector3 from, out Vector3 pos, out Vector3 normal, out Collider collider)
        {
            pos = target.CenterPos;
            normal = Vector3.up;
            collider = null;

            var parts = target.Damageables;
            if (parts == null) return false;

            float bestSqr = float.MaxValue;
            for (int i = 0, l = parts.Length; i < l; ++i)
            {
                if (!(parts[i] is Damageable dam)) continue;
                var col = dam.ClosestCollider(from);
                if (!col) continue;
                Vector3 p = col.ClosestPointOnBounds(from);
                float sqr = (p - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; collider = col; pos = p; }
            }
            if (!collider) return false;

            normal = from - pos;
            normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            return true;
        }

        /// <summary>画一跳电弧：在起点生成、+Z 朝下一跳。带 LineRenderer 的预制体直接把两端点喂进去
        /// （与 <c>Pro_P_Laser</c> 同一口径：**世界空间**两点，免得受预制体缩放/旋转影响），
        /// 其余预制体当普通特效摆（起点 + 朝向）。
        /// <para>⚠ 松手时武器的 <c>EndLaser</c> 只回收光束本体，这些电弧靠各自 <see cref="LimitedLife"/> 自然回收。</para></summary>
        private void SpawnChainVfx(Vector3 from, Vector3 to)
        {
            if (!ChainVfx) return;

            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f) return;

            var go = FPSGame.Core.VfxPool.Creat(ChainVfx, from, Quaternion.LookRotation(dir));
            if (!go) return;

            if (go.TryGetComponent<LineRenderer>(out var line))
            {
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPosition(0, from);
                line.SetPosition(1, to);
            }
        }


        bool IsHitValid(RaycastHit hit)
        {
            var ihd = hit.collider.GetComponent<IgnoreHitDetection>();
            //使用忽略组件忽略点击
            if (ihd)
            {
                //双向忽略：对子弹/光束都透明（wire 这类装饰物就该穿过，别再收紧 —— 2026-11-03 用户明确要求）
                if (!ihd.Unidirectional)
                {
                    return false;
                }
                //单向忽略(如果发射点在碰撞箱内部就忽略)
                else if (hit.collider.bounds.Contains(InitialPosition))
                {
                    return false;
                }
            }

            //忽略没有可损坏组件的触发器的命中
            if (hit.collider.isTrigger && hit.collider.GetComponent<Damageable>() == null)
            {
                return false;
            }

            //忽略具有特定忽略碰撞器（默认情况下为自碰撞器）的碰撞
            if (m_IgnoredColliders != null && m_IgnoredColliders.Contains(hit.collider))
            {
                return false;
            }

            return true;
        }

        void SetEnd(Vector3 lastPos, Vector3 pos, Vector3 normal)
        {

            bool jump = Mathf.Abs(transform.InverseTransformDirection(pos).z - transform.InverseTransformDirection(lastPos).z) > 1 || Vector3.Distance(pos,lastPos)>3;
            pos += normal * 0.03f;
            //Debug.DrawRay(pos,normal,Color.red,1);
            foreach (var item in m_EndObject)
            {
                if (item.TryGetComponent<LimitedLife>(out var life))
                {
                    life.ResetLift(1);
                }
                else if(item.TryGetComponent<ParticleSystem>(out var ps))
                {
                    if (jump)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        item.position = pos;
                        ps.Play();
                    }
                    else
                    {
                        item.position = pos;
                    }

                    Quaternion targetRot = Quaternion.FromToRotation(Vector3.forward, normal);
                    var main = ps.main;
                    main.startRotationX = -targetRot.eulerAngles.x * Mathf.Deg2Rad;
                    main.startRotationY = -targetRot.eulerAngles.y * Mathf.Deg2Rad;

                    //var shapeModule = ps.shape;
                    //shapeModule.rotation = new Vector3(-targetRot.eulerAngles.x, -targetRot.eulerAngles.y,0);
                }
                else
                {
                    
                    item.forward = normal;
                }
            }
        }
        void SetLife()
        {
            foreach (var item in m_EndObject)
            {
                if (item.TryGetComponent<LimitedLife>(out var life))
                {
                    life.ResetLift(1);
                }
            }
        }
    }
}