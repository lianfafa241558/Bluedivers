using System.Collections.Generic;
using Core;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace Unity.FPS.AI
{

    public abstract class EnemyControllerFX : MonoBehaviour {

        public Animator Animator;
        [Header("特效")]
        [CustomLabel("初始材质")]
        [SerializeField]
        private Material BirthMaterial;


        [SerializeField]
        protected DisplayDic<OccasionTypeEnum, FxSet> fxDic = new();

        [Header("击中闪光")]
        public List<RendererSet> rendererSet;


        protected I_AIController m_EnemyController;
        private List<KVP<Renderer,Material[]>> originalMaterials;

        protected virtual void Start() 
        {

            m_EnemyController = GetComponent<I_AIController>();

            m_EnemyController.OnAttack += OnAttack;
            m_EnemyController.OnDetectedTarget += OnDetectedTarget;
            m_EnemyController.OnLostTarget += OnLostTarget;
            m_EnemyController.OnDamaged += OnDamaged;
            m_EnemyController.OnDie += OnDie;

            InitRS();
            TriggerFX(OccasionTypeEnum.Birth, m_EnemyController.Pos, Quaternion.identity, transform);
            
        }

        protected virtual void Update() {
            UpdateRS();
        }


        /// <summary>
        /// 受击时
        /// </summary>
        protected virtual void OnDamaged(Collider collider) {
            TriggerRS(OccasionTypeEnum.Hit);
            Vector3 pos;
            Quaternion normal;
            if (collider)
            {
                pos = collider.RandomPoint(out normal);
            }
            else
            {
                pos = m_EnemyController.CenterPos;
                normal = transform.rotation;
            }
            
            TriggerFX(OccasionTypeEnum.Hit, pos, normal, collider?collider.transform:default);
            SetTrigger(Constants.k_AnimOnDamagedParameter,true);
        }
        /// <summary>
        /// 攻击时
        /// </summary>
        protected virtual void OnAttack(WeaponBaseController weapon) {
            TriggerRS(OccasionTypeEnum.Attack);
            
        }

        /// <summary>
        /// 发现目标
        /// </summary>
        protected virtual void OnDetectedTarget() {
            TriggerRS(OccasionTypeEnum.DetectedTarget);
            TriggerFX(OccasionTypeEnum.DetectedTarget, m_EnemyController.HpPos, Quaternion.identity, transform);
            SetBool(Constants.k_AnimIsActiveParameter, true);
        }

        /// <summary>
        /// 丢失目标
        /// </summary>
        protected virtual void OnLostTarget() {
            TriggerRS(OccasionTypeEnum.LostTarget);
            TriggerFX(OccasionTypeEnum.LostTarget, m_EnemyController.Pos, Quaternion.identity, transform);
            SetBool(Constants.k_AnimIsActiveParameter, false);
        }

        /// <summary>
        /// 死亡时
        /// </summary>
        void OnDie() {
            TriggerRS(OccasionTypeEnum.Die);
            TriggerFX(OccasionTypeEnum.Die, m_EnemyController.Pos, Quaternion.identity,null);
            SetBool(Constants.k_AnimIsActiveParameter, false);
            SetTrigger(Constants.k_AnimOnDeathParameter, true);
        }

    /*    
#if UNITY_EDITOR
        [ContextMenu("测试")]
        private void _Copy()
        {
            var EnemyController = GetComponent<EnemyController>();
            var EnemyInputBaseController = GetComponent<EnemyInputBaseController>();

            var EnemyMobile = GetComponent<EnemyMobile>();

            BodyMaterial = EnemyController.BodyMaterial;
            OnHitBodyGradient = EnemyController.OnHitBodyGradient;
            FlashOnHitDuration = EnemyController.FlashOnHitDuration;

        }
#endif*/


        private void InitRS() {
            originalMaterials = new();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++) {
                    for (int u = 0; u < rendererSet.Count; ++u) {
                        if (renderer.sharedMaterials[i] == rendererSet[u].material) {
                            rendererSet[u].Add(renderer, i);
                        }
                    }
                }
                if (BirthMaterial)
                {
                    originalMaterials.Add(new(renderer, renderer.sharedMaterials));
                    Material[] newMats = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < newMats.Length; i++)
                    {
                        newMats[i] = BirthMaterial;
                    }
                    renderer.sharedMaterials = newMats;
                }
            }
            if (BirthMaterial)
            {
                Invoke("RestoreMat", m_EnemyController.BirthDuration);
            }
        }

        void RestoreMat()
        {
            for (int i = 0; i < originalMaterials.Count; ++i)
            {
                originalMaterials[i].Key.sharedMaterials = originalMaterials[i].Value; // 恢复原始材质
            }
        }

        protected void TriggerRS(OccasionTypeEnum type) {
            for (int u = 0; u < rendererSet.Count; ++u) {
                rendererSet[u].Trigger(type);
            } 
        }
        private void UpdateRS() {
            for (int u = 0; u < rendererSet.Count; ++u) {
                rendererSet[u].Update();
            }
        }


        protected void TriggerFX(OccasionTypeEnum type,Vector3 pos,Quaternion roat,Transform parent) {
            if(fxDic.TryGet(type, out var value)){
                if(value.cilp.IsValid()) AudioManager.PlaySound(new(value.cilp, pos, group: AudioGroups.Enemy));
                if (value.ps.IsValid())
                {
                    VFXManager.Creat(value.ps.gameObject, pos, roat, parent);
                }
                if (value.trans.IsValid())
                {
                    Instantiate(value.trans, pos, transform.rotation,null);
                }
                foreach (var item in value.go)
                {
                    if (!item.go)
                    {
                        Debug.LogError(gameObject+"状态"+type+"没有设置物体",gameObject);
                        return;
                    }
                    item.go.SetActive(item.state);
                    item.go.transform.localScale *= item.scale;
                }
            }
        }


        [System.Serializable]
        protected class FxSet {
            public AudioClip cilp;
            public ParticleSystem ps;
            public GameObject trans;
            public List<ArmorBreakEffect> go;
        }


        public void SetTrigger(int name, bool state)
        {
            var anim = Animator;
            if (!anim) return;
            if (state)
            {
                anim.SetTrigger(name);
            }
            else
            {
                anim.ResetTrigger(name);
            }
        }
        public void SetBool(int name, bool state)
        {
            var anim = Animator;
            if (!anim) return;
            anim.SetBool(name, state);
        }
        public void SetFloat(int name, float value)
        {
            var anim = Animator;
            if (!anim) return;
            anim.SetFloat(name, value);
        }
    }


}