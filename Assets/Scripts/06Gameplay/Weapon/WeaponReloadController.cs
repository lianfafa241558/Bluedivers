using FPSGame.Attributes;
using PEMaths;
using UnityEngine;
using UnityEngine.Events;
using FPSGame.Utils;

namespace FPSGame.Weapon
{
    using Attr = WeaponAttrType;

    /// <summary>
    /// 在武器的基础上，添加了换弹的功能
    /// </summary>
    [AddComponentMenu("武器/换弹控制")]
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

        #endregion

        /// <summary>换弹状态翻转时抛（true=开始 / false=结束）。⚠ 只在**真正翻转**时抛：
        /// 收起武器（<c>ShowWeapon(false)</c>）也会经 setter 转成 false，订阅方据此还原移速惩罚。</summary>
        public event UnityAction<WeaponBaseController, bool> OnReloadStateChange;

        /// <summary>
        /// 【联机】读当前弹量交给对端（<c>&lt;0</c> = 该格无限 / 未带 ⇒ 对端不要动）。
        /// <para>▍用途：地面武器 / 背包武器是**每端各一份实例**，弹药只在持有者那端变化 ⇒
        /// 在"交接"（拾取 / 卸下）时把权威值带过去，另一端的副本才不会永远是满的。</para>
        /// <para>⚠ 口径与 <c>VehicleWeaponsManager.TryGetAmmo</c> 一致（无限的那格给 -1）。</para>
        /// </summary>
        public void GetAmmoForSync(out int magazine, out int ammo)
        {
            magazine = -1;
            ammo = -1;
            // 刚实例化那一帧 LogicInit 还没跑（属性未建立）⇒ 两个 -1 = "本端没有权威值"
            if (Magazine == null || Ammo == null) return;

            if (!InfiniteMagazine) magazine = Magazine.CurrValue.RawInt;
            if (!InfiniteAmmo) ammo = Ammo.CurrValue.RawInt;
        }

        /// <summary>【联机】把对端交过来的权威弹量写进本端这一份（<c>&lt;0</c> = 该项不动）。
        /// <para>⚠ <c>GameCurrentAttribute</c> 的 setter 自带上下限约束，这里不要再夹一次。</para></summary>
        public void ApplyAmmoForSync(int magazine, int ammo)
        {
            if (Magazine == null || Ammo == null) return;

            if (magazine >= 0 && !InfiniteMagazine) Magazine.CurrValue = magazine;
            if (ammo >= 0 && !InfiniteAmmo) Ammo.CurrValue = ammo;
        }

        private bool _isReloading;
        public bool IsReloading
        {
            get => _isReloading;
            protected set
            {
                if (_isReloading == value) return;
                _isReloading = value;
                OnReloadStateChange?.Invoke(this, value);
            }
        }
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

        /// <summary>
        /// 立即完成装弹（全队强化"补给大师"等外部效果调用）
        /// </summary>
        public virtual void ReloadComplete()
        {
            // 无限弹匣无需装弹
            if (InfiniteMagazine) return;
            // 没有后备弹则无法装弹
            if (!InfiniteAmmo && Ammo.CurrValue <= 0) return;
            // 弹匣已满无需装弹
            if (Magazine.CurrValue >= Magazine.FinalValue) return;

            ReloadEnd();
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
                // 只补"弹匣空出来的那部分"。旧写法把后备弹量当成了换弹后的弹匣总弹量，
                // 弹匣没打空时 newAmmo < oldAmmo ⇒ Ammo -= 负数 = **反向加弹**，
                // 于是"三连发"(ShootCost=3) 在弹匣2/后备1时 2/1 → 1/2 → 2/1 无限来回换弹。
                var space = PEMath.Max(Magazine.FinalValue - Magazine.CurrValue, 0);
                var take = PEMath.Min(Ammo.CurrValue, space);
                Magazine.CurrValue += take;
                Ammo.CurrValue -= take;
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
