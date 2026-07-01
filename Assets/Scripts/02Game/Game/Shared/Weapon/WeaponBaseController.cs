using System;
using System.Collections.Generic;
using Core;
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
        public event UnityAction<ProjectileBase> OnBulletShoot;
        public event UnityAction<WeaponBaseController> OnShoot;
        public event UnityAction<WeaponBaseController,bool> OnWantShootChange;


        #region 点位
        [Foldout("点位和信息", true)]
        [InspectorName("武器根对象")]
        public GameObject WeaponRoot;

        [InspectorName("发射点位")]
        public Transform WeaponMuzzle;

        [InspectorName("发射点位2")]
        public Transform WeaponMuzzle2;

        #endregion
        [Foldout("武器参数", true)]
        public WeaponCfg cfg = new();
        #region 子弹参数

        [Foldout("子弹参数", true)]

        [InspectorName("子弹预制体")]
        public ProjectileBase ProjectilePrefab;
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