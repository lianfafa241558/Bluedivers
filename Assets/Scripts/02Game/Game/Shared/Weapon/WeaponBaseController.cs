using System;
using System.Collections.Generic;
using Core;
using GameContract;
using PEMaths;
using Unity.BaseTool;
using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.Game {
    using Attr = WeaponAttrType;

    [Flags]
    public enum BulletFlag
    {

        /// <summary>穿透单位</summary>
        [CustomLabel("穿透单位")]
        PenetrateUnits = 1 << 0,
        /// <summary>穿透地形</summary>
        [CustomLabel("穿透地形")]
        PenetrateTerrain = 1 << 1,
        /// <summary>命中保留</summary>
        [CustomLabel("命中保留")]
        RetainOnHit = 1 << 2,
        /// <summary>享受敌人强化</summary>
        [CustomLabel("享受敌人强化")]
        EnemyIntensify = 1 << 3,
        /// <summary>允许伤害自身(也没用了)</summary>
        [CustomLabel("允许伤害自身(也没用了)")]
        AttackOneself = 1 << 4,
        //Everything = -1,
    }


    [System.Serializable]
    /// <summary>
    /// 伤害配置
    /// </summary>
    public class DamageData
    {


        [Header("运动")]
        [CustomLabel("投射物的速度")]
        public float Speed = 20f;
        /// <summary>下坠速度</summary>
        [CustomLabel("下坠速度")]
        public float Gravity = 0f;
        /// <summary>继承武器初速度</summary>
        [CustomLabel("是否继承武器初速度")]
        public bool InheritWeaponSpeed = false;
        [CustomLabel("生命周期")]
        public float MaxLifeTime = 5f;
        [CustomLabel("自爆引信(单位:M)")]
        public float MaxRange = -1;
        [CustomLabel("安全引信(单位:M)")]
        public float MinRange = -1;
        [CustomLabel("无源伤害")]
        public bool NoSource = false;

        [Header("直击伤害")]
        [CustomLabel("直击伤害值")]
        public float DamageDirect;
        [CustomLabel("伤害成分")]
        public List<KVP<DamageTypeEnum, float>> DamageGroupDirect = new() { new(DamageTypeEnum.Gun, 1), new(DamageTypeEnum.Destruction, 1) };

        [Header("爆炸伤害")]
        [CustomLabel("爆炸伤害值")]
        public float DamageExplosion;
        [CustomLabel("伤害范围", "DamageExplosion", 0, CompareOperate.Greater)]
        public float ExplosionRange = 0;
        [SerializeField]
        [CustomLabel("伤害衰减", "DamageExplosion", 0, CompareOperate.Greater)]
        private AnimationCurve DamageRatioOverDis;
        [CustomLabel("伤害成分")]
        public List<KVP<DamageTypeEnum, float>> DamageGroupExplosion = new() { new(DamageTypeEnum.Explosion, 1), new(DamageTypeEnum.Destruction, 1) };


        [Header("碰撞")]
        [CustomLabel("特效使用碰撞点的朝向")]
        public bool UseCollisionDirection = true;
        [CustomLabel("特效沿法线偏移量")]
        public float ImpactVfxSpawnOffset = 0.1f;

        [CustomLabel("碰撞特效")]
        public GameObject ImpactVfx;
        [CustomLabel("碰撞音效")]
        public AudioClip ImpactSfx;
        [CustomLabel("创建弹坑")]
        public bool UseHole;
        [CustomLabel("弹坑/不填使用默认")]
        public GameObject Hole;

        [Header("蓄力")]
        [SerializeField]
        [CustomLabel("满蓄伤害倍率")]
        private float ChargeDamageScale = 1;
        [SerializeField]
        [CustomLabel("满蓄溅射范围倍率")]
        private float ChargeAOERangeScale = 1;

        [CustomLabel("满蓄子弹速度")]
        public float ChargeSpeedScale = 1;
        [CustomLabel("满蓄子弹重力")]
        public float ChargeGravityScale = 1;

        public PEInt GetExplosionDamage(float ChargeScale, PEInt distance)
        {
            PEInt range = GetExplosionRange(ChargeScale);
            PEInt scale = 0;
            if (distance < range) scale = (PEInt)DamageRatioOverDis.Evaluate((distance / range).RawFloat);
            return (PEInt)(DamageExplosion * Mathf.Lerp(1, ChargeDamageScale.PreventZero(), ChargeScale)) * scale;
        }
        
        public PEInt GetExplosionRange(float ChargeScale) => (PEInt)(ExplosionRange * Mathf.Lerp(1, ChargeAOERangeScale.PreventZero(), ChargeScale));

        public PEInt GetDirectDamage(float ChargeScale) => (PEInt)(DamageDirect * Mathf.Lerp(1, ChargeDamageScale.PreventZero(), ChargeScale));


        #region 设置
        public float GetAttr(Attr type)
        {
            return type switch {
                Attr.DirectDamage => DamageDirect,
                Attr.ExplosionDamage => DamageExplosion,
                Attr.ExplosionRange => ExplosionRange,
                Attr.DirectDestruction => DamageGroupDirect.GetValue(DamageTypeEnum.Destruction),
                Attr.ExplosionDestruction => DamageGroupExplosion.GetValue(DamageTypeEnum.Destruction),
                Attr.BulletSpeed => Speed,
                Attr.ChargeDamageScale => ChargeDamageScale,
                Attr.ChargeExplosionRangeScale => ChargeAOERangeScale,
                Attr.DirectWeakness => DamageGroupDirect.GetValue(DamageTypeEnum.Weakness),
                Attr.ExplosionWeakness => DamageGroupExplosion.GetValue(DamageTypeEnum.Weakness),
                _ =>0,
            };
        }

        public void SetDamageDirect(PEInt value)=> DamageDirect = value.RawFloat;
        public void SetDamageExplosion(PEInt value) => DamageExplosion = value.RawFloat;
        public void SetExplosionRange(PEInt value) => ExplosionRange = value.RawFloat;
        public void SetDirectDestruction(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Destruction, value.RawFloat);
        public void SetExplosionDestruction(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Destruction, value.RawFloat);

        public void SetSpeed(PEInt value) => Speed = value.RawFloat;

        public void SetChargeDamageScale(PEInt value) => ChargeDamageScale = value.RawFloat;

        public void SetChargeExplosionRangeScale(PEInt value) => ChargeAOERangeScale = value.RawFloat;

        public void SetDirectWeakness(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Weakness, value.RawFloat);
        public void SetExplosionWeakness(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Weakness, value.RawFloat);


        #endregion
    }


    /// <summary>
    /// 基类，很单纯，只完成射击效果
    /// </summary>
    public class WeaponBaseController : LogicBehaviour
    {
        public event UnityAction<ProjectileBase> OnBulletShoot;
        public event UnityAction<WeaponBaseController> OnShoot;
        public event UnityAction<WeaponBaseController,bool> OnWantShootChange;


        #region 点位
        [Foldout("点位和信息", true)]
        [CustomLabel("武器根对象")]
        public GameObject WeaponRoot;

        [CustomLabel("发射点位")]
        public Transform WeaponMuzzle;

        [CustomLabel("发射点位2")]
        public Transform WeaponMuzzle2;

        #endregion
        [Foldout("武器参数", true)]
        public WeaponCfg cfg = new();
        #region 子弹参数

        [Foldout("子弹参数", true)]

        [CustomLabel("子弹预制件")]
        public ProjectileBase ProjectilePrefab;
        [CustomLabel("子弹标旗")]
        public BulletFlag BulletFlag;

        public List<DamageData> Damages;
        
        public int UseDamageIndex = 0;
        public DamageData CurrentDamgeData => Damages[UseDamageIndex];

        #endregion

        #region 特效
        [Foldout("特效和动画", true)]

        [CustomLabel("枪口闪光的预制")]
        public GameObject MuzzleFlashPrefab;

        /// <summary>闪光不附着于枪口</summary>
        [CustomLabel("闪光不附着于枪口")]
        public bool UnparentMuzzleFlash;

        [CustomLabel("音效范围")]
        public float SFXRange=20;
        [CustomLabel("射击音效", "UseContinuousShootSound", 0, CompareOperate.Equal)]
        public AudioClip ShootSfx;

        /// <summary>使用连续射击音效</summary>
        [SerializeField]
        [CustomLabel("使用连续射击音效")]
        protected bool UseContinuousShootSound = false;
        /// <summary>连续射击初始音效</summary>
        [SerializeField]
        [CustomLabel("连续射击初始音效", "UseContinuousShootSound",1,CompareOperate.Equal)]
        protected AudioClip ContinuousShootStartSfx;
        /// <summary>连续射击持续音效</summary>
        [SerializeField]
        [CustomLabel("连续射击持续音效", "UseContinuousShootSound", 1, CompareOperate.Equal)]
        protected AudioClip ContinuousShootLoopSfx;
        /// <summary>连续射击结束音效</summary>
        [SerializeField]
        [CustomLabel("连续射击结束音效", "UseContinuousShootSound", 1, CompareOperate.Equal)]
        protected AudioClip ContinuousShootEndSfx;

        protected AudioSource m_ContinuousShootAudioSource = null;

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

        public virtual float WeaponChargeScale_D => 1;
        public virtual float CurrentGravity => CurrentDamgeData.Gravity;
        public virtual float CurrentSpeed => CurrentDamgeData.Speed;

        public virtual float CurrentLife => CurrentDamgeData.MaxLifeTime;
        /// <summary>当前子弹的极限射程(包括爆炸范围)</summary>
        public virtual float CurrentWeaponExtremeRange => (CurrentDamgeData.MaxRange == -1 ? CurrentDamgeData.MaxLifeTime * CurrentDamgeData.Speed : CurrentDamgeData.MaxRange)+ CurrentDamgeData.ExplosionRange;

        /// <summary>当前子弹的正常射程(不包括爆炸范围)</summary>
        public virtual float CurrentWeaponRange => (CurrentDamgeData.MaxRange == -1 ? CurrentDamgeData.MaxLifeTime * CurrentDamgeData.Speed : CurrentDamgeData.MaxRange);


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

        private Transform[] Muzzles;
        private int shootCount=0;

        public override void LogicInit()
        {
            if (!WeaponMuzzle)
            {
                Debug.LogError("错误:未配置武器开火点", gameObject);
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

            InitAttribute();

            m_LastMuzzlePosition = WeaponRoot.transform.position;
            if (UseContinuousShootSound)
            {
                var audios = m_ContinuousShootAudioSource = gameObject.AddComponent<AudioSource>();
                audios.playOnAwake = false;
                audios.clip = ContinuousShootLoopSfx;
                audios.outputAudioMixerGroup = AudioManager.GetMixGroup(AudioGroups.Weapon);
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
            if (sfx.IsValid())return AudioManager.PlaySound(new(sfx, transform.position, SFXRange, AudioGroups.Weapon));
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

            //发射数量，如果以后做了蓄力会更多弹丸数量再说
            int bulletsPerShotFinal = AttrFinal(Attr.BulletsPerShot,1).RawInt;

            Transform muzzle = WeaponMuzzle= Muzzles[shootCount++ % Muzzles.Length];

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
                var bullet = VFXManager.Creat(ProjectilePrefab, shotPos, Quaternion.LookRotation(shotDirection));

                OnBulletShoot?.Invoke(bullet);
                bullet.Shoot(this);
            }

            ShootFlash(muzzle);

            if (!UseContinuousShootSound)
            {
                PlaySFX(ShootSfx);
            }
            OnShoot?.Invoke(this);
        }

        protected void ShootFlash(Transform point)
        {
            // 枪口闪光
            if (MuzzleFlashPrefab != null)
            {
                GameObject muzzleFlashInstance = VFXManager.Creat(MuzzleFlashPrefab, point.position, point.rotation, UnparentMuzzleFlash? null:point.transform);
                //为闪光指定第一人称图层
                TransformUtils.SetChildLayer(muzzleFlashInstance.gameObject, point.gameObject.layer, true);

            }
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
    }

}