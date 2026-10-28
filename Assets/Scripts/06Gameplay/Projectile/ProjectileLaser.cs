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
        protected virtual void OnEnable()
        {
            OnShoot += _OnShoot;
        }

        protected virtual void _OnShoot()
        {
            //这里可以有武器，但是update里面得脱落
            InitCatapult();          // 弹射次数（没配 = 0 = 老行为）+ 清空已命中表
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

            RaycastHit hit;

            if (!Physics.SphereCast(transform.position + MinRange * transform.forward, Radius, transform.forward, out hit, MaxRange, FpsHelper.GetHittableLayers(999)))
            {
                hit.point = transform.position + transform.forward * MaxRange;
                hit.normal = -transform.forward;

            }
            //Debug.DrawLine(transform.position + MinRange * transform.forward, hit.point,Color.red,3);
            m_lastPos = hit.point;
            m_EndObject = new();
            foreach (var item in EndObject)
            {
                //m_EndObject.Add(VFXManager.Creat(item, hit.point, Quaternion.LookRotation(hit.normal, Vector3.forward), null).transform);
                m_EndObject.Add(FPSGame.Core.VfxPool.Creat(item, hit.point, default, null).transform);
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
            Vector3 vector = transform.forward;
            Vector3 end;
            if (Physics.SphereCast(transform.position + MinRange * vector, Radius, vector, out var hit, MaxRange, FpsHelper.GetHittableLayers(999)))
            {
                m_line.SetPosition(1, hit.point);
                end = hit.point;
                TryHit(hit);
                SetEnd(m_lastPos,end, hit.normal, hit.transform.gameObject.layer == LayerDefinition.UnitLayers);
                m_lastPos = end;

            }
            else
            {
                hit.point = transform.position + vector * MaxRange;
                m_line.SetPosition(1, hit.point);
                SetLife();
                
            }
        }


        protected virtual void TryHit(RaycastHit hit)
        {
            //这样会导致前面射空了后，新击中目标立即造成伤害，属于无伤大雅的小bug，不管
            //每ShootSpeed 1Hit
            if (Time.time< m_ShootTime + ShootSpeed) return;
            m_ShootTime = Time.time;
            // 每个伤害节拍都重新给满跳数：光束是"按住就一直在打"，不是一颗子弹只弹几次
            RefreshCatapult();

            if (IsHitValid(hit))
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
                ChainLightning(hit.point, hit.collider);
            }

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