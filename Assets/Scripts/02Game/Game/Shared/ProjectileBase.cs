using Unity.BaseTool;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.ProBuilder;
using Utils;

namespace Unity.FPS.Game
{
    public abstract class ProjectileBase : MonoBehaviour
    {
        /// <summary>模板(用于回收)</summary>
        public ProjectileBase Template { get; set; }
        public GameObject Owner { get; private set; }
        public WeaponBaseController WeaponBase{ get; private set; }

        public DamageData DamageData { get; protected set; }
        /// <summary>初始位置</summary>
        public Vector3 InitialPosition { get; private set; }
        /// <summary>初始方向</summary>
        public Vector3 InitialDirection { get; private set; }
        /// <summary>所继承的武器初速度</summary>
        public Vector3 InheritedMuzzleVelocity { get; private set; }

        /// <summary>充能系数?</summary>
        public float Charge { get; private set; }
        /// <summary>音效范围</summary>
        public float SFXRange { get; private set; }

        /// <summary>最大射程/自爆引信</summary>
        public float MaxRange { get; private set; }
        /// <summary>重力加速度</summary>
        public float Gravity { get; private set; }

        protected Collider[] IgnoredColliders { get; private set; }

        public BulletFlag BulletFlag { get; private set; }

        public UnityAction OnShoot;
        public UnityAction<ProjectileHitData> OnHit;

        public void Shoot(WeaponBaseController controller)
        {
            Shoot(controller, controller.UseDamageIndex);
        }
        public void Shoot(WeaponBaseController controller,int index)
        {
            if (!controller.IsValid()) { Debug.LogWarning("子弹没有武器来源"+gameObject); Tool.Destroy(gameObject); return; }

            Owner = controller.Owner;
            WeaponBase = controller;
            InitialPosition = transform.position;
            InitialDirection = transform.forward;
            DamageData = controller.Damages[index];
            InheritedMuzzleVelocity = DamageData.InheritWeaponSpeed ? controller.MuzzleWorldVelocity : Vector3.zero;
            Charge = controller.WeaponChargeScale_D;
            SFXRange = controller.SFXRange;
            MaxRange = controller.CurrentWeaponRange;
            Gravity = controller.CurrentGravity*Mathf.Lerp(1,DamageData.ChargeGravityScale, Charge);
            BulletFlag = controller.BulletFlag;
            IgnoredColliders = controller.IgnoredColliders;

            OnHit += FpsHelper.Hit;
            OnShoot?.Invoke();
        }
        public void Release()
        {
            Debug.Log("释放" + gameObject, gameObject);
            VFXManager.Release(this);
        }

        private void OnDisable()
        {
            OnShoot = null;
            OnHit = null;
        }
    }

    public struct ProjectileHitData {
        public Vector3 pos, normal; 
        public Collider collider;
        public DamageData data;
        public float chargeScale;
        public GameObject owner;
        public float sfxRange;
        public bool useDiffScale;
        public WeaponBaseController weapon;
        public bool IgnoreSelf;
    }
}