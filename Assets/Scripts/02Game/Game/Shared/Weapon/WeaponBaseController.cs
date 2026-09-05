using System;
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using GameContract;
using PEMaths;

using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.Game {
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
        #region 子弹参数

        [Foldout("子弹参数", true)]

        //[InspectorName("子弹预制体")]
        //public ProjectileBase ProjectilePrefab;
        [InspectorName("子弹标旗")]
        public BulletFlag BulletFlag;

        public List<DamageData> Damages;
        
        public int UseDamageIndex = 0;
        public DamageData CurrentDamgeData => Damages[UseDamageIndex];

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


        public GameObject Owner{ get; set; }

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

        /// <summary>每次射击消耗的弹药量(开启齐射时自动按发射点位数量调整)</summary>
        [SerializeField]
        protected int ShootCost = 1;

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

            // 齐射:开火时所有点位一起开火,并按点位数量自动调整一次消耗的弹药量
            if (UseManyMuzzle)
            {
                ShootCost = WeaponManyMuzzles.Count;
            }
            else if (WeaponManyMuzzles.Count > 0)
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

        }

        #region 音效
        protected AudioSource PlaySFX(AudioClip sfx)
        {
            //Debug.LogError("播放"+ sfx,gameObject);
            if (sfx.IsValid())return AudioSvc.PlaySound(new(sfx, transform.position, SFXRange, AudioGroups.Weapon));
            return null;
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
            if (sfx.IsValid()) m_ContinuousShootAudioSource.PlayOneShot(sfx);
        }

        /// <summary>更新持续射击的音效</summary>
        void UpdateContinuousShootSound()
        {
            if (UseContinuousShootSound)
            {
                //showAudioState = m_ContinuousShootAudioSource.isPlaying;
                //敌人都是按着左键不撒手的
                if (WantsToShoot/* && Get(Attr.Magazine, AttrValueEnum.Curr) >= 1*/)
                {
                    if (!m_ContinuousShootAudioSource.isPlaying)
                    {
                        ShotSFX(ContinuousShootStartSfx);
                        //if (m_ContinuousShootEndSource.IsValid() && m_ContinuousShootEndSource.clip == ContinuousShootEndSfx) m_ContinuousShootEndSource.Stop();
                        //m_ContinuousShootStartSource =PlaySFX(ContinuousShootStartSfx);
                        m_ContinuousShootAudioSource.Play();
                    }
                }
                else if (m_ContinuousShootAudioSource.isPlaying)
                {
                    ShotSFX(ContinuousShootEndSfx);
                    //if (m_ContinuousShootStartSource.IsValid() && m_ContinuousShootStartSource.clip == ContinuousShootStartSfx) m_ContinuousShootStartSource.Stop();
                    //m_ContinuousShootEndSource =PlaySFX(ContinuousShootEndSfx);
                    m_ContinuousShootAudioSource.Stop();
                }
            }
        }
        #endregion

        #region 射击


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

            if (UseManyMuzzle)
            {
                // 齐射:所有发射点位一起开火
                for (int u = 0; u < WeaponManyMuzzles.Count; ++u)
                {
                    ShootFromMuzzle(WeaponManyMuzzles[u], bulletsPerShotFinal);
                }
            }
            else
            {
                // 交替射击
                Transform muzzle = WeaponMuzzle = Muzzles[shootCount++ % Muzzles.Length];
                ShootFromMuzzle(muzzle, bulletsPerShotFinal);
            }

            if (!UseContinuousShootSound)
            {
                PlaySFX(ShootSfx);
            }
            OnShoot?.Invoke(this);

            // 热量系统：每次射击增加热量，蓄力武器受蓄力热量倍率影响
            var heatPerShot = AttrFinal(Attr.HeatPerShot).RawFloat;
            if (heatPerShot > 0f)
            {
                var chargeHeatScale = PEMath.Lerp(1, AttrFinal(Attr.ChargeHeatScale, 1), WeaponChargeScale_D);
                _currentHeat += heatPerShot * chargeHeatScale.RawFloat;
                _coolTimer = AttrFinal(Attr.CoolDelay).RawFloat;
                if (_currentHeat >= 100f)
                {
                    _currentHeat = 100f;
                    IsOverheated = true;
                    _overheatTimer = AttrFinal(Attr.OverheatDuration).RawFloat;
                }
            }
        }

        /// <summary>
        /// 从单个发射点位发射一轮子弹(含枪口闪光)
        /// </summary>
        void ShootFromMuzzle(Transform muzzle, int bulletsPerShotFinal)
        {
            // 生成所有方向随机的子弹
            for (int i = 0; i < bulletsPerShotFinal; ++i)
            {
                Vector3 shotDirection = GetShotDirectionWithinSpread(muzzle);
                Vector3 shotPos = muzzle.position;
                if (AttrFinal(Attr.BulletsOffect) > 0)
                {
                    Vector2 point = RandomUtils.InsideUnitCircle() * AttrFinal(Attr.BulletsOffect).RawFloat;
                    shotPos = muzzle.TransformPoint(point);
                }
                var bullet = VFXManager.Creat(CurrentDamgeData.BulletPrefab, shotPos, Quaternion.LookRotation(shotDirection));

                OnBulletShoot?.Invoke(bullet);
                bullet.Shoot(this, UseDamageIndex, muzzle);

                // 每个弹丸都预测地形落点并生成落点预示(仅配置了 LandingPrefab 的伤害配置生效)
                SpawnLandingMarker(shotPos, shotDirection);
            }

            ShootFlash(muzzle);
        }

        protected void ShootFlash(Transform point)
        {
            // 枪口闪光
            if (MuzzleFlashPrefab != null)
            {
                GameObject muzzleFlashInstance = VFXManager.Creat(MuzzleFlashPrefab, point.position, point.rotation, UnparentMuzzleFlash? null:point.transform);
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
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, normal);
            var marker = VFXManager.Creat(data.LandingPrefab, landPos, rotation);
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
        /// 设置武器散布
        /// </summary>
        /// <param name="shootTransform"></param>
        /// <returns></returns>
        public virtual Vector3 GetShotDirectionWithinSpread(Transform shootTransform)
        {
            var bsa = AttrFinal(Attr.BulletsSpreadAngle);
            if (bsa == 0) return shootTransform.forward;
            PEInt spreadAngleRatio = bsa / 180;
            //从方向向球面随机方向移动spreadAngleRatio;
            Vector3 spreadWorldDirection = Vector3.Slerp(shootTransform.forward, UnityEngine.Random.insideUnitSphere,
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
                        _currentHeat = 100f * (_overheatTimer / totalDuration);
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