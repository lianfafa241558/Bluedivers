using System.Collections.Generic;
using GameContract;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class WeaponMineLauncherController : WeaponTemporaryController
    {
        private IHealth health;
        public override float CurrentSpeed => nowSpeed;
        private float nowSpeed;

        protected override void OnEnable()
        {
            base.OnEnable();
            ResetSpeed();
        }

        public override void LogicInit()
        {
            base.LogicInit();
            ResetSpeed();
            health = GetComponent<IHealth>();
            health.OnDie += Stop;
        }

        public override void LogicUnInit()
        {
            base.LogicUnInit();
            health.OnDie -= Stop;
            health = null;
        }

        private void ResetSpeed()
        {
            if (m_Initialized)
            {
                nowSpeed = AttrFinal(WeaponAttrType.LockRange).RawFloat;
            }
        }
        protected override void ReloadEnd()
        {
            base.ReloadEnd();
            nowSpeed += AttrFinal(WeaponAttrType.LockDistance).RawFloat;//使用锁定距离代替装弹增长范围
        }

        private void Stop(GameObject _)
        {
            Ammo.CurrValue = 0;
            Magazine.CurrValue = 0;
        }
    }
}