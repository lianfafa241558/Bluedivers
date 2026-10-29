using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Attributes;
using FPSGame.GameContract;
using PEMaths;

using UnityEngine;
using UnityEngine.Events;
using FPSGame.Utils;
using FPSGame.Audio;
using FPSGame.Gameplay;
using FPSGame.Game;

namespace FPSGame.Weapon {
    using Attr = WeaponAttrType;

    [Flags]
    public enum BulletFlag
    {

        /// <summary>穿透单位</summary>
        [InspectorName("穿透单位")]
        PenetrateUnits = 1 << 0,
        /// <summary>穿透地形</summary>
        [InspectorName("穿透地形")]
        PenetrateTerrain = 1 << 1,
        /// <summary>命中保留</summary>
        [InspectorName("命中保留")]
        RetainOnHit = 1 << 2,
        /// <summary>享受敌人强化</summary>
        [InspectorName("享受敌人强化")]
        EnemyIntensify = 1 << 3,
        /// <summary>允许伤害自身(也没用了)</summary>
        [InspectorName("允许伤害自身(也没用了)")]
        AttackOneself = 1 << 4,
        //Everything = -1,
    }


    /// <summary>
    /// 基类，很单纯，只完成射击效果
    /// </summary>
    public class WeaponBaseController : LogicBehaviour
    {
        //给子类继承来控制每一发子弹的
        protected event UnityAction<ProjectileBase> OnBulletShoot;
        /// <summary> 每"次"射击，多弹丸和齐射也一样只触发一次 </summary>
        public UnityAction<WeaponBaseController> OnShoot;
        public event UnityAction<WeaponBaseController,bool> OnWantShootChange;


        #region 点位
        [Foldout("点位和信息", true)]
        [InspectorName("武器根对象")]
        public GameObject WeaponRoot;

        [SerializeField]
        [InspectorName("齐射")]
        [Tooltip("开火时所有发射点位一起开火，并按点位数量自动调整一次消耗的弹药量")]
        protected bool UseManyMuzzle;


        [SerializeField]
        [Compare("UseManyMuzzle", 0, CompareOperate.Equal)]
        [InspectorName("发射点位")]
        protected Transform WeaponMuzzle;

        [SerializeField]
        [Compare("UseManyMuzzle", 0, CompareOperate.Equal)]
        [InspectorName("发射点位2")]
        protected Transform WeaponMuzzle2;



        [SerializeField]
        [InspectorName("发射点位(齐射)")]
        [Compare("UseManyMuzzle", 1, CompareOperate.Equal)]
        protected List<Transform> WeaponManyMuzzles;

        /// <summary>
        /// 获取第 index 个发射点位。齐射武器从齐射点位列表取，否则从交替点位数组取；越界自动取模
        /// </summary>
        public Transform GetMuzzle(int index)
        {
            if (UseManyMuzzle && WeaponManyMuzzles != null && WeaponManyMuzzles.Count > 0)
            {
                return WeaponManyMuzzles[index % WeaponManyMuzzles.Count];
            }
            if (Muzzles != null && Muzzles.Length > 0)
            {
                return Muzzles[index % Muzzles.Length];
            }
            return WeaponMuzzle;
        }

        #endregion
        [Foldout("武器参数", true)]
        public WeaponCfg cfg = new();

        /// <summary>连射散布膨胀(度)：每"次"射击加上去，停火按 <see cref="SpreadBloomDecay"/> 回落</summary>
        private float _spreadBloom;

        /// <summary>当前实际散布角(度) = 属性散布 + 连射膨胀（首发不带膨胀，见 HandleShoot 的累加时机）</summary>
        public PEInt CurrentSpreadAngle => AttrFinal(Attr.BulletsSpreadAngle) + (PEInt)_spreadBloom;

        // ---- 连射散布膨胀的参数：**默认全关**，只有玩家武器覆写成读 RecoilProfile ----
        // ⚠ 刻意用虚属性而不是序列化字段：这是"玩家手感"，不该让每把敌人/炮塔武器都平白多 3 个字段。
        /// <summary>每"次"射击的散布膨胀(度)：0 = 不变散</summary>
        protected virtual float SpreadBloomPerShot => 0f;

        /// <summary>散布膨胀上限(度)</summary>
        protected virtual float SpreadBloomMax => 0f;

        /// <summary>散布回落速度(度/秒)</summary>
        protected virtual float SpreadBloomDecay => 8f;
        #region 子弹参数

        [Foldout("子弹参数", true)]

        //[InspectorName("子弹预制体")]
        //public ProjectileBase ProjectilePrefab;
        [InspectorName("子弹标旗")]
        public BulletFlag BulletFlag;

        public List<DamageData> Damages;
        
        public int UseDamageIndex = 0;
        public DamageData CurrentDamgeData => Damages[UseDamageIndex];

        /// <summary>"伤害配置漏配子弹预制体"是否已报过错（见 <see cref="WarnBulletPrefabMissing"/>）</summary>
        private bool _noBulletPrefabWarned;

        #endregion

        #region 特效
        [Foldout("特效和动画", true)]

        [InspectorName("枪口闪光的预制体")]
        public GameObject MuzzleFlashPrefab;

        /// <summary>闪光大小倍率</summary>
        [InspectorName("闪光大小倍率")]
        public float FlashSizeScale=1;

        /// <summary>闪光不附着于枪口</summary>
        [InspectorName("闪光不附着于枪口")]
        public bool UnparentMuzzleFlash;

        [InspectorName("音效范围")]
        public float SFXRange=20;

        /// <summary>逻辑层：开火噪声半径(米)，AI 听觉用。表现层音效距离是上面的 SFXRange，两套分开</summary>
        [InspectorName("开火噪声半径(逻辑)")]
        [Tooltip("AI 听觉用的开火噪声半径(米)：每次射击从枪口发出一条噪声，越大越响；<=0 表示不产生开火噪声。表现层音效播放距离用上面的音效范围 SFXRange")]
        public int FireNoiseRadius = 20;

        /// <summary>噪声倍率(属性，1=原值)：开火噪声与命中噪声共用的半径倍率（消音/加噪改装）</summary>
        public PEInt NoiseScale => AttrFinal(Attr.NoiseScale, 1);

        /// <summary>本次开火的**实际**噪声半径(米) = <see cref="FireNoiseRadius"/> × <see cref="NoiseScale"/>；
        /// ≤0 表示不产生噪声</summary>
        public PEInt FireNoiseRadiusFinal => (PEInt)FireNoiseRadius * NoiseScale;

        /// <summary>最近一次开火用的是**第几个发射点**（<c>Muzzles</c> 下标；<c>-1</c> = 齐射）。
        /// <para>▍用途：随开火消息上报，让接收端的弹道从**同一个枪管**出（多管枪轮流开火，接收端没有那份计数）。</para></summary>
        public int LastMuzzleIndex;

        /// <summary>
        /// 【逻辑噪声】从枪口发一条"开火噪声"（AI 听觉用）。**单一事实来源**，两条路都调它：
        /// ① 本机真开火（<c>HandleShoot</c>，<c>remote=false</c>）；
        /// ② **远端复刻**（<c>remote=true</c>）：收到别人开火的**表现**时，用**本地这份武器实例**
        /// （枪口 + 本实例的 <see cref="FireNoiseRadiusFinal"/>）再验算一遍 ⇒ 两端对"同一枪"的怪反应一致。
        /// ⚠ 刻意**不新增网络字段**：方向/落点本来就在同步，枪口与噪声半径取本地实例的即可（最多差一个位姿周期）。
        /// </summary>
        public void EmitFireNoise(bool remote)
        {
            // 半径先过噪声倍率(改装)，倍率 0 或基础值 0 都等于"没有噪声"
            PEInt radius = FireNoiseRadiusFinal;
            if (!FPSGame.GameContract.BattleHub.Current.IsPresent || radius <= 0) return;

            var muzzle = GetMuzzle(0);
            FPSGame.Utils.NetSyncLog.AiLog(remote ? "噪声·发(枪声·远端复刻)" : "噪声·发(枪声)",
                $"{(Owner != null ? Owner.name : name)} @{(muzzle ? muzzle.position : transform.position):F2} 半径={radius}",
                Owner != null ? Owner.GetInstanceID() : GetInstanceID(), 0.5f);

            UnitEventBus.Noise(new NoiseData
            {
                source = Owner,
                pos = (PEVector3)(muzzle ? muzzle.position : transform.position),
                radius = radius,
            });
        }

        [InspectorName("射击音效")]
        [Compare("UseContinuousShootSound", 0, CompareOperate.Equal)]
        public AudioClip ShootSfx;

        /// <summary>使用连续射击音效</summary>
        [SerializeField]
        [InspectorName("使用连续射击音效")]
        protected bool UseContinuousShootSound = false;
        /// <summary>连续射击初始音效</summary>
        [SerializeField]
        [InspectorName("连续射击初始音效")]
        [Compare("UseContinuousShootSound",1,CompareOperate.Equal)]
        protected AudioClip ContinuousShootStartSfx;
        /// <summary>连续射击持续音效</summary>
        [SerializeField]
        [InspectorName("连续射击持续音效")]
        [Compare("UseContinuousShootSound", 1, CompareOperate.Equal)]
        protected AudioClip ContinuousShootLoopSfx;
        /// <summary>连续射击结束音效</summary>
        [SerializeField]
        [InspectorName("连续射击结束音效")]
        [Compare("UseContinuousShootSound", 1, CompareOperate.Equal)]
        protected AudioClip ContinuousShootEndSfx;

        protected AudioSource m_ContinuousShootAudioSource = null;

        #endregion

        #region 热量系统

        /// <summary>当前热量 (0-100)</summary>
        [SerializeField]
        [InspectorName("当前热量")]
        private float _currentHeat;

        /// <summary>是否过热</summary>
        public bool IsOverheated { get; private set; }

        /// <summary>散热冷却计时器</summary>
        private float _coolTimer;

        /// <summary>过热持续时间计时器</summary>
        private float _overheatTimer;

        /// <summary>热量满值：<see cref="_currentHeat"/> 与过热判定都用它</summary>
        public const float MaxHeat = 100f;

        /// <summary>当前热量比例 [0,1]（0 = 冷，1 = 满热）。表现层（枪身过热变色 / HUD 热量条）读它</summary>
        public float HeatRatio => Mathf.Clamp01(_currentHeat / MaxHeat);

        /// <summary>是否配了热量系统（`HeatPerShot > 0`）。
        /// ⚠ 热量武器的节奏由热量决定，`Magazine` 只是能量池 ⇒ 别拿弹匣比例当热量显示。</summary>
        public bool HasHeatSystem => AttrFinal(Attr.HeatPerShot).RawFloat > 0f;

        /// <summary>正在散热（热量回落中）：过热阶段线性回落也算；散热延迟还没走完时为 false</summary>
        public bool IsCoolingDown => _currentHeat > 0f && (IsOverheated || _coolTimer <= 0f);

        #endregion

        #region 获取属性

        public PEInt AttrFinal(Attr attrType, PEInt defaultValue = default)
        {
            var attr = cfg[attrType];
            if (attr)
            {
                return attr.FinalValue;
            }
            return defaultValue;
        }
        #endregion

        private GameObject _origin;
        private GameObject _owner;

        /// <summary>武器来源（**起源**）：一般为对应单位；空投/装置类的"呼叫者"也写在这里
        /// （<c>VFXAirdropEffect</c> 生成后回填 <c>weapon.Origin = 呼叫者</c>）。
        /// <para>⚠ 它原本是个自动属性、全仓**只写不读** —— 真正参与伤害归属的是 <see cref="Owner"/>
        /// ⇒ 空投把呼叫者写进来后起源仍是装置自身，而装置没有 NetId ⇒ 击杀归属/统计两端都解析不到
        /// （见 <c>topics/net-dup-damage.md</c>）。</para>
        /// <para>现在 setter 顺手同步给 <see cref="Owner"/>，**仅当 Owner 还是"未设 / 本武器自身"**时
        /// —— 玩家武器、炮塔这些已经显式赋过 Owner 的不受影响。</para></summary>
        public GameObject Origin
        {
            get => _origin;
            set
            {
                _origin = value;
                if (value.IsValid() && (!_owner.IsValid() || _owner == gameObject)) _owner = value;
            }
        }

        /// <summary>
        /// 武器所有者:一般为对应单位，轰炸类武器重定向至自己
        /// </summary>
        public GameObject Owner{ get => _owner; set => _owner = value; }

        public virtual PEInt WeaponChargeScale_D => 1;

        public virtual float CurrentGravity => CurrentDamgeData.GetGravity(WeaponChargeScale_D).RawFloat;
        public virtual float CurrentSpeed => CurrentDamgeData.GetSpeed(WeaponChargeScale_D).RawFloat;

        public virtual float CurrentLife => CurrentDamgeData.MaxLifeTime;
        /// <summary>当前子弹的极限射程(包括爆炸范围)</summary>
        public virtual float CurrentWeaponExtremeRange => (CurrentDamgeData.MaxRange == -1 ? CurrentDamgeData.MaxLifeTime * CurrentSpeed : CurrentDamgeData.MaxRange)+ CurrentDamgeData.GetDamageOuterRadius(1).RawFloat;

        /// <summary>当前子弹的正常射程(不包括爆炸范围)</summary>
        public virtual float CurrentWeaponRange => (CurrentDamgeData.MaxRange == -1 ? CurrentDamgeData.MaxLifeTime * CurrentSpeed : CurrentDamgeData.MaxRange);


        public Collider[] IgnoredColliders { get; set;}//实际上暂时只有敌人使用

        /// <summary>
        /// 想要射击
        /// </summary>
        [SerializeField]
        protected bool wantsToShoot;
        public bool WantsToShoot
        {
            get => wantsToShoot;
            set{
                if(wantsToShoot!= value)
                {
                    wantsToShoot = value;
                    //Debug.LogError("变为"+ value,gameObject);
                    OnWantShootChange?.Invoke(this,value);
                }
            }
        }
        //[SerializeField]
        //private bool showAudioState;


        protected bool m_Initialized;

        public Vector3 MuzzleWorldVelocity { get; private set; }//武器世界速度
        Vector3 m_LastMuzzlePosition;//上一帧武器所在位置(计算速度)

        protected Transform[] Muzzles;
        private int shootCount=0;

        // ---- 连射（弹丸数量>1 且连射间隔>0 时生效）----
        /// <summary>还剩几轮连射要打（0 = 没有进行中的连射）。一轮 = 每个发射点各一发</summary>
        private int _burstRoundsLeft;
        /// <summary>距下一轮连射的剩余时间(秒)</summary>
        private float _burstTimer;
        /// <summary>连射用的发射点；齐射武器为 null（每轮遍历 <see cref="WeaponManyMuzzles"/>）</summary>
        private Transform _burstMuzzle;

        // ---- 远端复刻的连发排队（只生成表现弹，不结算伤害）----
        /// <summary>远端复刻还剩几轮没放；齐射武器每轮 = 每个发射点各一颗</summary>
        private int _visualBurstLeft;
        /// <summary>距下一轮远端复刻的剩余时间(秒)</summary>
        private float _visualBurstTimer;
        /// <summary>远端复刻用的发射点；齐射武器为 null</summary>
        private Transform _visualBurstMuzzle;
        /// <summary>远端复刻的终点约束（开枪者准心实指的点，后续各轮沿用）</summary>
        private Vector3 _visualBurstEndPoint;

        /// <summary>每次射击消耗的弹药量：基础值来自属性 <see cref="Attr.ShootCost"/>（齐射武器 = 发射点位数量），
        /// 因此**改装给的修正能走上来**。
        /// <para>⚠ 别再改回序列化字段：字段是死值，`modifys` 里的加减项加不到它身上（原来的写法就是这个问题）。</para>
        /// <para>⚠ 下限夹 1：`OnlyInt` 是**向下取整**，1 再吃一个 ×0.5 就变 0 —— 而 0 消耗在 `UseMagazine`
        /// 里等于永久无限弹（负值更糟：反向加弹）。真要无限弹请把"弹匣容量"配成负数
        /// （<see cref="InfiniteMagazine"/> 的判据就是这个）。</para></summary>
        public virtual int ShootCost => Mathf.Max(1, AttrFinal(Attr.ShootCost, 1).RawInt);

        /// <summary>每次射击消耗的弹药量（<see cref="ShootCost"/> 的**只读出口**）。
        /// <para>▍用途：联机上报"开火后的弹量"要拿它折算 —— <c>OnShoot</c> 是在 <see cref="HandleShoot"/> 里抛的，
        /// 而 <c>UseMagazine(ShootCost)</c> 在它**之后**才执行 ⇒ 事件回调里读到的还是"扣之前"的值
        /// （见 <c>VehicleWeaponsManager.TryGetAmmoAfterShot</c>）。</para></summary>
        public int AmmoPerShot => ShootCost;

        /// <summary>连射间隔(秒)：0 = 一次打出全部弹丸（霰弹枪）；>0 且弹丸数量 >1 时每隔这么久打一发。
        /// <para>▍弹丸数(3) + 间隔(0.08) ⇒ 三连发；弹丸数(8) + 间隔(0) ⇒ 霰弹枪（老行为）。</para></summary>
        public float BurstInterval => AttrFinal(Attr.BurstInterval).RawFloat;

        /// <summary>是否还有连射没打完（HUD / 调试可读）</summary>
        public bool InBurst => _burstRoundsLeft > 0;

        public override void LogicInit()
        {
            if (!UseManyMuzzle && !WeaponMuzzle)
            {
                Debug.LogError("错误:未配置武器开火点", gameObject);
                enabled = false;
            }
            if (UseManyMuzzle && WeaponManyMuzzles.Count == 0)
            {
                Debug.LogError("错误:开启齐射但未配置发射点位", gameObject);
                enabled = false;
            }
            if (Damages.Count==0)
            {
                Debug.LogError("错误:未配置武器伤害配置", gameObject);
                enabled = false;
            }
            var list = new List<Transform>();
            if (WeaponMuzzle.IsValid()) list.Add(WeaponMuzzle);
            if (WeaponMuzzle2.IsValid()) list.Add(WeaponMuzzle2);
            Muzzles = list.ToArray();

            // 齐射:开火时所有点位一起开火，一次消耗的弹药量在 InitAttribute 里按点位数量建初值
            if (!UseManyMuzzle && WeaponManyMuzzles.Count > 0)
            {
                Muzzles = WeaponManyMuzzles.ToArray();
            }

            InitAttribute();

            m_LastMuzzlePosition = WeaponRoot.transform.position;
            if (UseContinuousShootSound)
            {
                var audios = m_ContinuousShootAudioSource = gameObject.AddComponent<AudioSource>();
                audios.playOnAwake = false;
                audios.clip = ContinuousShootLoopSfx;
                audios.outputAudioMixerGroup = AudioSvc.GetMixGroup(AudioGroups.Weapon);
                audios.rolloffMode = AudioRolloffMode.Linear;
                audios.spatialBlend = 1;
                audios.minDistance = 3;
                audios.maxDistance = SFXRange;
                audios.loop = true;
            }
            m_Initialized = true;

        }

        public override void LogicUnInit()
        {
            UnInitAttribute();
        }
        protected virtual void InitAttribute()
        {
            //初始化
            cfg.Init();

            // 噪声倍率与伤害配置无关，基础值恒为 1(不改行为)；只吃改装加成。
            if (!cfg[Attr.NoiseScale]) cfg.Add(Attr.NoiseScale, 1);
            // 弹射次数：0 = 不弹射，同样与伤害配置无关（消费点是子弹那边读 AttrFinal）。
            if (!cfg[Attr.Catapult]) cfg.Add(Attr.Catapult, 0);
            // 射击消耗：齐射武器一次消耗 = 发射点位数量，其余为 1。
            if (!cfg[Attr.ShootCost]) cfg.Add(Attr.ShootCost, UseManyMuzzle ? Mathf.Max(1, WeaponManyMuzzles.Count) : 1);
            // 连射间隔：0 = 一次性全部打出（霰弹枪，老行为），只有配了正值才进连发
            if (!cfg[Attr.BurstInterval]) cfg.Add(Attr.BurstInterval, 0);
        }

        protected virtual void UnInitAttribute()
        {

        }

        protected virtual void Update()
        {
            UpdateContinuousShootSound();
            UpdateHeatSystem();
        }

        public override void LogicTick()
        {
            //计算武器移动速度
            MuzzleWorldVelocity = (WeaponRoot.transform.position - m_LastMuzzlePosition) / TickTime.RawFloat;
            m_LastMuzzlePosition = WeaponRoot.transform.position;

            // 连射散布膨胀回落：走逻辑帧，与弹道同一条时间轴（放 Update 会被子类 override 漏掉）
            if (_spreadBloom > 0f)
                _spreadBloom = Mathf.MoveTowards(_spreadBloom, 0f, SpreadBloomDecay * TickTime.RawFloat);

            // 连射：补打上一轮扣扳机没打完的弹丸（弹丸数>1 且连射间隔>0 才有事可做）
            UpdateBurst();
            // 远端开火的连发排队（纯表现），与本地连射同一条时间轴
            UpdateVisualBurst();

        }

        #region 音效
        protected AudioSource PlaySFX(AudioClip sfx)
        {
            //Debug.LogError("播放"+ sfx,gameObject);
            if (sfx.IsValid())return AudioSvc.PlaySound(new(sfx, transform.position, SFXRange, AudioGroups.Weapon));
            return null;
        }

        /// <summary>
        /// 【表现层】只播"开火音效"，不产生任何射击逻辑（不发子弹、不结算伤害、不动弹药）。
        /// <para>▍用途：远程盟友的"枪响"同步 —— 他的伤害在他自己机器上算，这边只需要听见响。
        /// 本机自己的开枪请走 <c>HandleShoot</c>（那边会连带音效）。</para>
        /// </summary>
        public AudioSource PlayShootSfx() => PlaySFX(ShootSfx);

        /// <summary>
        /// 【表现层】只生成一颗"**不结算伤害**"的子弹：飞行、尾迹、命中特效照常，只把伤害回调摘掉。
        ///
        /// <para>▍用途：远程玩家（盟友）的弹道同步 —— 他的伤害由他自己机器结算（各自模拟各自的战场），
        /// 这边只需要**看得见弹道**。原先只在收到开火消息时放枪口闪光，看起来像"打空枪"。</para>
        ///
        /// <para>▍怎么做到"只表现"：<c>ProjectileBase.Shoot</c> 会往 <c>OnHit</c> 上挂 <c>FpsHelper.Hit</c>（伤害），
        /// 而 <c>ProjectileStandard.OnEnable</c> 挂的是 <c>HitFX</c>（命中特效）—— 两者共用同一个 <c>OnHit</c>，
        /// 所以 <b>只摘掉 <c>FpsHelper.Hit</c></b>：弹道与命中特效保留、伤害归零。</para>
        ///
        /// <para>▍与 <c>ShootFromMuzzle</c> 的差别：不走 <c>OnBulletShoot</c>、不生成落点预示
        /// （那是本机给玩家看的预测提示）、每"次"射击只给一颗（<c>BulletsPerShot</c> 的多弹丸不必在对端重放）。</para>
        /// </summary>
        /// <param name="direction">射击方向（世界空间）；零向量 = 用枪口朝向 + 本武器散布</param>
        /// <param name="endPoint">★ 开枪者**准心实指的目标点**（世界空间，来自 <c>PlayerShoot.HitX/Y/Z</c>）；
        /// 零向量 = 未知（退回"自由飞"的老行为）。给了它就约束弹道"飞抵即命中"，
        /// 见 <see cref="ProjectileBase.SetVisualEndPoint"/>（2026-10-09：修"远端子弹穿过目标"）。</param>
        public ProjectileBase SpawnVisualBullet(Transform muzzle, Vector3 direction = default, Vector3 endPoint = default)
        {
            if (muzzle == null) muzzle = GetMuzzle(0);
            if (muzzle == null || Damages == null || Damages.Count == 0)
            {
                // 数据说话：静默 return 的三种成因（枪口没解析到 / 这把枪没有伤害配置）⇒ "对端开了枪但本端没有弹道"
                FPSGame.Utils.NetSyncLog.Warn("生成表现弹", $"生成失败：枪口={(muzzle != null ? muzzle.name : "<无>")} " +
                    $"伤害配置数={(Damages != null ? Damages.Count : -1)} 武器={name}");
                return null;
            }

            var data = CurrentDamgeData;
            if (data == null || data.BulletPrefab == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("生成表现弹", $"生成失败：伤害配置={(data != null)} 子弹预制体={(data != null ? data.BulletPrefab : null)} 武器={name}");
                return null;
            }

            // ⚠ 方向优先用**发送端传来的**：盟友模型只同步了 yaw、没有俯仰，
            //   用枪口朝向会让所有弹道都水平（抬头打空中目标时明显不对）。
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : GetShotDirectionWithinSpread(muzzle);
            var bullet = FPSGame.Core.VfxPool.Creat(data.BulletPrefab, muzzle.position, Quaternion.LookRotation(dir));
            if (bullet == null) return null;

            // 数据说话：表现弹的身份 / 起点 / 有没有终点约定（有终点就该在终点收尾，没终点才会"飞过去"）
            FPSGame.Utils.NetSyncLog.BulletLog("生成表现弹", $"{bullet.GetType().Name} 起点={muzzle.position:F2} 方向={dir:F2}" +
                (endPoint.sqrMagnitude > 0.0001f ? $" 终点={endPoint:F2} 距终点={(endPoint - muzzle.position).magnitude:F1}m" : " 终点=<无>（自由飞，会飞到射程尽头）"));

            bullet.Shoot(this, UseDamageIndex, muzzle);
            bullet.MarkVisualBullet();           // ★ 标记"只表现"（必须在 Shoot 之后：Shoot 里会复位）
            bullet.OnHit -= FpsHelper.Hit;      // ★ 只摘伤害，别用 `OnHit = null`：那会把命中特效一起摘掉
            // ★ 但"命中表现"要补回来（2026-10-09）：FpsHelper.Hit 里除了伤害还包含命中特效/音效/弹痕，
            //   摘掉后对方屏幕上看不到任何命中反馈 ⇒ 表现弹命中时改成只播表现那一半。
            // ★ 改挂"表现 + **本地验算一次命中噪声**"（2026-10-11，用户口径"两边一致"）：
            //   原来只挂 PlayImpactFx ⇒ 本端的怪听不到"队友的子弹落地/爆炸"。
            bullet.OnHit += FpsHelper.PlayImpactFxRemote;
            // ★ 终点约束：飞抵开枪者瞄的那一点就收尾（否则会从目标身上穿过去，见 SetVisualEndPoint 说明）
            if (endPoint.sqrMagnitude > 0.0001f) bullet.SetVisualEndPoint(endPoint);
            return bullet;
        }

        /// <summary>
        /// 【表现层】按本武器**真实弹丸数**重放一轮表现弹（联机"远端开火"用）。
        ///
        /// <para>▍口径与 <c>HandleShoot</c> 一致：<c>AttrFinal(Attr.BulletsPerShot)</c> × (齐射 ? 多发射点数 : 1)——
        /// 远端这把武器实例带着**同样的改装配置** ⇒ 弹丸数可本地验算，不必上网（同"枪声本地复刻"的思路）。</para>
        ///
        /// <para>⚠ 只有**首颗**吃开枪端传来的 <paramref name="direction"/>/<paramref name="endPoint"/>（那是他准心实指的点）；
        /// 其余各颗用本端散布自由飞 —— 否则多弹丸会全部挤在同一点，看起来反而更假。</para>
        ///
        /// <para>▍连发（<see cref="BurstInterval"/> &gt;0）时**只放第一轮**，其余轮次进 <c>_visualBurstLeft</c>
        /// 由 <see cref="UpdateVisualBurst"/> 按同一间隔补放 —— 否则三连发在别人屏幕上还是"一枪三弹齐出"。</para>
        /// </summary>
        /// <param name="muzzleIndex">开枪者用的是第几个发射点（<c>&gt;=0</c> = 该枪管；<c>&lt;0</c> = 齐射所有多发射点）。
        /// ⚠ 多管枪轮流开火，本端那把武器**从不本机开火** ⇒ 自己的计数永远是 0，必须靠消息里的下标。</param>
        public void SpawnVisualSalvo(Transform muzzle, Vector3 direction = default, Vector3 endPoint = default,
            int muzzleIndex = 0)
        {
            int count = Mathf.Clamp(AttrFinal(Attr.BulletsPerShot, 1).RawInt, 1, 64);
            // 连发：只放第一轮，其余按间隔排队（弹丸数 3 + 间隔 0.08 ⇒ 三连发）
            float interval = BurstInterval;
            bool isBurst = count > 1 && interval > 0f;
            int firstRound = isBurst ? 1 : count;

            if (muzzleIndex < 0 && UseManyMuzzle && WeaponManyMuzzles != null && WeaponManyMuzzles.Count > 0)
            {
                for (int u = 0; u < WeaponManyMuzzles.Count; ++u)
                    for (int i = 0; i < firstRound; ++i)
                        SpawnVisualBullet(WeaponManyMuzzles[u], (u == 0 && i == 0) ? direction : default,
                            (u == 0 && i == 0) ? endPoint : default);

                _visualBurstMuzzle = null;
                _visualBurstEndPoint = endPoint;
                _visualBurstLeft = isBurst ? count - 1 : 0;
                _visualBurstTimer = interval;
                return;
            }

            // ★ 发射点与开枪端对齐（多管轮流开火）
            Transform pipe = muzzle;
            if (Muzzles != null && muzzleIndex >= 0 && muzzleIndex < Muzzles.Length) pipe = Muzzles[muzzleIndex];

            for (int i = 0; i < firstRound; ++i)
                SpawnVisualBullet(pipe, i == 0 ? direction : default, i == 0 ? endPoint : default);

            _visualBurstMuzzle = pipe;
            _visualBurstEndPoint = endPoint;
            _visualBurstLeft = isBurst ? count - 1 : 0;
            _visualBurstTimer = interval;
        }
        /// <summary>
        /// 发射音效，需要有连续射击
        /// </summary>
        /// <param name="sfx"></param>
        protected void ShotSFX(AudioClip sfx)
        {
            //Debug.LogError("shot"+ sfx,gameObject);
            if (!m_ContinuousShootAudioSource)
            {
                Debug.LogError("在没有连续射击时尝试shot音效");
            }
            if (sfx.IsValid())
            {
                m_ContinuousShootAudioSource.clip = sfx;
                m_ContinuousShootAudioSource.Play();
            }
        }

        /// <summary>更新持续射击的音效</summary>
        void UpdateContinuousShootSound()
        {
            if (UseContinuousShootSound)
            {
                //敌人都是按着左键不撒手的
                if (WantsToShoot)
                {
                    if (!m_ContinuousShootAudioSource.isPlaying)
                    {
                        ShotSFX(ContinuousShootStartSfx);
                        m_ContinuousShootAudioSource.Play();
                    }
                }
                else if (m_ContinuousShootAudioSource.isPlaying)
                {
                    // ⚠ 收尾音必须**独立播**：原来 `ShotSFX(End)` 是"换 clip + Play"打在这条循环源上，
                    //   紧跟着又 Stop() ⇒ 收尾音活不过一帧，等于没有；换成同一条循环源还要等它播完才能复用，会卡住下一次开火。
                    //   走 PlaySFX 的一次性空间音（与 ShootSfx 同一条路），循环源立刻停，互不干扰。
                    m_ContinuousShootAudioSource.Stop();
                    PlaySFX(ContinuousShootEndSfx);
                }
            }
        }
        #endregion

        #region 射击

        /// <summary>本次开火是否**只生成"表现弹"**（飞得出来、命中有反馈，但不结算任何伤害）。
        /// <para>▍默认 false = 本机真开火。需要覆写的是"**各端本地生成、自己按 tick 开火**"的装置
        /// （<c>WeaponTemporaryController</c>）：非权威端必须只打表现弹，否则 N 个端 = N 倍伤害
        /// （见 <c>BattleState.IsTickDamageAuthority</c>、<c>topics/net-dup-damage.md</c>）。</para>
        /// <para>⚠ 别在基类统一判定：玩家自己的枪在成员端也**必须**真开火（伤害靠 4025 上报、房主结算），
        /// 一律门掉会让他永远打不出伤害。</para></summary>
        protected virtual bool IsShotVisualOnly => false;

        /// <summary>本武器打出的伤害属于"**各端本地生成**的东西造成的"（支援炮台 / 治疗无人机 / 火箭包这类
        /// 空投支援单位：每个端各自 Instantiate、各自 AI 开火）。
        /// <para>▍置位方：<c>VFXAirdropEffect.SpawnCreatObject</c>（生成后按端身份打标，不在 prefab 里配）。</para>
        /// <para>▍消费方：<c>ProjectileBase.BuildHitData</c> → <c>ProjectileHitData.localEffectDamage</c>
        /// → <c>DamagePacket.LocalEffect</c> ⇒ 非权威端只对玩家/盟友落地（见 topics/net-dup-damage.md）。</para>
        /// <para>⚠ 玩家驾驶的载具武器不能标：那是玩家自己开的枪，伤害必须在本机落地。</para></summary>
        public bool LocalEffectDamage { get; set; }

        /// <summary>
        /// 进行射击
        /// </summary>
        /// <returns></returns>
        protected virtual void HandleShoot()
        {
            // 过热时不能射击
            if (IsOverheated)
            {
                return;
            }

            //发射数量，如果以后做了蓄力会更多弹丸数量再说
            int bulletsPerShotFinal = AttrFinal(Attr.BulletsPerShot,1).RawInt;

            // 连射：弹丸数 >1 且连射间隔 >0 ⇒ 本次只打第一轮，剩下的按间隔在 LogicTick 里逐轮打。
            // 弹丸数<=1 或间隔=0 时保持"一次性全部打出"（霰弹枪 / 老行为）。
            // ⚠ 弹药在 TryShoot 里一次性扣整轮(ShootCost)，连射中途不再结算弹药。
            float burstInterval = BurstInterval;
            bool isBurst = bulletsPerShotFinal > 1 && burstInterval > 0f;
            int firstRoundCount = isBurst ? 1 : bulletsPerShotFinal;

            if (UseManyMuzzle)
            {
                LastMuzzleIndex = -1;   // 齐射：所有发射点一起开
                _burstMuzzle = null;    // 连射的后续轮次遍历 WeaponManyMuzzles
                // 齐射:所有发射点位一起开火
                for (int u = 0; u < WeaponManyMuzzles.Count; ++u)
                {
                    ShootFromMuzzle(WeaponManyMuzzles[u], firstRoundCount);
                }
            }
            else
            {
                // 交替射击
                LastMuzzleIndex = shootCount % Muzzles.Length;   // ★ 先记下标再自增（接收端按它选枪管）
                Transform muzzle = WeaponMuzzle = Muzzles[shootCount++ % Muzzles.Length];
                ShootFromMuzzle(muzzle, firstRoundCount);
                _burstMuzzle = muzzle;
            }

            // 连射状态每次扣扳机都整份重写：新的扣扳机会接管上一段没打完的连射（正常被 ShootInterval 卡着不会重叠），
            // 配表在连射途中被改掉（换模组）时也会在这里收尾。
            _burstRoundsLeft = isBurst ? bulletsPerShotFinal - 1 : 0;
            _burstTimer = burstInterval;

            if (!UseContinuousShootSound)
            {
                PlaySFX(ShootSfx);
            }
            OnShoot?.Invoke(this);

            //逻辑层噪声：枪声从枪口发出(每次射击一条，齐射/多弹丸也只发一条)。
            //表现层音效距离走上面的 SFXRange，两者互不影响
            EmitFireNoise(false);

            // 一发一份的代价（连射膨胀 / 热量）：**连发时每轮各算一次**，见 UpdateBurst
            ApplyPerShotCost();
        }

        /// <summary>一"发"的代价：连射散布膨胀 + 热量。
        /// <para>▍触发点两处：<see cref="HandleShoot"/>（首轮 / 霰弹枪的一次性全打）与
        /// <see cref="UpdateBurst"/>（连发后续每轮）。⚠ 别再放回 HandleShoot 里当"每次扣扳机一份"——
        /// 三连发的热量/散布就只有一发了。</para></summary>
        private void ApplyPerShotCost()
        {
            // 连射散布膨胀：**发射之后**才累加 ⇒ 首发永远是最准的（先加会让第一发就散）
            if (SpreadBloomPerShot > 0f)
                _spreadBloom = Mathf.Min(_spreadBloom + SpreadBloomPerShot, SpreadBloomMax);

            // 热量系统：每发增加热量，蓄力武器受蓄力热量倍率影响
            var heatPerShot = AttrFinal(Attr.HeatPerShot).RawFloat;
            if (heatPerShot <= 0f) return;
            var chargeHeatScale = PEMath.Lerp(1, AttrFinal(Attr.ChargeHeatScale, 1), WeaponChargeScale_D);
            _currentHeat += heatPerShot * chargeHeatScale.RawFloat;
            _coolTimer = AttrFinal(Attr.CoolDelay).RawFloat;
            if (_currentHeat >= MaxHeat)
            {
                _currentHeat = MaxHeat;
                IsOverheated = true;
                _overheatTimer = AttrFinal(Attr.OverheatDuration).RawFloat;
            }
        }

        /// <summary>连射推进：每逻辑帧扣一次时间，到点打一轮（每个发射点各一发）。
        /// <para>⚠ 走逻辑帧而不是协程：武器被收起/销毁时 <see cref="OnDisable"/> 直接取消，
        /// 不会出现"换掉枪之后又在下一把枪上补打两发"。</para>
        /// <para>⚠ 粒度就是 1 个逻辑帧（20ms）：间隔配得比帧长还小会退化成"每帧一发"。</para></summary>
        private void UpdateBurst()
        {
            if (_burstRoundsLeft <= 0) return;
            // ⚠ 过热**不**中断已扣过弹药的连射：弹药在 TryShoot 已经一次性扣掉，把剩下的吞掉等于"付了钱不发货"；
            //   过热只挡住下一次扣扳机（HandleShoot 开头那条）
            _burstTimer -= TickTime.RawFloat;
            if (_burstTimer > 0f) return;

            if (_burstMuzzle != null) ShootFromMuzzle(_burstMuzzle, 1);
            else for (int u = 0; u < WeaponManyMuzzles.Count; ++u) ShootFromMuzzle(WeaponManyMuzzles[u], 1);

            // 首轮的枪声由 HandleShoot 播，后续每轮各补一枪；噪声仍按"一次扣扳机一条"算，不重复
            if (!UseContinuousShootSound) PlaySFX(ShootSfx);
            ApplyPerShotCost();

            if (--_burstRoundsLeft > 0) _burstTimer = BurstInterval;
        }

        /// <summary>远端复刻的连发排队：首颗即时、其余按同一 <see cref="BurstInterval"/> 补发（纯表现）。
        /// <para>▍为什么不走网络：弹丸数/间隔都在**本地这份武器实例**上（改装配置同源）⇒ 可本地验算，不必上网。</para></summary>
        private void UpdateVisualBurst()
        {
            if (_visualBurstLeft <= 0) return;
            _visualBurstTimer -= TickTime.RawFloat;
            if (_visualBurstTimer > 0f) return;

            if (_visualBurstMuzzle != null) SpawnVisualBullet(_visualBurstMuzzle, default, _visualBurstEndPoint);
            else for (int u = 0; u < WeaponManyMuzzles.Count; ++u) SpawnVisualBullet(WeaponManyMuzzles[u], default, _visualBurstEndPoint);

            if (--_visualBurstLeft > 0) _visualBurstTimer = BurstInterval;
        }

        /// <summary>取消进行中的连射（换枪、收起、销毁时调用；本机真弹与远端表现弹一起掐）</summary>
        protected void CancelBurst()
        {
            _burstRoundsLeft = 0;
            _burstTimer = 0f;
            _burstMuzzle = null;

            _visualBurstLeft = 0;
            _visualBurstTimer = 0f;
            _visualBurstMuzzle = null;
            _visualBurstEndPoint = default;
        }

        /// <summary>收起/销毁即取消连射，避免把上一把枪没打完的弹丸补到下一把枪上</summary>
        protected virtual void OnDisable()
        {
            CancelBurst();
        }

        /// <summary>
        /// 从单个发射点位发射一轮子弹(含枪口闪光)
        /// </summary>
        void ShootFromMuzzle(Transform muzzle, int bulletsPerShotFinal)
        {
            // 非权威端只打表现弹（见 IsShotVisualOnly）。枪口闪光/音效/噪声/热量仍由 HandleShoot 照常走，
            // 只是把"真弹"换成"表现弹"；落点预示（SpawnLandingMarker）是本机给玩家看的预测，这里不生成。
            if (IsShotVisualOnly)
            {
                for (int i = 0; i < bulletsPerShotFinal; ++i)
                {
                    var visualBullet = SpawnVisualBullet(muzzle, GetShotDirectionWithinSpread(muzzle));
                    // 激光/蓄力/锁定这类武器靠 OnBulletShoot 配置弹体（GetLaserBullet 还会登记给 EndLaser 回收）
                    // ⇒ 表现弹也要发一次，否则装置在非权威端只剩一颗普通子弹（如 Dec_ColossusTower）。
                    // ⚠ 顺序与真弹路径略有差别：SpawnVisualBullet 内部已经 Shoot 过，这里只补配置。
                    if (visualBullet != null) OnBulletShoot?.Invoke(visualBullet);
                }
                ShootFlash(muzzle);
                return;
            }

            // 生成所有方向随机的子弹
            for (int i = 0; i < bulletsPerShotFinal; ++i)
            {
                Vector3 shotDirection = GetShotDirectionWithinSpread(muzzle);
                Vector3 shotPos = muzzle.position;
                if (AttrFinal(Attr.BulletsOffect) > 0)
                {
                    // ★ 播种过同样走确定性流（否则"多管齐射的枪口偏移"两端也不同）
                    var rng = Rng;
                    Vector2 point = (rng != null ? rng.InsideUnitCircle() : RandomUtils.InsideUnitCircle())
                        * AttrFinal(Attr.BulletsOffect).RawFloat;
                    shotPos = muzzle.TransformPoint(point);
                }
                var bullet = FPSGame.Core.VfxPool.Creat(CurrentDamgeData.BulletPrefab, shotPos, Quaternion.LookRotation(shotDirection));

                // ★ 必须在"往外传"之前截住 null：伤害配置漏配 BulletPrefab 时 VfxPool 返回 null，
                //   再往下走 OnBulletShoot(null) 会让订阅方(GetChargeBullet/GetLaserBullet)直接 NRE，
                //   异常穿出 LogicTick ⇒ 整个逻辑帧剩下的单位都不再 tick（不是单纯少一发子弹）
                if (bullet == null) { WarnBulletPrefabMissing(); continue; }

                OnBulletShoot?.Invoke(bullet);
                bullet.Shoot(this, UseDamageIndex, muzzle);

                // 每个弹丸都预测地形落点并生成落点预示(仅配置了 LandingPrefab 的伤害配置生效)
                SpawnLandingMarker(shotPos, shotDirection);
            }

            ShootFlash(muzzle);
        }

        /// <summary>子弹预制体漏配只报一次：连发武器每发都报会把 Console 刷爆（首条已足以定位是哪个预制体的哪一档伤害）。</summary>
        private void WarnBulletPrefabMissing()
        {
            if (_noBulletPrefabWarned) return;
            _noBulletPrefabWarned = true;
            Debug.LogError($"{name} 的 DamageData 没配子弹预制体：Damages[{UseDamageIndex}].BulletPrefab 为空 ⇒ " +
                           $"本次开火不产生子弹（后续也不再生效），请在该武器预制体上补上 BulletPrefab", this);
        }

        protected void ShootFlash(Transform point)
        {
            // 枪口闪光
            if (MuzzleFlashPrefab != null)
            {
                GameObject muzzleFlashInstance = FPSGame.Core.VfxPool.Creat(MuzzleFlashPrefab, point.position, point.rotation, UnparentMuzzleFlash? null:point.transform);
                muzzleFlashInstance.transform.localScale = Vector3.one * FlashSizeScale;
                //为闪光指定第一人称图层
                TransformUtils.SetChildLayer(muzzleFlashInstance.gameObject, point.gameObject.layer, true);

            }
        }

        /// <summary>
        /// 开火时在预测的地面落点创建落点预示物体(<see cref="DamageData.LandingPrefab"/>)，物体按预计飞行时间自动清理，
        /// 旋转对齐落点表面法线
        /// </summary>
        private void SpawnLandingMarker(Vector3 origin, Vector3 direction)
        {
            if (Damages == null || Damages.Count == 0 || UseDamageIndex >= Damages.Count) return;
            var data = Damages[UseDamageIndex];
            if (data == null || data.LandingPrefab == null) return;

            if (!TryPredictLanding(origin, direction, out var landPos, out var normal, out float flightTime)) return;

            // 物体前向(+Z)对齐落点表面法线(即物体"朝上"立在落点)
            //Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, normal);
            Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);
            var marker = FPSGame.Core.VfxPool.Creat(data.LandingPrefab, landPos, rotation);
            if (!marker) return;

            // 短暂存在：生命周期=预计飞行时间(+少许余量)，超时后由 VFXManager 池自动回收
            var ll = marker.GetComponent<LimitedLife>();
            if (ll == null) ll = marker.AddComponent<LimitedLife>();
            ll.ResetLift(Mathf.Max(flightTime, 0.1f) + 0.2f);
        }

        /// <summary>
        /// 粗粒度模拟当前伤害配置的子弹弹道(速度/重力/继承武器速度)，用物理检测 GroundLayers 求落点。
        /// 敌人/单位层不参与拦截：超出极限射程或生命周期仍未命中地面则判定无落点。
        /// </summary>
        private bool TryPredictLanding(Vector3 origin, Vector3 direction,
            out Vector3 landPos, out Vector3 normal, out float flightTime)
        {
            landPos = default;
            normal = Vector3.up;
            flightTime = 0f;

            float speed = CurrentSpeed;
            if (speed <= 0f) return false;
            float gravity = CurrentGravity;
            Vector3 inherit = CurrentDamgeData.InheritWeaponSpeed ? MuzzleWorldVelocity : Vector3.zero;
            float maxDist = CurrentWeaponRange;
            float maxLife = Mathf.Max(CurrentDamgeData.MaxLifeTime, 0.01f);
            const float kStep = 0.05f;   // 弹道模拟步长(s)，落点预示精度要求不高
            const float kMaxSeg = 2f;    // 单次射线检测最大长度(m)，防高速大步长穿透薄地面

            // 起点已压着地面层(枪口贴地)直接视为落地
            if (Physics.CheckSphere(origin, 0.1f, LayerDefinition.GroundLayers, QueryTriggerInteraction.Collide))
            {
                landPos = origin;
                return true;
            }

            Vector3 pos = origin;
            Vector3 vel = direction.normalized * speed;
            float t = 0f;

            while (t < maxLife)
            {
                float dt = Mathf.Min(kStep, maxLife - t);
                vel += Vector3.down * gravity * dt;
                Vector3 move = (vel + inherit) * dt;
                Vector3 end = pos + move;
                float total = move.magnitude;

                if (total > 1e-4f)
                {
                    Vector3 moveDir = move / total;
                    float covered = 0f;
                    // 段内细分射线，避免单个大步长跨过薄地面
                    while (covered < total)
                    {
                        float seg = Mathf.Min(total - covered, kMaxSeg);
                        if (Physics.Raycast(pos + moveDir * covered, moveDir, out var hit, seg,
                                LayerDefinition.GroundLayers, QueryTriggerInteraction.Collide))
                        {
                            landPos = hit.point;
                            normal = hit.normal;
                            if ((landPos - origin).magnitude > maxDist) return false; // 打空(超过极限射程)

                            float local = covered + hit.distance;
                            flightTime = (t - dt) + dt * (local / total);
                            return true;
                        }
                        covered += seg;
                    }
                }

                // 超过极限射程仍未落地 => 打空，不生成落点
                if ((end - origin).magnitude >= maxDist) return false;

                pos = end;
                t += dt;
            }
            return false;
        }


        /// <summary>
        /// 本武器的**确定性随机种子**（0 = 不指定 ⇒ 沿用 <c>UnityEngine.Random</c> / 全局流的旧行为）。
        ///
        /// <para>▍谁来播种：**"各端各自执行一次"的东西**才需要 —— 目前是战备（轨道轰炸 / 飞鹰 / 运输机）在
        /// <c>VFXAirdropEffect</c> 里生成的那把武器（见那里调用的 <c>SeedUtil</c> 派生）。
        /// 不播种的后果：同一发轨道轰炸，两端的弹道散布/落点各摇各的 ⇒ 看起来"根本没同步"
        /// （2026-10-07 用户实测）。</para>
        /// </summary>
        public int RandomSeed;

        private System.Random _rng;
        private bool _rngBuilt;

        /// <summary>本武器的随机流：播种过 ⇒ 确定性 <see cref="System.Random"/>；没播种 ⇒ null（调用方走旧路径）。</summary>
        private System.Random Rng
        {
            get
            {
                if (!_rngBuilt)
                {
                    _rngBuilt = true;
                    _rng = RandomSeed != 0 ? new System.Random(RandomSeed) : null;
                }
                return _rng;
            }
        }

        /// <summary>球内随机方向（与 <c>UnityEngine.Random.insideUnitSphere</c> 同用途，但可走确定性流）。</summary>
        private static Vector3 InsideUnitSphere(System.Random rng)
        {
            var c = rng.InsideUnitCircle();
            float y = (float)(rng.NextDouble() * 2.0 - 1.0);
            return new Vector3(c.x, y, c.y);
        }

        /// <summary>
        /// 设置武器散布
        /// </summary>
        /// <param name="shootTransform"></param>
        /// <returns></returns>
        public virtual Vector3 GetShotDirectionWithinSpread(Transform shootTransform)
        {
            var bsa = CurrentSpreadAngle;
            if (bsa == 0) return shootTransform.forward;
            PEInt spreadAngleRatio = bsa / 180;
            //从方向向球面随机方向移动spreadAngleRatio;
            // ★ 播种过就走本武器的确定性流（战备两端各自执行也要落在同一片区域）
            var rng = Rng;
            Vector3 spreadWorldDirection = Vector3.Slerp(shootTransform.forward,
                rng != null ? InsideUnitSphere(rng) : UnityEngine.Random.insideUnitSphere,
                spreadAngleRatio.RawFloat);

            return spreadWorldDirection;
        }



        /// <summary>
        /// 强制进行射击
        /// </summary>
        /// <returns></returns>
        public void Shoot()
        {
            HandleShoot();
        }
        #endregion

        #region 热量系统

        /// <summary>
        /// 更新热量散热逻辑
        /// </summary>
        private void UpdateHeatSystem()
        {
            if (IsOverheated)
            {
                // 过热期间热量线性归零，时间到才解除过热
                _overheatTimer -= Time.deltaTime;
                if (_overheatTimer <= 0f)
                {
                    _currentHeat = 0f;
                    IsOverheated = false;
                }
                else
                {
                    var totalDuration = AttrFinal(Attr.OverheatDuration).RawFloat;
                    if (totalDuration > 0f)
                    {
                        _currentHeat = MaxHeat * (_overheatTimer / totalDuration);
                    }
                }
            }
            else if (_coolTimer > 0f)
            {
                _coolTimer -= Time.deltaTime;
            }
            else if (_currentHeat > 0f)
            {
                _currentHeat -= AttrFinal(Attr.CoolSpeed).RawFloat * Time.deltaTime;
                if (_currentHeat <= 0f)
                {
                    _currentHeat = 0f;
                }
            }
        }

        #endregion
    }

}