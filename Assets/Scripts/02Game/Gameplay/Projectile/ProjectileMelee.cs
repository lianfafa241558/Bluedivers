using System.Collections.Generic;

using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 近战伤害
    /// </summary>
    public class ProjectileMelee : ProjectileBase
    {

        protected virtual void OnEnable()
        {
            OnShoot += _OnShoot;
            OnHit += HitFX;
        }

        protected virtual void _OnShoot()
        {
            OnHit?.Invoke(new() {
                pos = InitialPosition + InitialDirection * (Mathf.Max(MaxRange, 1)),
                normal = InitialDirection,
                collider = null,
                data = DamageData,
                chargeScale = Charge,
                owner = Owner,
                sfxRange = SFXRange,
                weapon = WeaponBase,
                useDiffScale = BulletFlag.HasFlag(BulletFlag.EnemyIntensify),
                IgnoreSelf = true,
            });
        }
        
        /// <summary>击中 </summary>
        void HitFX(ProjectileHitData hitdata)
        {
            Release();
        }


    }
}