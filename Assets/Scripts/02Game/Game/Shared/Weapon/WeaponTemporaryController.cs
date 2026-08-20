using System.Collections.Generic;
using FPSGame.Attribute;
using GameContract;
using PEMaths;

using UnityEngine;

namespace Unity.FPS.Game
{
    using Attr = WeaponAttrType;
    public class WeaponTemporaryController : WeaponController, IVfxEffect
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
        }

        public override void LogicTick()
        {
            base.LogicTick();
            // 临时武器无外部输入：每帧模拟"按住扳机"，由 ShootType 决定实际行为(全自动/激光/蓄力/锁定等)
            HandleShootInputs(true, true, false);
        }


        public void SetOwner(GameObject owner, GameObject weaponRoot, Collider target, Vector3 point)
        {
            Owner = owner;
        }



        
    }

   
}