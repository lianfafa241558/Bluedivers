using FPSGame.Attribute;
using PEMaths;
using UnityEngine;
using Utils;

namespace Unity.FPS.Game
{
    using Attr = WeaponAttrType;

    /// <summary>
    /// 在武器的基础上，添加了换弹的功能
    /// </summary>
    public class WeaponReloadController : WeaponBaseController
    {


        #region 属性
        /// <summary>弹匣弹量</summary>
        public GameCurrentAttribute Magazine;
        /// <summary>射击间隔</summary>
        public GameCurrentAttribute ShootInterval;
        /// <summary>后备弹量</summary>
        public GameCurrentAttribute Ammo;
        /// <summary>手动装弹时间</summary>
        public GameCurrentAttribute ReloadTime;

        #endregion

        #region 帮助属性
        /// <summary>允许射击(冷却完成)</summary>
        public bool AllowShoot => ShootInterval.ScaleValue >= 1;
        /// <summary>可以射击(子弹足够)</summary>
        protected bool CanShoot => InfiniteMagazine || Magazine.CurrValue >= ShootCost;

        /// <summary>子弹耗尽</summary>
        public bool Exhausted => Magazine.CurrValue == 0 && Ammo.CurrValue == 0;

        /// <summary>无限后备子弹</summary>
        public bool InfiniteAmmo => Ammo.FinalValue < 0;
        /// <summary>无限弹匣子弹</summary>
        public bool InfiniteMagazine => Magazine.FinalValue < 0;

        [SerializeField]
        protected int ShootCost = 1;
        #endregion

        public bool IsReloading { get; protected set; }
        [DisplayField]
        [SerializeField]
        private int showAmmo;
        [DisplayField]
        [SerializeField]
        private bool showInReload;
        [DisplayField]
        [SerializeField]
        private float showReloadTime;
        [DisplayField]
        [SerializeField]
        private float showCool;


        public override void LogicInit()
        {
            base.LogicInit();
            if (!TrySet(Attr.Magazine,out Magazine, true)) Debug.LogError(gameObject + "没有设置弹匣弹量", gameObject);
            if (!TrySet(Attr.ShootInterval, out ShootInterval)) Debug.LogError(gameObject + "没有设置射击间隔", gameObject);
            if (!TrySet(Attr.Ammo, out Ammo,true)) Debug.LogError(gameObject + "没有设置后备弹量", gameObject);
            if (!TrySet(Attr.ReloadTime, out ReloadTime, true)) Debug.LogError(gameObject + "没有设置装弹时间", gameObject);

            ShootInterval.CurrValue -= AttrFinal(Attr.StartCool);
        }

        public override void LogicUnInit()
        {

        }

        protected bool TrySet(Attr type, out GameCurrentAttribute attr, bool autoCreat=false)
        {
            attr = cfg[type] as GameCurrentAttribute;
            if (autoCreat && !attr.IsValid())
            {
                attr = cfg.Add(type, 0) as GameCurrentAttribute;
            }
            return attr.IsValid();
        }

        public override void LogicTick()
        {
            base.LogicTick();
            ShootInterval.CurrValue += TickTime;
            if (IsReloading)
            {
                ReloadTime.CurrValue += TickTime;
                if (ReloadTime.ScaleValue >= 1)
                {
                    ReloadEnd();
                }
            }
            showCool = ShootInterval.CurrValue.RawFloat;
            showAmmo = Magazine.CurrValue.RawInt;
            showInReload = IsReloading;
            showReloadTime = ReloadTime.CurrValue.RawFloat;
        }


        /// <summary>
        /// 尝试(非充能)射击
        /// </summary>
        protected virtual bool TryShoot()
        {
            if (!AllowShoot) return false;
            if (IsOverheated) return false;
            if (CanShoot)
            {
                HandleShoot();
                UseMagazine(ShootCost);
                ResetInterval();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 无限弹匣的武器不会尝试换弹(自动阻止无限弹匣武器)
        /// </summary>
        /// <returns>是否消耗成功</returns>
        protected virtual bool UseMagazine(int count)
        {
            if (!InfiniteMagazine)//需要消耗子弹
            {
                if (count > Magazine.CurrValue)//不够的从备弹取
                {
                    count -= Magazine.CurrValue.RawInt;//自动约束
                    Magazine.CurrValue = 0;
                    Ammo.CurrValue -= count;//自动约束
                }
                else
                {
                    Magazine.CurrValue -= count;//自动约束
                }

                if (Magazine.CurrValue <= 0) TryManualReload();
                return true;
            }
            return false;
        }


        /// <summary>
        /// 尝试手动换弹
        /// </summary>
        public virtual void TryManualReload()
        {
            //有子弹来保证换弹
            if (InfiniteAmmo || Ammo.CurrValue > 0)
            {
                ReloadStart();
            }
        }

        protected virtual void ReloadStart()
        {
            ReloadTime.CurrValue = 0;
            IsReloading = true;
        }

        /// <summary>
        /// 完成换弹
        /// </summary>
        protected virtual void ReloadEnd()
        {
            if (InfiniteAmmo)//无限子弹直接满上
            {
                Magazine.CurrValue = Magazine.FinalValue;
            }
            else//扣除后备子弹
            {
                var oldAmmo = Magazine.CurrValue;
                var newAmmo = PEMath.Min(Ammo.CurrValue, Magazine.FinalValue);
                Magazine.CurrValue = newAmmo;
                Ammo.CurrValue -= newAmmo - oldAmmo;
            }
            IsReloading = false;
        }

        /// <summary>
        /// 重置射击间隔等属性
        /// </summary>
        protected virtual void ResetInterval()
        {
            ShootInterval.CurrValue = 0;
        }

    }
}