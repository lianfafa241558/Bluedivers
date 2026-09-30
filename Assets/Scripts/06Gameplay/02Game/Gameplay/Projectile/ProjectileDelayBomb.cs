using System.Collections;
using System.Collections.Generic;
using FPSGame.Game;
using UnityEngine;
namespace FPSGame.Gameplay
{
    /// <summary>
    /// 延迟炸弹：落地不爆炸，需要通过标记tag，然后只能空爆
    /// </summary>
    [AddComponentMenu("子弹/延迟炸弹", 30)]
    public class ProjectileDelayBomb : ProjectileStandard
    {
        public int DamagesIndex = 1;

        private DamageData BombDamageData;

        protected override void _OnShoot()
        {
            base._OnShoot();
            BombDamageData = WeaponBase.Damages[DamagesIndex];
        }


        protected override IEnumerator DelayedRelese(float time)
        {
            yield return new WaitForSeconds(time);
            OnHit?.Invoke(BuildHitData(transform.position, transform.forward, damageData: BombDamageData));
            Release();
        }
    }
}