using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Attributes;
using PEMaths;

using UnityEngine;
using FPSGame.Gameplay;
using FPSGame.GameData;

namespace FPSGame.Weapon
{
    using Attr = WeaponAttrType;
    using Random = UnityEngine.Random;

    /// <summary>
    /// 玩家武器的开火、换弹与升级逻辑。
    /// </summary>
    [AddComponentMenu("武器/玩家武器控制")]
    public partial class WeaponPlayerController : WeaponController
    {
        [Foldout("点位和信息", true)]
        [DisplayField]
        [InspectorName("所属玩家")]
        public int PlayerIndex;

        [InspectorName("武器名称")]
        public string WeaponName;
        [InspectorName("武器类型")]
        public string WeaponType;

        [InspectorName("武器类型枚举")]
        public WeaponTypeEnum WeaponTypeEnum;

        [SpritePreview(5,3)]
        [InspectorName("武器图标")]
        public Sprite WeaponIcon;


        [InspectorName("准星预制体")]
        public Animator Sight;


        [InspectorName("瞄准视野物体")]
        public GameObject ScopeGo;

        [InspectorName("左手")]
        public Transform LHand;
        [InspectorName("右手")]
        public Transform RHand;
        [InspectorName("展示点位")]
        public GameObject ShowRoot;



        [Foldout("武器参数", true)]
        [Space]
        [InspectorName("后坐力")]
        [Range(0f, 2f)]
        public float RecoilForce = 1;

        /// <summary>后坐力手感参数（每把玩家武器一份）：相机冲量/回正节奏 + 武器模型回弹 + 连射散布膨胀 + 屏震。
        /// <para>▍为什么只在玩家武器上：后坐力是"玩家手感"，AI/炮塔武器放它没有意义；
        /// 载具驾驶的炮塔另有一份（<see cref="VehicleWeaponsManager.RecoilProfile"/>）。</para></summary>
        [InspectorName("后坐力参数")]
        public WeaponRecoilProfile RecoilProfile = new();

        [InspectorName("瞄准时隐藏准星")]
        public bool AimingHideCrosshair = true;

        [InspectorName("瞄准时放大倍率")]
        [Range(0f, 1f)]
        public float AimZoomRatio = 1f;

        [InspectorName("瞄准时的偏移")]
        public Vector3 AimOffset;

        [InspectorName("装备时玩家模型旋转角度")]
        [Range(-90, 90)]
        public float playerAngle;


        public float ReloadSpeedScale { get; private set; } = 1;//属于一项升级，暂时没用


        /// <summary>后坐力倍率(属性，1=原值)：只吃改装加成，0 = 完全无后坐力</summary>
        public float RecoilScale => AttrFinal(Attr.Recoil, 1).RawFloat;

        /// <summary>后坐力最终值(基础 × 改装倍率)：**武器回弹与镜头抬头共用这一口径**。
        /// 瞄准减弱不在这里做 —— 那是 <see cref="WeaponRecoilProfile.AdsScale"/> 的职责（同一份参数只写一处）。</summary>
        public float RecoilForceFinal => RecoilForce * RecoilScale;

        // ---- 连射散布膨胀的参数来自本枪的 RecoilProfile（基类默认全关，只有这里覆写）----
        protected override float SpreadBloomPerShot => RecoilProfile.SpreadBloomPerShot;
        protected override float SpreadBloomMax => RecoilProfile.SpreadBloomMax;
        protected override float SpreadBloomDecay => RecoilProfile.SpreadBloomDecay;


        public override void LogicInit()
        {
            base.LogicInit();
            if (ScopeGo)
            {
                ScopeGo.SetActive(false);
            }
        }

        /// <summary>伤害类属性的"属性 → 当前 DamageData"订阅表（UnInitAttribute 整批退订用）。
        /// <para>⚠ 不能再按 <c>Damages[0].SetXxx</c> 去退订：切过子弹配置后绑的已经不是 0 号那条了。</para></summary>
        private readonly Dictionary<Attr, Action<PEInt>> _damageSetters = new();

        /// <summary>每条 DamageData 的属性原始值（<c>[属性][配置下标]</c>），切"子弹配置序号"时当基线用。
        /// <para>⚠ 必须备份：handler 会把 cfg 终值写回 DamageData，切回来再读就是"已被加成过的值"，
        /// 来回切模组会越乘越高。</para></summary>
        private readonly Dictionary<Attr, PEInt[]> _damagePrimeBackup = new();

        protected override void InitAttribute()
        {
            base.InitAttribute();

            // 后坐力倍率：与伤害配置无关，基础值恒为 1(不改行为)；
            // ⚠ 必须放在下面那个早退之前，装备槽的空伤害武器也有后坐力
            if (!cfg[Attr.Recoil]) cfg.Add(Attr.Recoil, 1);

            // ⚠ 伤害配置为空的武器（如角色**装备槽**的 *Armor：Damages.Count == 0）原来直接 Damages[0]
            //   ⇒ **IndexOutOfRangeException** 把整条 LogicInit 链打断：后面 `WeaponReloadController.LogicInit`
            //   的 Magazine/Ammo/ReloadTime 也建不出来 ⇒ 之后 `ApplyUpgrade` 里 `cfg[type]` 取到 null 再炸一次
            //   （联机 4045 载入配置时实测）。配置错误上面 `WeaponBaseController.LogicInit` 已报过，这里静默早退。
            if (Damages == null || Damages.Count == 0) return;

            _damagePrimeBackup.Clear();   // 武器重建后 Damages 换了，旧备份作废
            // prefab 里手填错序号时别让 CurrentDamgeData 越界（它就是 Damages[UseDamageIndex]）
            UseDamageIndex = Mathf.Clamp(UseDamageIndex, 0, Damages.Count - 1);
            var _cfg = cfg;

            // ⚠ setter 必须经 CurrentDamgeData 转发，不能捕获某个 DamageData 实例：
            //   模组把"子弹配置序号"切到别的配置后，属性的加减要落到**当前**那一条上。
            //   订阅用的 delegate 存进 _damageSetters，UnInitAttribute 靠它整批退订。
            void Set(Attr attr, Action<DamageData, PEInt> setter)
            {
                // 备份必须在任何写回之前读（下面 else 分支就会写回）
                if (!_damagePrimeBackup.TryGetValue(attr, out var primes))
                {
                    primes = new PEInt[Damages.Count];
                    for (var i = 0; i < Damages.Count; ++i) primes[i] = new PEInt(Damages[i].GetAttr(attr));
                    _damagePrimeBackup[attr] = primes;
                }

                if (!_cfg[attr]) _cfg.Add(attr, primes[Mathf.Clamp(UseDamageIndex, 0, primes.Length - 1)].RawFloat);
                else setter(CurrentDamgeData, AttrFinal(attr));

                Action<PEInt> handler = value => setter(CurrentDamgeData, value);
                _damageSetters[attr] = handler;
                _cfg[attr].OnFinalValueChange += handler;
            }

            Set(Attr.DirectDamage, (d, v) => d.SetDamageDirect(v));
            Set(Attr.ExplosionDamage, (d, v) => d.SetDamageExplosion(v));
            Set(Attr.ExplosionRange, (d, v) => d.SetExplosionRange(v));

            Set(Attr.DirectDestruction, (d, v) => d.SetDirectDestruction(v));
            Set(Attr.ExplosionDestruction, (d, v) => d.SetExplosionDestruction(v));
            Set(Attr.WeaknessBonus, (d, v) => d.SetWeaknessBonus(v));

            Set(Attr.BulletSpeed, (d, v) => d.SetSpeed(v));
            Set(Attr.LifeTime, (d, v) => d.SetLifeTime(v));
            // 穿甲等级：直击 / 爆炸两条独立属性，升级项可直接修改并写回子弹数据
            Set(Attr.DirectAP, (d, v) => d.SetDirectAP(v));
            Set(Attr.ExplosionAP, (d, v) => d.SetExplosionAP(v));
            Set(Attr.ChargeDamageScale, (d, v) => d.SetChargeDamageScale(v));
            Set(Attr.ChargeExplosionRangeScale, (d, v) => d.SetChargeExplosionRangeScale(v));
            Set(Attr.ChargeHeatScale, (d, v) => d.SetChargeHeatScale(v));

            // 伤害类型系数
            Set(Attr.DamageTypeGun, (d, v) => d.SetDamageTypeGun(v));
            Set(Attr.DamageTypeExplosion, (d, v) => d.SetDamageTypeExplosion(v));
            Set(Attr.DamageTypeDestruction, (d, v) => d.SetDamageTypeDestruction(v));
            Set(Attr.DamageTypeReal, (d, v) => d.SetDamageTypeReal(v));
            Set(Attr.DamageTypeToxicity, (d, v) => d.SetDamageTypeToxicity(v));
            Set(Attr.DamageTypeBurn, (d, v) => d.SetDamageTypeBurn(v));
            Set(Attr.DamageTypeFreeze, (d, v) => d.SetDamageTypeFreeze(v));
            Set(Attr.DamageTypeElectric, (d, v) => d.SetDamageTypeElectric(v));
            Set(Attr.DamageTypeVertigo, (d, v) => d.SetDamageTypeVertigo(v));
            Set(Attr.DamageTypeTerror, (d, v) => d.SetDamageTypeTerror(v));
            Set(Attr.DamageTypeRadiation, (d, v) => d.SetDamageTypeRadiation(v));
            Set(Attr.DamageTypeHacker, (d, v) => d.SetDamageTypeHacker(v));

            // 易伤系数（目标挂着对应异常状态时的伤害倍率）
            Set(Attr.VulnerabilityToxicity, (d, v) => d.SetVulnerabilityToxicity(v));
            Set(Attr.VulnerabilityBurn, (d, v) => d.SetVulnerabilityBurn(v));
            Set(Attr.VulnerabilityFreeze, (d, v) => d.SetVulnerabilityFreeze(v));
            Set(Attr.VulnerabilityElectric, (d, v) => d.SetVulnerabilityElectric(v));
            Set(Attr.VulnerabilityVertigo, (d, v) => d.SetVulnerabilityVertigo(v));
            Set(Attr.VulnerabilityTerror, (d, v) => d.SetVulnerabilityTerror(v));
            Set(Attr.VulnerabilityRadiation, (d, v) => d.SetVulnerabilityRadiation(v));
            Set(Attr.VulnerabilityHacker, (d, v) => d.SetVulnerabilityHacker(v));

            // 伤害类型系数（爆炸伤害组）
            Set(Attr.ExplosionTypeGun, (d, v) => d.SetExplosionTypeGun(v));
            Set(Attr.ExplosionTypeExplosion, (d, v) => d.SetExplosionTypeExplosion(v));
            Set(Attr.ExplosionTypeDestruction, (d, v) => d.SetExplosionTypeDestruction(v));
            Set(Attr.ExplosionTypeReal, (d, v) => d.SetExplosionTypeReal(v));
            Set(Attr.ExplosionTypeToxicity, (d, v) => d.SetExplosionTypeToxicity(v));
            Set(Attr.ExplosionTypeBurn, (d, v) => d.SetExplosionTypeBurn(v));
            Set(Attr.ExplosionTypeFreeze, (d, v) => d.SetExplosionTypeFreeze(v));
            Set(Attr.ExplosionTypeElectric, (d, v) => d.SetExplosionTypeElectric(v));
            Set(Attr.ExplosionTypeVertigo, (d, v) => d.SetExplosionTypeVertigo(v));
            Set(Attr.ExplosionTypeTerror, (d, v) => d.SetExplosionTypeTerror(v));
            Set(Attr.ExplosionTypeRadiation, (d, v) => d.SetExplosionTypeRadiation(v));
            Set(Attr.ExplosionTypeHacker, (d, v) => d.SetExplosionTypeHacker(v));

            // 子弹配置序号：基础值取当前 UseDamageIndex（信号枪等运行时切档的值不能在这里被重置），
            // 只在属性真变时才回写字段。tag = IsHide ⇒ 参数面板永远不显示这一行。
            if (!_cfg[Attr.DamageIndex]) _cfg.Add(Attr.DamageIndex, UseDamageIndex);
            _cfg[Attr.DamageIndex].OnFinalValueChange += OnDamageIndexChanged;

        }

        protected override void UnInitAttribute()
        {
            var _cfg = cfg;

            foreach (var item in _damageSetters)
            {
                var attr = _cfg[item.Key];
                if (attr) attr.OnFinalValueChange -= item.Value;
            }
            _damageSetters.Clear();

            if (_cfg[Attr.DamageIndex]) _cfg[Attr.DamageIndex].OnFinalValueChange -= OnDamageIndexChanged;
        }

        /// <summary>子弹配置序号（<see cref="Attr.DamageIndex"/>）落地：只换 <c>Damages</c> 下标，越界夹回合法范围。
        /// <para>⚠ 信号枪的 0/1 档位由 <c>PlayerWeaponsManager</c> 运行时直接赋 <c>UseDamageIndex</c>，
        /// 这类武器别再配"子弹配置序号"模组，否则两边互相覆盖。</para></summary>
        private void OnDamageIndexChanged(PEInt value)
        {
            if (Damages == null || Damages.Count == 0) return;

            var index = Mathf.Clamp(value.RawInt, 0, Damages.Count - 1);
            if (index == UseDamageIndex) return;

            UseDamageIndex = index;
            // 伤害类属性的基线跟着换到新配置，否则面板显示与改装加成还停在旧那条配置上
            RebaseDamageData();
        }

        /// <summary>把伤害类属性的基线换成"当前配置的原始值"（Factor/Extra/ΣBase 保留）；
        /// <see cref="GameAttribute.Rebase"/> 内部 Recalculate 会把结果经 handler 写回新配置。</summary>
        private void RebaseDamageData()
        {
            var index = UseDamageIndex;

            foreach (var item in _damageSetters)
            {
                if (!_damagePrimeBackup.TryGetValue(item.Key, out var primes) || primes.Length == 0) continue;

                var attr = cfg[item.Key];
                if (!attr) continue;

                attr.Rebase(primes[Mathf.Clamp(index, 0, primes.Length - 1)]);
            }
        }


        /// <summary>
        /// 输入射击命令
        /// </summary>
        public bool HandleShootInputs(bool inputDown, bool inputHeld, bool inputUp, bool inputAiming)
        {
            m_InAiming = inputAiming;
            return base.HandleShootInputs(inputDown, inputHeld, inputUp);
        }


        protected override void ReloadStart()
        {
            base.ReloadStart();
            // 换弹语音：手动换弹才会走到这里；
            // 带 AutomaticReload flag 的武器走 UpdateAmmo() 恢复弹药，不会调用 ReloadStart()，故不播。
            // 非玩家武器 Owner 无匹配订阅，自动忽略。
            if (Owner != null&&!WeaponFlag.HasFlag( WeaponFlag.AutomaticReload) && !WeaponFlag.HasFlag(WeaponFlag.NoManualReload))
            {
                GlobalEventBus.PlayMeetSpeech(Owner, !InfiniteAmmo && Ammo.CurrValue == 0 ? SpeechTypeEnum.FinalMaga : SpeechTypeEnum.ReLoad);
            }
        }

        /// <summary>
        /// 第三人称瞄准目标点（由 PlayerWeaponsManager 注入），
        /// 非零时子弹从枪口指向此点（屏幕中心对应的世界位置）
        /// </summary>
        public Vector3 ThirdPersonAimTarget { get; set; }

        /// <summary>
        /// 获取射击方向（含散布）。
        /// 第三人称时子弹从枪口指向屏幕中心对应的世界目标点，确保准星即命中点
        /// </summary>
        public override Vector3 GetShotDirectionWithinSpread(Transform shootTransform)
        {
            if (ThirdPersonAimTarget != default)
            {
                Vector3 baseDirection = (ThirdPersonAimTarget - shootTransform.position).normalized;

                // 三点一线也要吃连射膨胀，否则"准心即命中"会把 bloom 白算
                var bsa = CurrentSpreadAngle;
                if (bsa == 0) return baseDirection;
                PEInt chargeSpreadScale = CurrentDamgeData.GetSpread(WeaponChargeScale_D);
                PEInt spreadAngleRatio = bsa / 180 * (PEInt)(m_InAiming ? 0.3f : 1) * chargeSpreadScale;
                return Vector3.Slerp(baseDirection, Random.insideUnitSphere, spreadAngleRatio.RawFloat);
            }

            return base.GetShotDirectionWithinSpread(shootTransform);
        }

        /// <summary>
        /// 使用补给
        /// </summary>
        public void UseSupply()
        {

            var cost = Magazine.FinalValue - Magazine.CurrValue;
            PEInt supplyValue = PEMath.Ceil(TotalAmmo *(PEInt)0.5f);
            Magazine.CurrValue += PEMath.Min(supplyValue,cost);

            supplyValue -= cost;
            if (supplyValue <= 0) return;
            Ammo.CurrValue += supplyValue;

        }
        /*
        private void OnDrawGizmos()
        {
            if(ShootType== WeaponShootType.Lock)
            {
                var pos = new Vector3(WndManager.Instance.transform.GetRect().rect.width, WndManager.Instance.transform.GetRect().rect.height,0).Mult(WndManager.Instance.transform.localScale);
                //Tool.DrawLabel(pos / 2, pos.ToString(), Time.deltaTime);
                float range = Get(WeaponAttrType.LockRange).RawFloat;
                switch (Lockshape)
                {
                    case ShapeType.Circle:
                        Gizmos.DrawWireSphere(pos / 2, range * 2);
                        break;
                    case ShapeType.Rectangle:
                        Gizmos.DrawWireCube(pos / 2, new(range * 2, range * 2,0));
                        break;
                }


            }
        }
        */
    }


}
