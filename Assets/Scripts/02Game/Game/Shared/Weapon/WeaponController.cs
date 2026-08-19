using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using FPSGame.Attribute;
using GameContract;
using PEMaths;

using UnityEngine;
using UnityEngine.Events;
using Utils;
using static UnityEngine.Rendering.DebugUI;
namespace Unity.FPS.Game
{
    using Attr = WeaponAttrType;

    public enum WeaponShootType
    {
        /// <summary>半自动</summary>
        [InspectorName("半自动")]
        Manual,
        /// <summary>全自动</summary>
        [InspectorName("全自动")]
        Automatic,
        /// <summary>充能</summary>
        [InspectorName("充能")]
        Charge,
        /// <summary>激光</summary>
        [InspectorName("激光")]
        Laser,
        /// <summary>锁定</summary>
        [InspectorName("锁定")]
        Lock,
    }

    [Flags]//flag不可以定义为0的，会和1<<0冲突
    public enum WeaponFlag
    {
        /// <summary>自动换弹</summary>
        [InspectorName("自动换弹")]
        AutomaticReload = 1 << 0,
        /// <summary>禁止手动换弹</summary>
        [InspectorName("禁止手动换弹")]
        NoManualReload = 1 << 1,
        /// <summary>子弹耗尽死亡 敌人专用</summary>
        [InspectorName("子弹耗尽死亡 敌人专用")]
        AutoDeath = 1 << 2,
        /// <summary>强制连射：开火后松手不停，直到弹匣打空(无限弹匣则一直打)</summary>
        [InspectorName("强制连射(打空弹匣才停)")]
        ForceShoot = 1 << 3,
    }

    public abstract partial class WeaponController : WeaponReloadController
    {
        /// <summary>锁定/解除锁定一个敌人时</summary>
        public UnityAction<I_Actor, bool> OnLockUpdate;
        /// <summary>充能百分比变化时</summary>
        public UnityAction<float> OnChargeRatioUpdate;
        /// <summary>需要更新ui</summary>
        public UnityAction OnUIUpdate;


        //public event UnityAction OnHit;
        public event UnityAction<bool> OnCharget;

        public event UnityAction<bool> OnLock;


        /*
        [ContextMenu("转移参数")]
        void SetPara()
        {
            TrySet(WeaponAttrType.Ammo, ClipSize);
            TrySet(WeaponAttrType.Magazine, MaxAmmo);
            TrySet(WeaponAttrType.ShootInterval, DelayBetweenShots);
            TrySet(WeaponAttrType.Catapult, 0);
            if(BulletsPerShot!=1) TrySet(WeaponAttrType.BulletsPerShot, BulletsPerShot);
            TrySet(WeaponAttrType.BulletsSpreadAngle, BulletSpreadAngle);
            if (!WeaponFlag.HasFlag(WeaponFlag.NoManualReload))
            {
                TrySet(WeaponAttrType.ReloadTime, AmmoReloadDelay);
            }
            
            if (WeaponFlag.HasFlag(WeaponFlag.AutomaticReload))
            {
                TrySet(WeaponAttrType.AutoReloadTime, AmmoReloadDelay);
                TrySet(WeaponAttrType.AutoReloadSpeed, AmmoReloadRate);
            }

            switch (ShootType)
            {
                case WeaponShootType.Manual:
                    break;
                case WeaponShootType.Automatic:
                    break;
                case WeaponShootType.Charge:
                    TrySet(WeaponAttrType.ChargeLowestStage, ChargeLowestStage);
                    TrySet(WeaponAttrType.ChargeHigheststage, Chargestage);
                    TrySet(WeaponAttrType.ChargeAmmoOnLowest, AmmoUsedOnStartCharge);
                    TrySet(WeaponAttrType.ChargeAmmoOnHighest, AmmoUsageRateWhileCharging);
                    TrySet(WeaponAttrType.ChargeDuration, MaxChargeDuration);
                    break;
                case WeaponShootType.Laser:
                    TrySet(WeaponAttrType.LaserWaitTime, WaitTime);
                    break;
                case WeaponShootType.Lock:

                    TrySet(WeaponAttrType.LockDistance, LockDistance);
                    TrySet(WeaponAttrType.LockRange, LockRange);
                    TrySet(WeaponAttrType.LockLayers, LockLayers);
                    TrySet(WeaponAttrType.LockPerCount, ActorMaxLockCount);
                    TrySet(WeaponAttrType.LockInterval, LockInterval);
                    break;
            }
            
           

            void TrySet(WeaponAttrType type,float value)
            {
                if (value == 0) return;
                cfg.SetPara(type, value);
            }
        }
        */

        #region 参数
        [Foldout("武器参数", true)]

        [InspectorName("射击类型")]
        public WeaponShootType ShootType;
        [InspectorName("武器标旗")]
        public WeaponFlag WeaponFlag;

        [Space]
        /// <summary>满蓄自动射击</summary>
        [InspectorName("满蓄自动射击")]
        [Compare("ShootType", (int)WeaponShootType.Charge, CompareOperate.Equal)]
        public bool AutomaticReleaseOnCharged;
        [Space]
        /// <summary>锁定形状</summary>
        [InspectorName("锁定形状")]
        [Compare("ShootType", (int)WeaponShootType.Lock, CompareOperate.Equal)]
        public ShapeType Lockshape = ShapeType.Circle;

        #endregion


        #region 特效

        [Foldout("特效和动画", true)]

        [InspectorName("换弹音效")]
        public AudioClip ReloadSfx;

        [InspectorName("切换此武器的音效")]
        public AudioClip ChangeWeaponSfx;
        [InspectorName("蓄力特效")]
        [Compare( "ShootType", (int)WeaponShootType.Charge, CompareOperate.Equal)]
        public ChargeView ChargeVfx;

        /// <summary>
        /// 目前仅返回装弹的trigger OnReload 和激光/连射/蓄力状态的bool IsActive
        /// </summary>
        [Space]
        [InspectorName("武器动画")]
        [Tooltip("目前仅返回装弹的trigger OnReload 和激光/连射/蓄力状态的bool IsActive")]
        public Animator WeaponAnimator;
        #endregion

        #region 其他属性


        ///<summary>正在瞄准</summary>
        protected bool m_InAiming { get; set; }

        ///<summary>正在连射</summary>
        public bool InShoots { get; private set; }

        ///<summary>正在发射激光</summary>
        public bool InLasering { get; private set; }

        ///<summary>正在蓄力</summary>
        public bool InCharging { get; private set; }

        ///<summary>正在锁定</summary>
        public bool InLock { get; private set; }
        /// <summary>正在散热/自动换弹</summary>
        public bool InCooling { get; private set; }

        public bool IsWeaponActive { get; private set; }



        /// <summary>上次隐藏此武器的时间</summary>
        protected float m_LastTimeHide;

        /// <summary>每次锁定的目标</summary>
        public List<I_Actor> WeaponLockTarget { get; private set; } = new();

        protected ChargeView m_ChargeVfx;

        #endregion

        #region 武器属性
        /// <summary>自动装弹延迟</summary>
        public GameCurrentAttribute AutoReloadTime;

        /// <summary>蓄力时间</summary>
        public GameCurrentAttribute ChargeDuration;

        /// <summary>锁定冷却</summary>
        public GameCurrentAttribute LockInterval;
        #endregion
        #region 帮助属性

        /// <summary>正在自动恢复子弹</summary>
        public bool InAutoReload => WeaponFlag.HasFlag(WeaponFlag.AutomaticReload) && AutoReloadTime.ScaleValue >= 1;

        /// <summary> 子弹剩余百分比</summary></summary>
        public PEInt CurrentTotalAmmoRatio
        {
            get
            {
                if (InfiniteAmmo || InfiniteMagazine) return 1;
                if (Ammo.FinalValue == 0 && Magazine.FinalValue == 0) return 0;
                return CurrentTotalAmmo / TotalAmmo;
            }
        }
        /// <summary>子弹总量</summary>
        public PEInt TotalAmmo => PEMath.Max(Ammo.FinalValue, 0) + PEMath.Max(Magazine.FinalValue, 0);

        /// <summary>剩余子弹总量</summary>
        public PEInt CurrentTotalAmmo => PEMath.Max(Ammo.CurrValue, 0) + PEMath.Max(Magazine.CurrValue, 0);

        /// <summary>当前蓄力比例(离散值)(0-1但按阶段取值)</summary></summary>
        public override PEInt WeaponChargeScale_D=> new(AttrFinal(Attr.ChargeHigheststage) == default ? 1 : (ChargeDuration?.StageScale.RawFloat??1));

        #endregion
        #region 生命周期

        public override void LogicInit()
        {
            base.LogicInit();

            if (HasFlag(WeaponFlag.AutomaticReload)&&!TrySet(Attr.AutoReloadTime, out AutoReloadTime,true)) Debug.LogError(gameObject + "没有设置自动装弹时间", gameObject);

            switch (ShootType)
            {
                case WeaponShootType.Laser:
                    OnBulletShoot += GetLaserBullet;
                    break;
                case WeaponShootType.Charge:
                    OnBulletShoot += GetChargeBullet;
                    OnChargeRatioUpdate?.Invoke(0);
                    if (!TrySet(Attr.ChargeDuration, out ChargeDuration)) Debug.LogError(gameObject + "没有设置蓄力时间", gameObject);
                    //ShootCost = Get(Attr.ChargeAmmoOnLowest).RawInt;//重置为最低消耗
                    break;
                case WeaponShootType.Lock:
                    WeaponLockTarget.Clear();
                    OnBulletShoot += GetLockBullet;
                    if (!TrySet(Attr.LockInterval, out LockInterval)) Debug.LogError(gameObject + "没有设置锁定冷却");
                    break;
            }

            OnUIUpdate?.Invoke();
        }

        public override void LogicTick()
        {
            base.LogicTick();

            UpdateAmmo();
            switch (ShootType)
            {
                case WeaponShootType.Charge:
                    UpdateCharge();//防止鼠标切出屏幕打断
                    break;
                case WeaponShootType.Laser:
                    UpdateLaser();//防止鼠标切出屏幕打断
                    break;
                case WeaponShootType.Lock:
                    UpdateLock();//防止鼠标切出屏幕打断
                    break;
            }
        }

        public override void LogicUnInit()
        {
            switch (ShootType)
            {
                case WeaponShootType.Laser:
                    OnBulletShoot -= GetLaserBullet;
                    break;
                case WeaponShootType.Charge:
                    OnBulletShoot -= GetChargeBullet;
                    break;
                case WeaponShootType.Lock:
                    OnBulletShoot -= GetLockBullet;
                    break;
            }
            base.LogicUnInit();
        }

        #endregion

        #region 换弹

        /// <summary>
        /// 完成换弹
        /// </summary>
        protected override void ReloadEnd()
        {
            base.ReloadEnd();
            OnUIUpdate?.Invoke();
        }


        protected override void ReloadStart()
        {
            base.ReloadStart();
            InShoots = false;
            PlaySFX(ReloadSfx);
            SetTrigger(Constants.k_AnimOnReloadParameter, true);
            OnUIUpdate?.Invoke();
        }

        /// <summary>
        /// 尝试手动换弹
        /// </summary>
        public override void TryManualReload()
        {
            //禁止自动装弹的武器无法换弹
            if (WeaponFlag.HasFlag(WeaponFlag.NoManualReload)) return;
            base.TryManualReload();
        }

        void UpdateAmmo()
        {

            if (HasFlag(WeaponFlag.AutomaticReload)) AutoReloadTime.CurrValue += TickTime;
            var speed = AttrFinal(Attr.AutoReloadSpeed);
            //自动恢复子弹
            if (InAutoReload && Magazine.ScaleValue < 1 &&
                (AutoReloadTime.FinalValue ==0|| (!InCharging && !InLock && !InLasering)))
            {
                if (Ammo.CurrValue > 0 || InfiniteAmmo) {
                    if (speed > 99)
                    {
                        // 立即装填
                        ReloadEnd();
                    }
                    else
                    {
                        // 随着时间的推移重新装填武器
                        Magazine.CurrValue += speed * TickTime;
                        Ammo.CurrValue -= speed * TickTime;
                    }
                }
                InCooling = true;
            }
            else
            {
                InCooling = false;
            }


        }

        /// <summary>
        /// 重置射击间隔等属性
        /// </summary>
        protected override void ResetInterval()
        {
            base.ResetInterval();
            if(HasFlag(WeaponFlag.AutomaticReload)) AutoReloadTime.CurrValue = 0;
        }

        #endregion

        #region 蓄力

        //还没做好左右切换
        /// <summary>
        /// 尝试开始蓄力武器蓄力
        /// </summary>
        bool TryBeginCharge()
        {
            
            ShootCost = AttrFinal(Attr.ChargeAmmoOnLowest).RawInt;//重置为最低消耗
            if (!InCharging && CanShoot && AllowShoot)
            {
                InCharging = true;
                SetBool(Constants.k_AnimIsActiveParameter, true);
                ChargeDuration.CurrValue = 0;
                OnCharget?.Invoke(true);
                var muzzle = GetMuzzle(0);
                if (ChargeVfx && muzzle) m_ChargeVfx = VFXManager.Creat(ChargeVfx.gameObject, muzzle.position, muzzle.rotation, muzzle).GetComponent<ChargeView>();
                return true;
            }
            else
            {
                //蓄力失败
                if(UseContinuousShootSound)ShotSFX(ContinuousShootEndSfx);
                return false;
            }
        }


        void UpdateCharge()
        {
            if (InCharging)
            {
                if (ChargeDuration.ScaleValue < 1)
                {
                    ChargeDuration.CurrValue += TickTime;
                    if (m_ChargeVfx) m_ChargeVfx.UpdateCharget(ChargeDuration.ScaleValue.RawFloat);

                    OnChargeRatioUpdate?.Invoke(ChargeDuration.StageScale.RawFloat);
                }
                else if (AutomaticReleaseOnCharged)
                {
                    TryReleaseCharge();
                }
            }
        }

        /// <summary>
        /// 尝试蓄力武器射击
        /// </summary>
        bool TryReleaseCharge()
        {
            if (InCharging)
            {
                //蓄力不够，取消
                //StageScale为-1时会发生问题(总是返回0),所以在这里也使用scale
                if (ChargeDuration.ScaleValue * AttrFinal(Attr.ChargeHigheststage, 1) < AttrFinal(Attr.ChargeLowestStage, 1))
                {
                    PlaySFX(ContinuousShootEndSfx);
                    EndCharge();//蓄力不足取消
                }
                else
                {
                    HandleShoot();
                    //Debug.LogError("开始射击" + gameObject + "蓄力最大值" + ChargeDuration.FinalValue + "当前值" + ChargeDuration.CurrValue + " 系数" + ChargeDuration.ScaleValue, gameObject);

                    //Debug.LogError("开始射击" + gameObject + "蓄力" + ChargeDuration.StageScale +"连续值" + ChargeDuration.CurrValue , gameObject);

                    UseMagazine(PEMath.Lerp(AttrFinal(Attr.ChargeAmmoOnLowest), AttrFinal(Attr.ChargeAmmoOnHighest), ChargeDuration.StageScale).RawInt);
                    EndCharge();//射击完毕
                }
            }
            return false;
        }


        protected virtual void EndCharge()
        {
            OnChargeRatioUpdate?.Invoke(0);
            ChargeDuration.CurrValue = 0;
            ResetInterval();
            OnCharget?.Invoke(false);
            InCharging = false;
            SetBool(Constants.k_AnimIsActiveParameter, false);
            if (m_ChargeVfx)VFXManager.Release(m_ChargeVfx.gameObject);
            m_ChargeVfx = null;
        }

        void GetChargeBullet(ProjectileBase projectile)
        {
            if(projectile.TryGetComponent<ChargeView>(out var cv))
            {
                cv.UpdateCharget(ChargeDuration.ScaleValue.RawFloat);
            }
        }
        #endregion


        #region 激光武器

        /// <summary>
        /// 尝试开始发射激光
        /// </summary>
        bool TryBeginLaser()
        {
            if (AllowShoot && CanShoot)
            {
                SetBool(Constants.k_AnimIsActiveParameter,true);
                if(AttrFinal(Attr.LaserWaitTime)!=0) ShootInterval.CurrValue = -AttrFinal(Attr.LaserWaitTime);
                return true;
            }
            return false;
        }

        bool TryUpdateLaser()
        {
            if (!InLasering)
            { 
                if (AllowShoot && CanShoot)
                {
                    //Debug.LogError("激光创建自杀");
                    m_LastProjectiles.Clear();//开启新一轮激光，重置记录
                    HandleShoot();
                    OnUIUpdate?.Invoke();
                    InLasering = true;
                    return true;
                }
            }
            return false;
        }
        void UpdateLaser()
        {
            if (InLasering && CanShoot &&AllowShoot)
            {
                ResetInterval();
                UseMagazine(ShootCost);
                if (Magazine.CurrValue <= 0)
                {
                    EndLaser();//子弹耗尽
                }
            }
        }

        protected virtual void EndLaser()
        {
            InLasering = false;
            SetBool(Constants.k_AnimIsActiveParameter, false);
            // 齐射时可能有多个激光子弹，全部释放
            foreach (var projectile in m_LastProjectiles)
            {
                if (projectile) projectile.Release();
            }
            m_LastProjectiles.Clear();
        }

        protected List<ProjectileBase> m_LastProjectiles = new();
        void GetLaserBullet(ProjectileBase projectile)
        {
            m_LastProjectiles.Add(projectile);
        }

        #endregion

        #region 锁定武器


        public bool GetLockActor(out I_Actor actor)
        {
            TargetCfg targetCfg = new() { actorState = ActorState.Normal, targetType = UnitTypeEnum.Enemy };
            // 获取屏幕中心点（屏幕坐标）
            Vector3 screenCenter = Camera.main.ViewportToScreenPoint(new(0.5f,0.5f,0));
            
            bool ActorInLockRange(I_Actor actor)
            {
                if (Vector3.Distance(actor.CenterPos, transform.position) >= AttrFinal( WeaponAttrType.LockDistance).RawFloat) return false;
                Vector3 Spos = Camera.main.WorldToScreenPoint(actor.CenterPos);
                float range = AttrFinal(WeaponAttrType.LockRange).RawFloat;
                switch (Lockshape)
                {
                    case ShapeType.Circle:
                        if (Vector3.Distance(Spos, screenCenter) > range) return false;
                        break;
                    case ShapeType.Rectangle:
                        if (Mathf.Abs(Spos.x-screenCenter.x) > range || Mathf.Abs(Spos.y - screenCenter.y) > range) return false;
                        break;
                    default:
                        Debug.LogError("错误的锁定形状" + Lockshape);
                        return false;
                }
                return WeaponLockTarget.Count(item=>item==actor)<AttrFinal(WeaponAttrType.LockPerCount);
            }

            var list = BattleManager.Instance.FindUnits(targetCfg, ActorInLockRange);

            actor = list.OrderBy((actor)=> Vector3.Distance(Camera.main.WorldToScreenPoint(actor.CenterPos), screenCenter)).FirstOrDefault();
            return actor!=null;
        }

 

        /// <summary>
        /// 尝试开始锁定
        /// </summary>
        bool TryBeginLock()
        {
            //总是返回false,因为没有实际开始射击
            if (!InLock)
            {
                if(AttrFinal(Attr.LaserWaitTime)!=0) LockInterval.CurrValue = -AttrFinal(Attr.LaserWaitTime);
            }
            return false;
        }

        /// <summary>
        /// 尝试开始锁定
        /// </summary>
        bool TryUpdateBeginLock()
        {
            //总是返回false,因为没有实际开始射击
            if (!InLock)
            {
                LockInterval.CurrValue += TickTime;
                if (CanShoot && AllowShoot&& LockInterval.ScaleValue>=1)
                {
                    LockInterval.CurrValue = 0;
                    InLock = true;
                    OnLock?.Invoke(true);
                }
            }
            return false;
        }


        void UpdateLock()
        {
            if (InLock)
            {

                LockInterval.CurrValue += TickTime;
                if (LockInterval.ScaleValue >= 1)
                {
                    
                    LockInterval.CurrValue -= LockInterval.FinalValue;
                    for (int i = WeaponLockTarget.Count - 1; i >= 0; --i)
                    {
                        if (!WeaponLockTarget[i].IsValid())
                        {
                            WeaponLockTarget.RemoveAt(i);
                        }
                        else if (WeaponLockTarget[i].ActorState == ActorState.Dead)
                        {
                            OnLockUpdate?.Invoke(WeaponLockTarget[i], false);
                            WeaponLockTarget.RemoveAt(i);
                        }
                    }

                    if (WeaponLockTarget.Count < PEMath.Min(AttrFinal(Attr.LockLayers), Magazine.CurrValue))//没超过锁定上限
                    {
                        if (GetLockActor(out var target))
                        {
                            WeaponLockTarget.Add(target);
                            OnLockUpdate?.Invoke(target, true);
                        }

                    }
                }
                
            }
            else if (WeaponLockTarget.Count > 0 && TryShoot())//完成攻速冷却
            {
                
            }
        }


        /// <summary>
        /// 尝试锁定武器开始射击
        /// </summary>
        bool TryReleaseLock()
        {
            if (InLock)
            {
                InLock = false;
                
                OnLock?.Invoke(false);
                return true;
            }
            else TryShoot();
            return false;
        }



        void GetLockBullet(ProjectileBase projectile)
        {
            while (WeaponLockTarget.Count > 0)
            {
                var tar = WeaponLockTarget[WeaponLockTarget.Count - 1];
                WeaponLockTarget.RemoveAt(WeaponLockTarget.Count - 1);
                //跳过死亡单位
                if (!tar.IsValid() || tar.ActorState == ActorState.Dead) continue;
                if (projectile is Gameplay.ProjectileLockStandard lockPro)
                {
                    lockPro.SetTarget(tar.AimPoint);
                    break;
                }
                
            }

        }

        protected virtual void EndLock()
        {
            WeaponLockTarget.Clear();
            InLock = false;
        }
        #endregion


        #region 射击


        protected override bool UseMagazine(int count)
        {
            if (base.UseMagazine(count))
            {
                OnUIUpdate?.Invoke();
                return true;
            }
            return false;
        }


        /// <summary>
        /// 设置武器状态
        /// </summary>
        /// <param name="show"></param>
        public void ShowWeapon(bool show)
        {
          
            WeaponRoot.SetActive(show);
            if (show)
            {
                if (gameObject.activeInHierarchy)
                {
                    PlaySFX(ChangeWeaponSfx);
                }

            }
            else
            {
                Animator anim = GetComponent<Animator>();
                if (anim&&gameObject.activeInHierarchy) anim.Play("Idle");
                IsReloading = false;
                WantsToShoot = false;
                //PlaySFX(ContinuousShootEndSfx);
                if (m_Initialized)
                {
                    switch (ShootType)
                    {
                        case WeaponShootType.Manual:
                            break;
                        case WeaponShootType.Automatic:
                            break;
                        case WeaponShootType.Charge:
                            EndCharge();//武器隐藏
                            break;
                        case WeaponShootType.Laser:
                            EndLaser();//武器隐藏
                            break;
                        case WeaponShootType.Lock:
                            EndLock();//武器隐藏
                            break;
                    }
                }
                m_LastTimeHide = Time.time;
            }
            OnUIUpdate?.Invoke();

            IsWeaponActive = show;
        }


        /// <summary>
        /// 输入射击
        /// </summary>
        public virtual bool HandleShootInputs(bool inputDown, bool inputHeld, bool inputUp)
        {
            // 强制连射：开火后松手不停，直到弹匣打空(无限弹匣则一直打)
            if (HasFlag(WeaponFlag.ForceShoot))
            {
                // 弹匣已打空(不足以再打一发)，结束强制连射
                if ((InShoots || InLasering) && !CanShoot)
                {
                    if (InLasering) EndLaser();
                    else
                    {
                        InShoots = false;
                        SetBool(Constants.k_AnimIsActiveParameter, false);
                    }
                }
                // 连射中且仍有子弹：忽略松手输入，视同继续按住扳机
                else if (inputUp && (InShoots || InLasering) && CanShoot)
                {
                    inputUp = false;
                    inputHeld = true;
                }
            }

            switch (ShootType)
            {
                case WeaponShootType.Manual:
                    return inputDown && TryShoot();

                case WeaponShootType.Automatic:
                    if (InShoots && inputUp)
                    {
                        InShoots = false;
                        SetBool(Constants.k_AnimIsActiveParameter, false);
                    }
                    if (inputDown && !InShoots && CanShoot && AllowShoot)
                    {
                        //想搞二连击得动画就是两段，不然只会连着放两次动画
                        //敌人类型会一直返回true/true/false
                        if(AttrFinal(Attr.LaserWaitTime)!=0) ShootInterval.CurrValue = -AttrFinal(Attr.LaserWaitTime);
                        InShoots = true;
                        SetBool(Constants.k_AnimIsActiveParameter, true);
                    }
                    WantsToShoot = InShoots && CanShoot;
                    return InShoots && TryShoot();

                case WeaponShootType.Charge:
                    WantsToShoot = inputHeld && InCharging;

                    //鼠标抬起或者满蓄自动发射
                    if (inputUp)
                    {
                        TryReleaseCharge();//总是false
                        return false;
                    }
                    //这里开始出现了分歧点，敌人我们希望开始蓄力的时候就播放动画
                    //而玩家武器我们希望释放的时候才true
                    //或者我们改一下，用事件过
                    //只有开始的时候有一次true
                    if (inputDown) return TryBeginCharge();

                    return false;

                case WeaponShootType.Laser:
                    WantsToShoot = inputHeld && InLasering;

                    if (inputUp) EndLaser();//鼠标抬起
                    else if (inputHeld) return TryUpdateLaser();
                    else if (inputDown) return TryBeginLaser();
                    return false;

                case WeaponShootType.Lock:
                    WantsToShoot = inputHeld && InLock;

                    if (inputUp) return TryReleaseLock();
                    else if (inputHeld) return TryUpdateBeginLock();
                    else if (inputDown) return TryBeginLock();

                    return false;

                default:
                    return false;
            }
        }


        /// <summary>
        /// 进行射击
        /// </summary>
        /// <returns></returns>
        protected override void HandleShoot()
        {
            base.HandleShoot();
            /*
            // Trigger attack animation if there is any
            if (WeaponAnimator)
            {
                WeaponAnimator.SetTrigger(Constants.k_AnimAttackParameter);
            }
            */
            
        }

  
           
        /// <summary>
        /// 设置武器散布
        /// </summary>
        /// <param name="shootTransform"></param>
        /// <returns></returns>
        public override Vector3 GetShotDirectionWithinSpread(Transform shootTransform)
        {
            var bsa = AttrFinal(WeaponAttrType.BulletsSpreadAngle);
            if (bsa == 0) return shootTransform.forward;
            PEInt spreadAngleRatio = bsa / 180 * (PEInt)(m_InAiming ? 0.3f : 1);
            //从方向向球面随机方向移动spreadAngleRatio;
            Vector3 spreadWorldDirection = Vector3.Slerp(shootTransform.forward, UnityEngine.Random.insideUnitSphere,
                spreadAngleRatio.RawFloat);

            return spreadWorldDirection;
        }

        #endregion


        public bool HasFlag(WeaponFlag flag) => WeaponFlag.HasFlag(flag);


        public void SetTrigger(int name,bool state)
        {
            var anim = WeaponAnimator;
            if (!anim) return;
            if (state)
            {
                anim.SetTrigger(name);
            }
            else
            {
                anim.ResetTrigger(name);
            }
        }
        public void SetBool(int name, bool state)
        {
            var anim = WeaponAnimator;
            if (!anim) return;
            anim.SetBool(name, state);
        }

    }



}