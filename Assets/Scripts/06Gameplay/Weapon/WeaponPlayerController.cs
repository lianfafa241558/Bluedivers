using System;
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

            var _cfg = cfg;
            var damages = Damages[0];

            void Set(Attr attr, Action<PEInt> acton)
            {
                if (!_cfg[attr]) _cfg.Add(attr, damages.GetAttr(attr));
                else acton.Invoke(AttrFinal(attr));
                _cfg[attr].OnFinalValueChange += acton;
            }

            Set(Attr.DirectDamage, damages.SetDamageDirect);
            Set(Attr.ExplosionDamage, damages.SetDamageExplosion);
            Set(Attr.ExplosionRange, damages.SetExplosionRange);

            Set(Attr.DirectDestruction, damages.SetDirectDestruction);
            Set(Attr.ExplosionDestruction, damages.SetExplosionDestruction);
            Set(Attr.WeaknessBonus, damages.SetWeaknessBonus);

            Set(Attr.BulletSpeed, damages.SetSpeed);
            Set(Attr.LifeTime, damages.SetLifeTime);
            // 穿甲等级：直击 / 爆炸两条独立属性，升级项可直接修改并写回子弹数据
            Set(Attr.DirectAP, damages.SetDirectAP);
            Set(Attr.ExplosionAP, damages.SetExplosionAP);
            Set(Attr.ChargeDamageScale, damages.SetChargeDamageScale);
            Set(Attr.ChargeExplosionRangeScale, damages.SetChargeExplosionRangeScale);
            Set(Attr.ChargeHeatScale, damages.SetChargeHeatScale);

            // 伤害类型系数
            Set(Attr.DamageTypeGun, damages.SetDamageTypeGun);
            Set(Attr.DamageTypeExplosion, damages.SetDamageTypeExplosion);
            Set(Attr.DamageTypeDestruction, damages.SetDamageTypeDestruction);
            Set(Attr.DamageTypeReal, damages.SetDamageTypeReal);
            Set(Attr.DamageTypeToxicity, damages.SetDamageTypeToxicity);
            Set(Attr.DamageTypeBurn, damages.SetDamageTypeBurn);
            Set(Attr.DamageTypeFreeze, damages.SetDamageTypeFreeze);
            Set(Attr.DamageTypeElectric, damages.SetDamageTypeElectric);
            Set(Attr.DamageTypeVertigo, damages.SetDamageTypeVertigo);
            Set(Attr.DamageTypeTerror, damages.SetDamageTypeTerror);
            Set(Attr.DamageTypeRadiation, damages.SetDamageTypeRadiation);
            Set(Attr.DamageTypeHacker, damages.SetDamageTypeHacker);

            // 易伤系数（目标挂着对应异常状态时的伤害倍率）
            Set(Attr.VulnerabilityToxicity, damages.SetVulnerabilityToxicity);
            Set(Attr.VulnerabilityBurn, damages.SetVulnerabilityBurn);
            Set(Attr.VulnerabilityFreeze, damages.SetVulnerabilityFreeze);
            Set(Attr.VulnerabilityElectric, damages.SetVulnerabilityElectric);
            Set(Attr.VulnerabilityVertigo, damages.SetVulnerabilityVertigo);
            Set(Attr.VulnerabilityTerror, damages.SetVulnerabilityTerror);
            Set(Attr.VulnerabilityRadiation, damages.SetVulnerabilityRadiation);
            Set(Attr.VulnerabilityHacker, damages.SetVulnerabilityHacker);

            // 伤害类型系数（爆炸伤害组）
            Set(Attr.ExplosionTypeGun, damages.SetExplosionTypeGun);
            Set(Attr.ExplosionTypeExplosion, damages.SetExplosionTypeExplosion);
            Set(Attr.ExplosionTypeDestruction, damages.SetExplosionTypeDestruction);
            Set(Attr.ExplosionTypeReal, damages.SetExplosionTypeReal);
            Set(Attr.ExplosionTypeToxicity, damages.SetExplosionTypeToxicity);
            Set(Attr.ExplosionTypeBurn, damages.SetExplosionTypeBurn);
            Set(Attr.ExplosionTypeFreeze, damages.SetExplosionTypeFreeze);
            Set(Attr.ExplosionTypeElectric, damages.SetExplosionTypeElectric);
            Set(Attr.ExplosionTypeVertigo, damages.SetExplosionTypeVertigo);
            Set(Attr.ExplosionTypeTerror, damages.SetExplosionTypeTerror);
            Set(Attr.ExplosionTypeRadiation, damages.SetExplosionTypeRadiation);
            Set(Attr.ExplosionTypeHacker, damages.SetExplosionTypeHacker);

        }

        protected override void UnInitAttribute()
        {
            var _cfg = cfg;
            var damages = Damages[0];
            _cfg[Attr.DirectDamage].OnFinalValueChange -= damages.SetDamageDirect;
            _cfg[Attr.ExplosionDamage].OnFinalValueChange -= damages.SetDamageExplosion;
            _cfg[Attr.ExplosionRange].OnFinalValueChange -= damages.SetExplosionRange;

            _cfg[Attr.DirectDestruction].OnFinalValueChange -= damages.SetDirectDestruction;
            _cfg[Attr.ExplosionDestruction].OnFinalValueChange -= damages.SetExplosionDestruction;
            _cfg[Attr.WeaknessBonus].OnFinalValueChange -= damages.SetWeaknessBonus;


            _cfg[Attr.BulletSpeed].OnFinalValueChange -= damages.SetSpeed;
            _cfg[Attr.LifeTime].OnFinalValueChange -= damages.SetLifeTime;
            _cfg[Attr.DirectAP].OnFinalValueChange -= damages.SetDirectAP;
            _cfg[Attr.ExplosionAP].OnFinalValueChange -= damages.SetExplosionAP;
            _cfg[Attr.ChargeDamageScale].OnFinalValueChange -= damages.SetChargeDamageScale;
            _cfg[Attr.ChargeExplosionRangeScale].OnFinalValueChange -= damages.SetChargeExplosionRangeScale;
            _cfg[Attr.ChargeHeatScale].OnFinalValueChange -= damages.SetChargeHeatScale;

            // 伤害类型系数
            _cfg[Attr.DamageTypeGun].OnFinalValueChange -= damages.SetDamageTypeGun;
            _cfg[Attr.DamageTypeExplosion].OnFinalValueChange -= damages.SetDamageTypeExplosion;
            _cfg[Attr.DamageTypeDestruction].OnFinalValueChange -= damages.SetDamageTypeDestruction;
            _cfg[Attr.DamageTypeReal].OnFinalValueChange -= damages.SetDamageTypeReal;
            _cfg[Attr.DamageTypeToxicity].OnFinalValueChange -= damages.SetDamageTypeToxicity;
            _cfg[Attr.DamageTypeBurn].OnFinalValueChange -= damages.SetDamageTypeBurn;
            _cfg[Attr.DamageTypeFreeze].OnFinalValueChange -= damages.SetDamageTypeFreeze;
            _cfg[Attr.DamageTypeElectric].OnFinalValueChange -= damages.SetDamageTypeElectric;
            _cfg[Attr.DamageTypeVertigo].OnFinalValueChange -= damages.SetDamageTypeVertigo;
            _cfg[Attr.DamageTypeTerror].OnFinalValueChange -= damages.SetDamageTypeTerror;
            _cfg[Attr.DamageTypeRadiation].OnFinalValueChange -= damages.SetDamageTypeRadiation;
            _cfg[Attr.DamageTypeHacker].OnFinalValueChange -= damages.SetDamageTypeHacker;

            // 易伤系数
            _cfg[Attr.VulnerabilityToxicity].OnFinalValueChange -= damages.SetVulnerabilityToxicity;
            _cfg[Attr.VulnerabilityBurn].OnFinalValueChange -= damages.SetVulnerabilityBurn;
            _cfg[Attr.VulnerabilityFreeze].OnFinalValueChange -= damages.SetVulnerabilityFreeze;
            _cfg[Attr.VulnerabilityElectric].OnFinalValueChange -= damages.SetVulnerabilityElectric;
            _cfg[Attr.VulnerabilityVertigo].OnFinalValueChange -= damages.SetVulnerabilityVertigo;
            _cfg[Attr.VulnerabilityTerror].OnFinalValueChange -= damages.SetVulnerabilityTerror;
            _cfg[Attr.VulnerabilityRadiation].OnFinalValueChange -= damages.SetVulnerabilityRadiation;
            _cfg[Attr.VulnerabilityHacker].OnFinalValueChange -= damages.SetVulnerabilityHacker;

            // 伤害类型系数（爆炸伤害组）
            _cfg[Attr.ExplosionTypeGun].OnFinalValueChange -= damages.SetExplosionTypeGun;
            _cfg[Attr.ExplosionTypeExplosion].OnFinalValueChange -= damages.SetExplosionTypeExplosion;
            _cfg[Attr.ExplosionTypeDestruction].OnFinalValueChange -= damages.SetExplosionTypeDestruction;
            _cfg[Attr.ExplosionTypeReal].OnFinalValueChange -= damages.SetExplosionTypeReal;
            _cfg[Attr.ExplosionTypeToxicity].OnFinalValueChange -= damages.SetExplosionTypeToxicity;
            _cfg[Attr.ExplosionTypeBurn].OnFinalValueChange -= damages.SetExplosionTypeBurn;
            _cfg[Attr.ExplosionTypeFreeze].OnFinalValueChange -= damages.SetExplosionTypeFreeze;
            _cfg[Attr.ExplosionTypeElectric].OnFinalValueChange -= damages.SetExplosionTypeElectric;
            _cfg[Attr.ExplosionTypeVertigo].OnFinalValueChange -= damages.SetExplosionTypeVertigo;
            _cfg[Attr.ExplosionTypeTerror].OnFinalValueChange -= damages.SetExplosionTypeTerror;
            _cfg[Attr.ExplosionTypeRadiation].OnFinalValueChange -= damages.SetExplosionTypeRadiation;
            _cfg[Attr.ExplosionTypeHacker].OnFinalValueChange -= damages.SetExplosionTypeHacker;

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
