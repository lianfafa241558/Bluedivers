using System.Collections.Generic;
using Core;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FPSGame.AI
{

    /// <summary>
    /// 允许任意 I_AIController 的表现控制器。
    /// 受击 / 死亡表现与特效管线来自 <see cref="FxControllerBase"/>，本类只额外绑定 AI 时机
    /// （攻击 / 发现目标 / 丢失目标）与诞生材质。
    /// </summary>
    public abstract partial class EnemyControllerFX : FxControllerBase
    {
        [InspectorName("初始材质")]
        [SerializeField]
        private Material BirthMaterial;

        protected I_AIController m_Controller;

        /// <summary>诞生材质替换前的原始材质（用于还原）</summary>
        private List<KVP<Renderer, Material[]>> originalMaterials;

        protected virtual void Start()
        {
            m_Controller = GetComponent<I_AIController>();

            m_Controller.OnAttack += OnAttack;
            m_Controller.OnDetectedTarget += OnDetectedTarget;
            m_Controller.OnLostTarget += OnLostTarget;
            m_Controller.OnHit += OnHit;
            m_Controller.OnDie += OnDie;

            InitFx();
            if (m_Controller.BirthDuration > 0) TriggerFX(OccasionTypeEnum.Birth, m_Controller.Pos, Quaternion.identity, transform);
            if (BirthMaterial && m_Controller.BirthDuration > 0) Invoke(nameof(RestoreMat), m_Controller.BirthDuration);
            InitAboStateFxListener();
        }

        private void OnDestroy()
        {
            if (m_Controller == null) return;
            m_Controller.OnAttack -= OnAttack;
            m_Controller.OnDetectedTarget -= OnDetectedTarget;
            m_Controller.OnLostTarget -= OnLostTarget;
            m_Controller.OnHit -= OnHit;
            m_Controller.OnDie -= OnDie;
            OnDestroyAboStateFx();
        }

        protected virtual void Update()
        {
            UpdateFx();
        }

        /// <summary>受击时</summary>
        protected virtual void OnHit(Vector3 pos,Vector3 normal) => PlayHitFx(pos, normal);

        /// <summary>攻击时</summary>
        protected virtual void OnAttack(WeaponBaseController weapon)
        {
            TriggerRS(OccasionTypeEnum.Attack);
        }

        /// <summary>发现目标</summary>
        protected virtual void OnDetectedTarget()
        {
            TriggerRS(OccasionTypeEnum.DetectedTarget);
            TriggerFX(OccasionTypeEnum.DetectedTarget, m_Controller.HpPos, Quaternion.identity, transform);
            SetBool(Constants.k_AnimIsActiveParameter, true);
        }

        /// <summary>丢失目标</summary>
        protected virtual void OnLostTarget()
        {
            TriggerRS(OccasionTypeEnum.LostTarget);
            TriggerFX(OccasionTypeEnum.LostTarget, m_Controller.Pos, Quaternion.identity, transform);
            SetBool(Constants.k_AnimIsActiveParameter, false);
        }

        /// <summary>死亡时</summary>
        protected virtual void OnDie() => PlayDieFx(m_Controller.Pos);

        /// <summary>扫描开始：复位诞生材质记录</summary>
        protected override void OnFxInitBegin()
        {
            originalMaterials = new();
        }

        /// <summary>每个渲染器扫描完成：整块换成诞生材质，等待 BirthDuration 后还原</summary>
        protected override void OnFxRendererReady(Renderer renderer)
        {
            if (!BirthMaterial) return;
            originalMaterials.Add(new(renderer, renderer.sharedMaterials));
            Material[] newMats = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                newMats[i] = BirthMaterial;
            }
            if (m_Controller.BirthDuration > 0) renderer.sharedMaterials = newMats;
        }

        void RestoreMat()
        {
            for (int i = 0; i < originalMaterials.Count; ++i)
            {
                originalMaterials[i].Key.sharedMaterials = originalMaterials[i].Value; // 恢复原始材质
            }
        }
    }
}
