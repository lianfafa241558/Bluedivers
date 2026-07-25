using System;
using Core;
using FPSGame.Attribute;
using PEMaths;

using UnityEngine;

namespace Unity.FPS.Game
{
    using Attr = WeaponAttrType;
    using Random = UnityEngine.Random;

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


        public float GetRecoil()=> RecoilForce*(m_InAiming?Mathf.Max(AimZoomRatio*0.5f,0.1f):1);


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

                var bsa = AttrFinal(WeaponAttrType.BulletsSpreadAngle);
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