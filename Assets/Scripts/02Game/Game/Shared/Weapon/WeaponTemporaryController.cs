using System.Collections.Generic;
using FPSGame.Attribute;
using GameContract;
using PEMaths;

using UnityEngine;

namespace Unity.FPS.Game
{
    using Attr = WeaponAttrType;
    public class WeaponTemporaryController : WeaponReloadController, IVfxEffect
    {

        [Foldout("点位和信息", true)]
        [InspectorName("齐射")]
        public bool UseManyMuzzle;
        /// <summary>
        /// 会自动根据齐射数量修改一次消耗的弹药量
        /// </summary>
        [InspectorName("发射点位")]
        public List<Transform> WeaponManyMuzzles;



        protected virtual void OnEnable()
        {
            if(m_Initialized)
            {
                Ammo.CurrValue = Ammo.FinalValue;
                Magazine.CurrValue = Magazine.FinalValue;
            }
        }

        public override void LogicInit()
        {
            base.LogicInit();
            if (UseManyMuzzle) ShootCost = WeaponManyMuzzles.Count;
            else if(WeaponManyMuzzles.Count>0) Muzzles = WeaponManyMuzzles.ToArray();
            if (AttrFinal(Attr.StartCool) == 0) WantsToShoot = true;
        }

        public override void LogicTick()
        {
            base.LogicTick();
            if (AttrFinal(Attr.StartCool) > 0) WantsToShoot = ShootInterval.CurrValue >= new PEInt(-0.1f);
            TryShoot();//每帧都尝试射击
        }

        /// <summary>
        /// 进行射击(多枪管)
        /// </summary>
        /// <returns></returns>
        protected override void HandleShoot()
        {
            if (!UseManyMuzzle)
            {
                base.HandleShoot();
                return;
            }

            int bulletsPerShotFinal = AttrFinal(Attr.BulletsPerShot,1).RawInt;
            for (int u=0;u< WeaponManyMuzzles.Count;++u)
            {
                Transform muzzle = WeaponManyMuzzles[u];

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
                    ProjectileBase newProjectile = VFXManager.Creat(ProjectilePrefab, shotPos,
                        Quaternion.LookRotation(shotDirection));
                    newProjectile.Shoot(this);
                    Debug.DrawLine(shotPos, muzzle.position+ shotDirection * CurrentWeaponRange,Color.red,2);
                }
                ShootFlash(muzzle);
            }

            ResetInterval();
            OnShoot?.Invoke(this);
            if (!UseContinuousShootSound)
            {
                PlaySFX(ShootSfx);
            }


        }


        public void SetOwner(GameObject owner, GameObject weaponRoot, Collider target, Vector3 point)
        {
            Owner = owner;
        }



        
    }

   
}