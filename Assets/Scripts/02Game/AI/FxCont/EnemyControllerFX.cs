using System.Collections.Generic;
using Core;

using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FPSGame.AI
{
    /// <summary>
    /// 允许任意I_AIController
    /// </summary>
    public abstract partial class EnemyControllerFX : MonoBehaviour {

        public Animator Animator;
        [Header("特效")]
        [InspectorName("初始材质")]
        [SerializeField]
        private Material BirthMaterial;

        /// <summary>共享渲染模板 SO（rendererSet；条目材质留空时用 fxMaterial）</summary>
        [InspectorName("特效模板 SO")]
        public EnemyFxData_SO fxData;

        /// <summary>事件特效 SO（fxDic：受击/死亡等时机的音效粒子），可被同类单位共享</summary>
        [InspectorName("事件特效 SO")]
        public EnemyFxEventData_SO fxEvent;

        /// <summary>单位自身生效材质：RendererSetConfig.material 为空时用它做匹配/生效材质</summary>
        [InspectorName("生效材质")]
        public Material fxMaterial;

        [Header("击中闪光")]
        /// <summary>运行态 MPB 闪变条目（由 fxData.rendererSet 在 InitRS 构建），不参与序列化</summary>
        private List<RendererSet> rendererSet = new();

        /// <summary>是否已提示过"特效条目缺材质"（只提示一次）</summary>
        private bool _warnedNoMaterial;


        protected I_AIController m_Controller;
        private List<KVP<Renderer,Material[]>> originalMaterials;

        protected float m_lastDamageTime;
        protected bool allowDeath;

        protected virtual void Start() 
        {

            m_Controller = GetComponent<I_AIController>();

            m_Controller.OnAttack += OnAttack;
            m_Controller.OnDetectedTarget += OnDetectedTarget;
            m_Controller.OnLostTarget += OnLostTarget;
            m_Controller.OnDamaged += OnDamaged;
            m_Controller.OnDie += OnDie;

            InitRS();
            if(m_Controller.BirthDuration>0) TriggerFX(OccasionTypeEnum.Birth, m_Controller.Pos, Quaternion.identity, transform);
            InitAboStateFxListener();
            
        }
        private void OnDestroy()
        {
            if (m_Controller==null) return;
            m_Controller.OnAttack -= OnAttack;
            m_Controller.OnDetectedTarget -= OnDetectedTarget;
            m_Controller.OnLostTarget -= OnLostTarget;
            m_Controller.OnDamaged -= OnDamaged;
            m_Controller.OnDie -= OnDie;
            OnDestroyAboStateFx();

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
                pos = m_Controller.CenterPos;
                normal = transform.rotation;
            }
            //每0.05秒最多触发一次音效
            bool ignoreAudio = Time.time < m_lastDamageTime + Constants.LoginFrame.RawFloat;
            if (!ignoreAudio)
            {
                m_lastDamageTime = Time.time;
            }
            TriggerFX(OccasionTypeEnum.Hit, pos, normal, collider?collider.transform:default, ignoreAudio);
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
            TriggerFX(OccasionTypeEnum.DetectedTarget, m_Controller.HpPos, Quaternion.identity, transform);
            SetBool(Constants.k_AnimIsActiveParameter, true);
        }

        /// <summary>
        /// 丢失目标
        /// </summary>
        protected virtual void OnLostTarget() {
            TriggerRS(OccasionTypeEnum.LostTarget);
            TriggerFX(OccasionTypeEnum.LostTarget, m_Controller.Pos, Quaternion.identity, transform);
            SetBool(Constants.k_AnimIsActiveParameter, false);
        }

        /// <summary>
        /// 死亡时
        /// </summary>
        protected virtual void OnDie() {
            allowDeath = true;
            TriggerRS(OccasionTypeEnum.Die);
            TriggerFX(OccasionTypeEnum.Die, m_Controller.Pos, Quaternion.identity,null);
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
            rendererSet.Clear();
            if (fxData.IsValid() && fxData.rendererSet != null)
            {
                // 由共享配置构建运行态条目（实例私有状态：MPB/匹配结果/计时）
                for (int c = 0; c < fxData.rendererSet.Count; ++c)
                {
                    var cfg = fxData.rendererSet[c];
                    if (cfg == null) continue;
                    rendererSet.Add(new RendererSet { Config = cfg });
                }
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++) {
                    for (int u = 0; u < rendererSet.Count; ++u) {
                        // 材质来源：config.material 非空优先，否则用单位 fxMaterial（模板条目通常留空）
                        Material mat = GetFxMaterial(rendererSet[u]);
                        if (mat == null)
                        {
                            if (!_warnedNoMaterial)
                            {
                                _warnedNoMaterial = true;
                                Debug.LogWarning(gameObject + "：特效条目既无 RendererSetConfig.material，组件也未设置 fxMaterial，闪白将无法匹配材质。", gameObject);
                            }
                            continue;
                        }
                        if (renderer.sharedMaterials[i] == mat)
                        {
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
                    if(m_Controller.BirthDuration > 0) renderer.sharedMaterials = newMats;
                }
            }
            if (BirthMaterial&&m_Controller.BirthDuration > 0)
            {
                Invoke("RestoreMat", m_Controller.BirthDuration);
            }
        }

        /// <summary>特效条目生效材质：config.material 非空优先（特例覆盖），否则回落单位 fxMaterial（模板条目通常留空）</summary>
        private Material GetFxMaterial(RendererSet rs)
        {
            if (rs == null || rs.Config == null) return fxMaterial;
            return rs.Config.material != null ? rs.Config.material : fxMaterial;
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

        /// <summary>
        /// 取某时机的特效配置：fxEvent（事件 SO）优先；fxData.fxDic 为过渡兜底（旧资产未抽离 fxDic 前兼容）。
        /// </summary>
        protected FxSetConfig GetFxSet(OccasionTypeEnum type)
        {
            if (fxEvent.IsValid() && fxEvent.fxDic.TryGet(type, out var cfg))
            {
                return cfg;
            }
            if (fxData.IsValid() && fxData.fxDic.TryGet(type, out var cfg2))
            {
                return cfg2;
            }
            return null;
        }

        protected void TriggerFX(OccasionTypeEnum type,Vector3 pos,Quaternion roat,Transform parent,bool ignoreAudio =false) {
            var value = GetFxSet(type);
            if (value == null) return;
            // 有音效组用音效组，否则用单个音频剪辑
            if (!ignoreAudio && (value.SG || value.cilp.IsValid()))
            {
                if (value.SG)
                {
                    AudioSvc.PlaySound(value.SG.Get(pos));
                }
                else
                {
                    AudioSvc.PlaySound(new(value.cilp, pos,range:80, group: AudioGroups.Enemy));
                }
            }
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
            //Debug.LogError(anim+"name"+name,anim);
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