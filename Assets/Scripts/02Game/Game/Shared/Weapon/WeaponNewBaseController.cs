using System;
using System.Collections.Generic;
using Core;
using PEMaths;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.Game {


    /// <summary>
    /// 基类，不考虑换弹等效果
    /// </summary>
    public class WeaponNewBaseController : MonoBehaviour
    {

        #region 点位
        [Foldout("点位和信息", true)]
        [CustomLabel("武器根对象")]
        public GameObject WeaponRoot;

        [CustomLabel("发射点位")]
        public Transform WeaponMuzzle;

        #endregion

        #region 参数

        [Foldout("武器参数", true)]
        public WeaponCfg cfg=new();

        //public DisplayDic

        [Foldout("武器参数", true)]
        
        [CustomLabel("射击间隔/射速")]
        /// <summary>射击间隔/射速</summary>
        public float DelayBetweenShots = 0.5f;

        [CustomLabel("弹丸数量")]
        public int BulletsPerShot = 1;

        [CustomLabel("子弹散布角度(0=无散布)")]
        public float BulletSpreadAngle = 0f;

        [CustomLabel("弹匣容量")]
        /// <summary>弹匣容量</summary>
        public int MaxAmmo = 8;

        #endregion

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
        [CustomLabel("射击音效")]
        public AudioClip ShootSfx;

        /// <summary>使用连续射击音效</summary>
        [SerializeField]
        [CustomLabel("使用连续射击音效")]
        protected bool UseContinuousShootSound = false;
        /// <summary>连续射击初始音效</summary>
        [SerializeField]
        [CustomLabel("连续射击初始音效", "UseContinuousShootSound")]
        protected AudioClip ContinuousShootStartSfx;
        /// <summary>连续射击持续音效</summary>
        [SerializeField]
        [CustomLabel("连续射击持续音效", "UseContinuousShootSound")]
        protected AudioClip ContinuousShootLoopSfx;
        /// <summary>连续射击结束音效</summary>
        [SerializeField]
        [CustomLabel("连续射击结束音效", "UseContinuousShootSound")]
        protected AudioClip ContinuousShootEndSfx;

        protected AudioSource m_ContinuousShootAudioSource = null;

        #endregion

        #region 测试

        [Foldout("特殊", true)]
        [DisplayField(true, false, false)]
        [CustomLabel("剩余当前弹药")]
        [SerializeField]
        protected float nowAmmo ;
        /// <summary>剩余后备弹量</summary>
        [CustomLabel("剩余后备弹量")]
        [DisplayField(true, false, false)]
        [SerializeField]
        protected float remainBullets;

        #endregion

        public event UnityAction<ProjectileBase> OnBulletShoot;

        public GameObject Owner{ get; set; }
        public GameObject SourcePrefab { get; set; }//这个不知道干什么用的
        public float m_LastTimeShot { get; set; } = Mathf.NegativeInfinity;
        public virtual float WeaponChargeScale_D => 1;
        public virtual float CurrentGravity => CurrentDamgeData.Gravity;
        public virtual float CurrentSpeed => CurrentDamgeData.Speed;

        public virtual float CurrentLife => CurrentDamgeData.MaxLifeTime;
        public float WeaponRange => (CurrentDamgeData.MaxRange == -1 ? CurrentDamgeData.MaxLifeTime * CurrentDamgeData.Speed : CurrentDamgeData.MaxRange)+CurrentDamgeData.ExplosionRange;

        protected float m_InitTime;
        protected bool m_Initialized;
        /// <summary>
        /// 想要射击：帧检测，如果松开就false
        /// </summary>
        protected bool m_WantsToShoot { get; set; }

        public Vector3 MuzzleWorldVelocity { get; private set; }//武器世界速度
        Vector3 m_LastMuzzlePosition;//上一帧武器所在位置(计算速度)

        protected virtual void Start()
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
            nowAmmo = MaxAmmo;
            m_LastMuzzlePosition = WeaponRoot.transform.position;
            if (UseContinuousShootSound)
            {
                var audios = m_ContinuousShootAudioSource = gameObject.AddComponent<AudioSource>();
                audios.playOnAwake = false;
                audios.clip = ContinuousShootLoopSfx;
                audios.outputAudioMixerGroup = AudioManager.GetMixGroup(AudioGroups.Weapon);
                audios.loop = true;
            }
            m_Initialized = true;
            m_InitTime = Time.time;
        }

        protected virtual void Update()
        {
            UpdateContinuousShootSound();
            if (Time.deltaTime > 0)//计算武器移动速度
            {
                MuzzleWorldVelocity = (WeaponRoot.transform.position - m_LastMuzzlePosition) / Time.deltaTime;
                m_LastMuzzlePosition = WeaponRoot.transform.position;
            }
        }

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
                //敌人都是按着左键不撒手的
                if (m_WantsToShoot && nowAmmo >= 1f)
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

        //private AudioSource m_ContinuousShootStartSource, m_ContinuousShootEndSource;

        #region 射击

        protected ProjectileBase lastProjectile;
        /// <summary>
        /// 进行射击(只有一个枪口)
        /// </summary>
        /// <returns></returns>
        protected virtual void HandleShoot()
        {

            //发射数量，如果以后做了蓄力会更多弹丸数量再说
            int bulletsPerShotFinal = BulletsPerShot;

            Transform muzzle = WeaponMuzzle;

            // 生成所有方向随机的子弹
            for (int i = 0; i < bulletsPerShotFinal; ++i)
            {
                Vector3 shotDirection = GetShotDirectionWithinSpread(muzzle);

                lastProjectile = Instantiate(ProjectilePrefab, muzzle.position, Quaternion.LookRotation(shotDirection));
                OnBulletShoot?.Invoke(lastProjectile);
                //lastProjectile.Shoot(this);
            }

            ShootFlash(muzzle);

            m_LastTimeShot = Time.time;

            if (!UseContinuousShootSound)
            {
                PlaySFX(ShootSfx);
            }

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
            if (BulletSpreadAngle == 0) return shootTransform.forward;
            float spreadAngleRatio = BulletSpreadAngle / 180f;
            //从方向向球面随机方向移动spreadAngleRatio;
            Vector3 spreadWorldDirection = Vector3.Slerp(shootTransform.forward, UnityEngine.Random.insideUnitSphere,
                spreadAngleRatio);

            return spreadWorldDirection;
        }

        #endregion


        [ContextMenu("重置参数")]
        protected virtual void Reset()
        {
            cfg.Reset(WeaponShootType.Automatic, default);

        }
    }

}