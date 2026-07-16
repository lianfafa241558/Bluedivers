using System;
using System.Collections;
using System.Collections.Generic;
using PEMaths;
using Unity.BaseTool;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.Game
{
    using Attr = WeaponAttrType;

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
            Set(Attr.DirectWeakness, damages.SetDirectWeakness);
            Set(Attr.ExplosionWeakness, damages.SetExplosionWeakness);

            Set(Attr.BulletSpeed, damages.SetSpeed);
            Set(Attr.ChargeDamageScale, damages.SetChargeDamageScale);
            Set(Attr.ChargeExplosionRangeScale, damages.SetChargeExplosionRangeScale);

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
            _cfg[Attr.DirectWeakness].OnFinalValueChange -= damages.SetDirectWeakness;
            _cfg[Attr.ExplosionWeakness].OnFinalValueChange -= damages.SetExplosionWeakness;

            _cfg[Attr.BulletSpeed].OnFinalValueChange -= damages.SetSpeed;
            _cfg[Attr.ChargeDamageScale].OnFinalValueChange -= damages.SetChargeDamageScale;
            _cfg[Attr.ChargeExplosionRangeScale].OnFinalValueChange -= damages.SetChargeExplosionRangeScale;

        }


        /// <summary>
        /// 输入射击键
        /// </summary>
        public bool HandleShootInputs(bool inputDown, bool inputHeld, bool inputUp, bool inputAiming)
        {
            m_InAiming = inputAiming;
            return base.HandleShootInputs(inputDown, inputHeld, inputUp);
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