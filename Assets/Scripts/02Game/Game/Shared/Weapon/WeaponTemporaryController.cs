using System.Collections.Generic;
using GameContract;
using PEMaths;
using Unity.BaseTool;
using UnityEngine;

namespace Unity.FPS.Game
{
    using Attr = WeaponAttrType;
    public class WeaponTemporaryController : WeaponReloadController, VfxEffect
    {

        [Foldout("点位和信息", true)]
        [CustomLabel("齐射")]
        public bool UseManyMuzzle;
        [CustomLabel("发射点位")]
        public List<Transform> WeaponManyMuzzles;



        protected virtual void OnEnable()
        {
            if(m_Initialized)
            {
                Ammo.CurrValue = Ammo.FinalValue;
            }
        }

        public override void LogicInit()
        {
            base.LogicInit();
            if (UseManyMuzzle) ShootCost = WeaponManyMuzzles.Count;
            WantsToShoot = true;
        }

        public override void LogicTick()
        {
            base.LogicTick();
            TryShoot();//每帧都尝试射击
        }

        /// <summary>
        /// 进行射击(多枪口)
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

                    ProjectileBase newProjectile = Instantiate(ProjectilePrefab, muzzle.position,
                        Quaternion.LookRotation(shotDirection));
                    newProjectile.Shoot(this);
                }
                ShootFlash(muzzle);
            }

            ResetInterval();

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