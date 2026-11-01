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

        protected float m_ShootTime;

        protected List<Collider> m_IgnoredColliders;
        protected LineRenderer m_line;
        protected List<Transform> m_EndObject;
        protected Vector3 m_lastPos;

        private float finalRaudis;

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
            Vector3 normal;
            bool hitSomething, endIsUnit;
            m_lastPos = SampleBeam(false, out normal, out hitSomething, out endIsUnit);

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
            Vector3 normal;
            bool hitSomething, endIsUnit;
            Vector3 end = SampleBeam(settle, out normal, out hitSomething, out endIsUnit);

            m_line.SetPosition(1, end);

            if (hitSomething)
            {
                SetEnd(m_lastPos, end, normal, endIsUnit);
                m_lastPos = end;
            }
            else
            {
                SetLife();
            }
        }

        /// <summary>
        /// 沿整条光束做一次球扫，按距离从近到远决定"结算谁、在哪里截断"。
        ///
        /// <para>▍标旗口径与飞行弹 <see cref="ProjectileStandard"/> 一致：
        /// <c>PenetrateUnits</c> = 打中单位继续往后；<c>PenetrateTerrain</c> = 打中地形（没有 <see cref="Damageable"/>）也继续。
        /// 没勾的那一类就是光束的挡点。</para>
        /// <para>▍⚠ 激光是瞬时从头扫到尾的，不像实体弹那样逐帧推进，所以穿透**不需要累计位移**，一次查询就够。</para>
        /// </summary>
        /// <param name="settle">是否结算伤害（开火首帧只求终点，不结算）</param>
        /// <param name="normal">终点法线</param>
        /// <param name="hitSomething">是否打在实体上（false = 直达最大射程）</param>
        /// <param name="endIsUnit">终点是不是单位（给 <see cref="SetEnd"/> 的命中类型）</param>
        protected Vector3 SampleBeam(bool settle, out Vector3 normal, out bool hitSomething, out bool endIsUnit)
        {
            Vector3 dir = transform.forward;
            Vector3 origin = transform.position + MinRange * dir;
            Vector3 end = transform.position + dir * MaxRange;
            normal = -dir;
            hitSomething = false;
            endIsUnit = false;

            int count = Physics.SphereCastNonAlloc(origin, finalRaudis, dir, m_BeamHits, MaxRange,
                FpsHelper.GetHittableLayers(999));
            if (count <= 0) return end;

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
                // 被忽略的命中（自身碰撞 / IgnoreHitDetection / 无伤害组件触发器）既不结算也不截断，
                // 否则穿透会被这类杂物挡在半路
                if (!IsHitValid(hit)) continue;

                IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                bool isUnit = damageable.IsValid();

                if (isUnit && !BulletFlag.HasFlag(BulletFlag.PenetrateUnits))
                {
                    if (settle) HitUnit(hit, damageable, ref chainUsed);
                    end = hit.point; normal = hit.normal; hitSomething = true; endIsUnit = true;
                    break;
                }
                if (!isUnit && !BulletFlag.HasFlag(BulletFlag.PenetrateTerrain))
                {
                    // 地形/道具也要走一次结算：爆炸、弹痕、命中音效都挂在 OnHit 那条链上
                    if (settle) ApplyHit(hit, false);
                    end = hit.point; normal = hit.normal; hitSomething = true;
                    break;
                }

                // 穿透：单位照常吃伤害，地形直接忽略继续往后打
                if (settle && isUnit) HitUnit(hit, damageable, ref chainUsed);
            }

            return end;
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
                //双向忽略
                if (!ihd.Unidirectional) {
                    return false;
                }
                //单向忽略(如果发射点在碰撞箱内部就忽略)
                else if(hit.collider.bounds.Contains(InitialPosition)){
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

        void SetEnd(Vector3 lastPos, Vector3 pos, Vector3 normal,bool isUnit)
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