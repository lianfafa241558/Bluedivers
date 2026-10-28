using System.Collections.Generic;
using PEMaths;

using UnityEngine;
using UnityEngine.Events;
using FPSGame.Utils;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;

namespace FPSGame.Weapon
{
    public abstract class ProjectileBase : MonoBehaviour
    {
        /// <summary>模板(用于回收)</summary>
        public ProjectileBase Template { get; set; }
        public GameObject Owner { get; private set; }
        public WeaponBaseController WeaponBase{ get; private set; }
        /// <summary>发射该子弹的炮口(齐射时可区分具体点位)</summary>
        public Transform Muzzle { get; private set; }

        public DamageData DamageData { get; protected set; }
        /// <summary>初始位置</summary>
        public Vector3 InitialPosition { get; private set; }
        /// <summary>初始方向</summary>
        public Vector3 InitialDirection { get; private set; }
        /// <summary>所继承的武器初速度</summary>
        public Vector3 InheritedMuzzleVelocity { get; private set; }

        /// <summary>充能系数?</summary>
        public PEInt Charge { get; private set; }
        /// <summary>音效范围</summary>
        public float SFXRange { get; private set; }

        /// <summary>最大射程 自爆引信</summary>
        public float MaxRange { get; private set; }

        /// <summary>最小射程 安全引信</summary>
        public float MinRange { get; private set; }
        

        /// <summary>重力加速度</summary>
        public float Gravity { get; private set; }

        protected Collider[] IgnoredColliders { get; private set; }

        public BulletFlag BulletFlag { get; private set; }

        public UnityAction OnShoot;
        public UnityAction<ProjectileHitData> OnHit;

        /// <summary>【表现弹专用】是否约束了终点（见 <see cref="SetVisualEndPoint"/>）。普通子弹恒为 false。</summary>
        public bool HasVisualEndPoint { get; private set; }

        /// <summary>【表现弹专用】期望的终点（世界空间）。</summary>
        public Vector3 VisualEndPoint { get; private set; }

        /// <summary>【表现弹专用】本颗是不是"只表现"的弹（由 <c>WeaponBaseController.SpawnVisualBullet</c> 标记）。
        /// <para>▍为什么要能分辨：制导弹（<c>ProjectilePlayerHoming</c>）要按**实时**准心刷新自己的终点，
        /// 而"真人子弹"的 <c>OnHit</c> 上挂着伤害 ⇒ 绝不能给它补终点（会在半路凭空结算 / 凭空播爆点）。</para></summary>
        public bool IsVisualBullet { get; private set; }

        /// <summary>【表现弹专用】标记为"只表现"（见 <see cref="IsVisualBullet"/>）。</summary>
        public void MarkVisualBullet() => IsVisualBullet = true;

        /// <summary>
        /// 【表现弹专用】指定"飞到这个点就算命中"（只影响弹道收尾，不改伤害）。
        ///
        /// <para>▍用途：联机远程弹道的终点来自**开枪者准心实指的那一点**（<c>PlayerShoot.HitX/Y/Z</c>，已同步）。
        /// 本端那颗"表现弹"是本地物理模拟的，而两端的敌人/世界只是近似一致 ⇒ 靠 <c>SphereCast</c> 自己撞常常擦不到，
        /// 表现上就是"子弹从目标身上穿过去、没命中"（2026-10-09 用户实测）。给个终点后，飞抵即按命中收尾。</para>
        /// </summary>
        public void SetVisualEndPoint(Vector3 point)
        {
            VisualEndPoint = point;
            HasVisualEndPoint = true;
        }

        public void Shoot(WeaponBaseController controller,int damageDataIndex=0)
        {
            Shoot(controller, damageDataIndex, controller.GetMuzzle(0));
        }
        public void Shoot(WeaponBaseController controller,int damageDataIndex, Transform muzzle)
        {
            if (!controller.IsValid()) { Debug.LogWarning("子弹没有武器来源"+gameObject); Tool.Destroy(gameObject); return; }

            Owner = controller.Owner;
            WeaponBase = controller;
            Muzzle = muzzle;
            InitialPosition = transform.position;
            InitialDirection = transform.forward;
            DamageData = controller.Damages[damageDataIndex];
            InheritedMuzzleVelocity = DamageData.InheritWeaponSpeed ? controller.MuzzleWorldVelocity : Vector3.zero;
            Charge = controller.WeaponChargeScale_D;
            SFXRange = controller.SFXRange;
            MaxRange = controller.CurrentWeaponRange;
            MinRange = Mathf.Max(DamageData.MinRange,0);
            Gravity = controller.CurrentGravity;
            BulletFlag = controller.BulletFlag;
            IgnoredColliders = controller.IgnoredColliders;
            // ★ 池化复用：上一次当"表现弹"时留下的终点/标记必须清掉（普通子弹永不约束弹道）
            HasVisualEndPoint = false;
            IsVisualBullet = false;
            OnHit += FpsHelper.Hit;
            OnShoot?.Invoke();
        }
        public void Release()
        { 
            //Debug.Log("释放" + gameObject, gameObject);
            FPSGame.Core.VfxPool.Release(this);
        }

        #region 弹射

        [Header("弹射")]
        [InspectorName("弹射索敌半径")]
        [Tooltip("命中单位后，以命中点为圆心在此半径内找最近的另一个目标继续飞；0 = 关闭弹射。\n次数由武器属性「通用/弹射次数」给，本字段只决定弹射时能搜多远")]
        public float CatapultRadius = 20f;

        /// <summary>剩余弹射次数：开火时从武器属性「通用/弹射次数」读，每弹一次 -1</summary>
        protected int m_CatapultLeft;

        /// <summary>弹射次数（本次开火读到的总数，供 <see cref="RefreshCatapult"/> 复位）</summary>
        protected int m_CatapultTotal;

        /// <summary>弹射过（已被甩到别的目标上）。弹种可据此关掉"拉回准心"之类的默认行为。</summary>
        protected bool HasCatapulted => m_Catapulted;
        private bool m_Catapulted;

        /// <summary>已命中过的单位根对象。两个用途：弹射索敌要排除它们；穿透单位靠它避免重复打同一个。
        /// <para>⚠ 跟 <c>m_CatapultLeft</c> 一样在 <see cref="InitCatapult"/> 里重置（池化复用必须清空）。</para></summary>
        protected List<GameObject> m_hasHits;

        /// <summary>开火时调用一次：读弹射次数 + 清空已命中表。</summary>
        protected void InitCatapult()
        {
            m_CatapultTotal = WeaponBase ? WeaponBase.AttrFinal(WeaponAttrType.Catapult).RawInt : 0;
            m_Catapulted = false;
            RefreshCatapult();
        }

        /// <summary>
        /// 把"剩余次数 / 已命中表"恢复到本段起点。
        /// <para>▍持续型弹种（光束）每个伤害节拍都要调一次：一次连击就把整条光束的次数用光的话，
        /// 之后按住扳机再也不导电了。</para>
        /// </summary>
        protected void RefreshCatapult()
        {
            m_CatapultLeft = m_CatapultTotal;
            if (m_hasHits == null) m_hasHits = new List<GameObject>();
            else m_hasHits.Clear();
        }

        /// <summary>
        /// 命中一个单位后尝试弹射：还有次数、且附近存在"最近的另一个"目标时返回 true
        /// （已扣次数、记录命中、并调过一次 <see cref="ResetLife"/>）。
        /// <para>▍这里只负责"选谁"：弹道型自己去改速度（<c>ProjectileStandard</c>），
        /// 光束型则拿它当链状闪电的下一跳（<c>ProjectileLaser</c>）。</para>
        /// </summary>
        /// <param name="hitPos">命中点（索敌圆心）</param>
        /// <param name="collider">命中的碰撞体（判断是不是可伤害单位）</param>
        /// <param name="target">要弹射过去的目标</param>
        protected bool TryCatapult(Vector3 hitPos, Collider collider, out IActor target)
        {
            target = null;
            if (m_CatapultLeft <= 0 || CatapultRadius <= 0f || !collider) return false;

            // 只对"可伤害单位"弹：打地形也弹 = 子弹永不消散，还会在墙角来回跳
            var damageable = collider.GetComponent<IDamageable>();
            if (!damageable.IsValid()) return false;

            if (m_hasHits == null) m_hasHits = new List<GameObject>();
            var hitActor = damageable.ActorGo;
            if (hitActor && !m_hasHits.Contains(hitActor)) m_hasHits.Add(hitActor);

            target = FindCatapultTarget(hitPos);
            if (target == null) return false;

            --m_CatapultLeft;
            m_Catapulted = true;
            ResetLife();
            NetSyncLog.BulletLog("弹射", $"{GetType().Name} 命中={collider.name} 弹射点={hitPos:F2} " +
                $"→ {target.ShowName} 剩余次数={m_CatapultLeft}");
            return true;
        }

        /// <summary>在命中点附近找最近的"另一个"目标（排除同队 / 已命中 / 不可伤害 / 无敌）。</summary>
        private IActor FindCatapultTarget(Vector3 center)
        {
            int team = ShooterTeam();
            List<IActor> units = UnitQuery.FindUnits(
                new PECircle((PEVector2)center, (PEInt)CatapultRadius),
                TargetCfg.All,
                item => FpsHelper.VaildTarget(item)
                     && item.Team != team
                     && !m_hasHits.Contains(item.gameObject));

            IActor best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0, l = units.Count; i < l; ++i)
            {
                float sqr = (units[i].CenterPos - center).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = units[i]; }
            }
            return best;
        }

        /// <summary>射击方所属队伍（弹射索敌排除友军）；解析不到按 0（= 玩家阵营）。</summary>
        private int ShooterTeam()
        {
            if (WeaponBase)
            {
                var actor = WeaponBase.GetComponentInParent<Actor>();
                if (actor) return actor.Team;
            }
            return Owner && Owner.TryGetComponent(out IActor ownerActor) ? ownerActor.Team : 0;
        }

        /// <summary>
        /// 重置本发的"生命"。默认什么都不做，由有存活计时 / 弹道锚点的弹种覆写。
        /// <para>▍为什么要它：弹射是"重开一段"，沿用上一段的计时会让快到期的子弹弹出去当场消散；
        /// 反过来，没有存活计时的弹种（光束）也有自己的"这一段"要重摆。</para>
        /// </summary>
        protected virtual void ResetLife() { }

        #endregion

        /// <summary>
        /// 构造命中数据包：统一填充本子弹的通用数据（伤害、充能、归属、音效范围、难度修正）。
        /// <para><paramref name="pos"/>/<paramref name="normal"/>/<paramref name="collider"/> 由调用方按各自的命中情况给出；
        /// 落空、超时释放或落地结算时传 <c>null</c> 碰撞体 ⇒ 只结算范围伤害。</para>
        /// </summary>
        /// <param name="damageData">要覆盖的伤害配置；不传则用子弹自己的 <see cref="DamageData"/></param>
        protected ProjectileHitData BuildHitData(Vector3 pos, Vector3 normal, Collider collider = null, IDamageData damageData = null)
        {
            return new ProjectileHitData
            {
                pos = pos,
                normal = normal,
                collider = collider,
                data = damageData ?? DamageData,
                chargeScale = Charge,
                // ⭐ 收口：一处算两个身份
                //   soure  = **起源**（统计）—— 空投武器被 VFXAirdropEffect 写成"呼叫者" ⇒ 天然正确
                //   caused = **直接来源**（显示/AI）—— 武器挂在谁身上就是谁：玩家手持=玩家；空投炮台=炮台自己
                //   （地雷/补给舱那类无武器的物件在各自的构造点显式置为自身，见 DeployableMine / AirdropPod）
                origin = Owner,
                caused = WeaponBase ? (WeaponBase.GetComponentInParent<Actor>()?.gameObject ?? Owner) : Owner,
                self = gameObject,
                sfxRange = SFXRange,
                weapon = WeaponBase,
                useDiffScale = BulletFlag.HasFlag(BulletFlag.EnemyIntensify),
                // 支援单位（各端各自生成的炮台/无人机）的子弹：伤害要走"非权威端只落玩家/盟友"那道门
                localEffectDamage = WeaponBase != null && WeaponBase.LocalEffectDamage,
            };
        }

        private void OnDisable()
        {
            OnShoot = null;
            OnHit = null;
        }

    }

    public struct ProjectileHitData {
        public Vector3 pos, normal; 
        public Collider collider;
        /// <summary>伤害来源（**起源**：统计口径 = 武器持有者 / 召唤物的呼叫者）</summary>
        public GameObject origin;
        /// <summary>**直接来源**（显示 / AI 口径）：真正发出这一发的东西（炮台本身 / 地雷）；
        /// null ⇒ 回落 <see cref="origin"/>。见 <c>ProjectileBase.BuildHitData</c> 的收口。</summary>
        public GameObject caused;
        /// <summary>伤害物体(自身)</summary>
        public GameObject self;
        /// <summary>伤害信息</summary>
        public IDamageData data;
        /// <summary>武器引用，只有击中特效依然是子弹的时候使用（？想办法去掉？）</summary>
        public WeaponBaseController weapon;
        /// <summary>充能系数</summary>
        public PEInt chargeScale;
        /// <summary>音效范围</summary>
        public float sfxRange;
        /// <summary>只有敌人使用，基于难度修正</summary>
        public bool useDiffScale;
        /// <summary>不会自伤</summary>
        public bool IgnoreSelf;

        /// <summary>本端**本地生成**的效果伤害（毒烟/火焰的 <c>SustainedEffect</c> per-tick、地雷自爆）。
        /// <para>▍为什么要标：这类东西**每个端各自生成一份、各自结算**，两端都落地 = N 倍伤害
        /// （见 <c>topics/net-dup-damage.md</c>）。</para>
        /// <para>▍口径：**非权威端只对玩家/盟友落地**（他们的血本机权威；且盟友实例在别人机器上
        /// 没有 <c>Damageable</c>，房主替他们扣不了血），其余目标等房主结算后的伤害广播。</para>
        /// <para>▍落点：本字段只是"事件级"标记，<c>FpsHelper</c> 会透传成 <c>DamagePacket.LocalEffect</c>，
        /// **门只有一处** = <c>Damageable.InflictDamage</c>。</para></summary>
        public bool localEffectDamage;
    }
}