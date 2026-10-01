using System.Collections.Generic;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Weapon;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 近战伤害
    /// </summary>
    [AddComponentMenu("子弹/近战", 30)]
    public class ProjectileMelee : ProjectileBase
    {

        protected virtual void OnEnable()
        {
            OnShoot += _OnShoot;
            OnHit += HitFX;
        }

        protected virtual void _OnShoot()
        {
            //近战不自伤：只对目标结算伤害
            var hitData = BuildHitData(InitialPosition + InitialDirection * Mathf.Max(MaxRange, 1), InitialDirection);
            hitData.IgnoreSelf = true;
            OnHit?.Invoke(hitData);
        }
        
        /// <summary>击中 </summary>
        void HitFX(ProjectileHitData hitdata)
        {
            Release();
        }


    }
}