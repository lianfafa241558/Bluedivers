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
            if (AttrFinal(Attr.StartCool) == 0) WantsToShoot = true;
        }

        public override void LogicTick()
        {
            base.LogicTick();
            if (AttrFinal(Attr.StartCool) > 0) WantsToShoot = ShootInterval.CurrValue >= new PEInt(-0.1f);
            TryShoot();//每帧都尝试射击
        }


        public void SetOwner(GameObject owner, GameObject weaponRoot, Collider target, Vector3 point)
        {
            Owner = owner;
        }



        
    }

   
}