
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class WeaponEnemyController : WeaponController
    {


        private enum AttackEnum
        {
            Normal,
            Special1,
            Special2,
            Special3,
        };

        [Foldout("特效和动画")]
        [SerializeField]
        [InspectorName("射击时(单位)播放的动画类型")]
        private AttackEnum animNameType;


        public int AnimName => animNameType switch {
            AttackEnum.Special1 => Constants.k_AnimSpecialAttack1Parameter,
            AttackEnum.Special2 => Constants.k_AnimSpecialAttack2Parameter,
            AttackEnum.Special3 => Constants.k_AnimSpecialAttack3Parameter,
            _ => Constants.k_AnimAttackParameter,
        };

        public override void LogicTick()
        {
            base.LogicTick();

            //子弹用尽自杀
            if (!InfiniteAmmo && !InfiniteMagazine &&
                Ammo.CurrValue+Magazine.CurrValue<=0 &&
                HasFlag(WeaponFlag.AutoDeath))
            {
                ShowWeapon(false);
                GetComponentInParent<Health>().Kill();
            }
        }



        public bool InAttackState()
        {
            //在蓄力/激光/射击
            if (InCharging||InLasering||InShoots) return true;
            //子弹还没打光
            return(!InAutoReload && CanShoot);
        }

        /// <summary>
        /// 输入射击命令
        /// </summary>
        public override bool HandleShootInputs(bool inputDown, bool inputHeld, bool inputUp)
        {
            if (!AllowShoot && !inputUp) return false;
            var muzzle = GetMuzzle(0);
            if ((!muzzle || !muzzle.gameObject.activeInHierarchy) && !inputUp) return false;
            return base.HandleShootInputs(inputDown, inputHeld, inputUp);
           
        }
        /// <summary>
        /// 输入射击命令(不关心结果)
        /// </summary>
        public void ShootInputs(bool inputDown, bool inputHeld, bool inputUp)
        {
            HandleShootInputs(inputDown, inputHeld, inputUp);
        }


        protected override bool TryShoot()
        {
            if (AllowShoot)
            {
                //Debug.LogError("射击开始 "+Magazine.CurrValue,gameObject);
                //Debug.LogWarning("上次射击时间"+ m_LastTimeShot+"间隔" + DelayBetweenShots+"当前时间"+ Time.time);
                if (InfiniteMagazine)
                {
                    HandleShoot();
                    ResetInterval();
                    //Debug.LogError("射击结束" + Magazine.CurrValue, gameObject);
                    return true;
                }
                else if (Magazine.CurrValue >= ShootCost)
                {
                    HandleShoot();
                    UseMagazine(ShootCost);
                    ResetInterval();
                    //Debug.LogError("射击结束" + Magazine.CurrValue, gameObject);
                    return true;
                }
                
            }

            return false;
        }


        private void OnDrawGizmosSelected()
        {
            // 编辑器下可能未配置伤害数据
            if (Damages == null || Damages.Count == 0) return;

            // 收集发射点位：齐射武器遍历齐射点位，否则遍历单点点位
            var muzzles = new List<Transform>();
            if (UseManyMuzzle && WeaponManyMuzzles.IsValid() && WeaponManyMuzzles.Count > 0)
            {
                muzzles.AddRange(WeaponManyMuzzles);
            }
            else
            {
                if (WeaponMuzzle.IsValid()) muzzles.Add(WeaponMuzzle);
                if (WeaponMuzzle2.IsValid()) muzzles.Add(WeaponMuzzle2);
            }
            if (muzzles.Count == 0) return;

            for (int i = 0; i < muzzles.Count; ++i) DrawBallistic(muzzles[i]);

            void DrawBallistic(Transform muzzle)
            {
                Gizmos.color = Color.cyan;

                Vector3 pos = muzzle.position;
                Vector3 muzzleVelocity = muzzle.forward * CurrentSpeed;
                Vector3 inheriteVelocity = CurrentDamgeData.InheritWeaponSpeed ? MuzzleWorldVelocity : Vector3.zero;
                Vector3 velocity = muzzleVelocity;
                float gravity = CurrentGravity;
                float totalRange = CurrentWeaponRange;
                const float stepTime = 0.02f;
                float elapsedDistance = 0f;
                Vector3 lastPos = pos;

                while (elapsedDistance < totalRange)
                {
                    Vector3 moveDelta = (velocity + inheriteVelocity) * stepTime;
                    pos += moveDelta;
                    velocity += Vector3.down * gravity * stepTime;
                    elapsedDistance += moveDelta.magnitude;

                    Gizmos.DrawLine(lastPos, pos);
                    lastPos = pos;
                }

                if (Damages[0].GetDamageOuterRadius(1) > 0)
                    Gizmos.DrawWireSphere(pos, Damages[0].GetDamageOuterRadius(1).RawFloat);
            }
        }

#if UNITY_EDITOR


        [ContextMenu("测试")]
        private void _Copy()
        {
            string jsonStr = JsonUtility.ToJson(GetComponent<WeaponController>());
            JsonUtility.FromJsonOverwrite(jsonStr, this);
        }
        /*
        [ContextMenu("射击")]
        private void _Shoot() {
            HandleShootInputs(true, false, false, true);
        }*/
#endif

    }
}