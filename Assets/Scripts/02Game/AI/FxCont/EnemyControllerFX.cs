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

        /// <summary>共享特效配置 SO（每敌人类型一个资产）。赋值后优先于旧 rendererSet/fxDic（迁移过渡期旧字段兜底）</summary>
        [InspectorName("特效配置 SO")]
        public EnemyFxData_SO fxData;

        [SerializeField]
        protected DisplayDic<OccasionTypeEnum, FxSet> fxDic = new();

        [Header("击中闪光")]
        public List<RendererSet> rendererSet;


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
            if (fxData.IsValid() && fxData.rendererSet != null && fxData.rendererSet.Count > 0)
            {
                // SO 优先：由共享配置构建运行态列表（实例私有状态），替代序列化到 prefab 的旧内联数据
                var runtime = new List<RendererSet>(fxData.rendererSet.Count);
                for (int c = 0; c < fxData.rendererSet.Count; ++c)
                {
                    var cfg = fxData.rendererSet[c];
                    runtime.Add(new RendererSet
                    {
                        type = (RendererSet.MPBTypeEnum)cfg.type,
                        occasion = cfg.occasion,
                        material = cfg.material,
                        colorName = cfg.colorName,
                        defaultColor = cfg.defaultColor,
                        switchOccasion = cfg.switchOccasion,
                        switchColor = cfg.switchColor,
                        gradient = cfg.gradient,
                        duration = cfg.duration,
                    });
                }
                rendererSet = runtime;
            }

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
                    if(m_Controller.BirthDuration > 0) renderer.sharedMaterials = newMats;
                }
            }
            if (BirthMaterial&&m_Controller.BirthDuration > 0)
            {
                Invoke("RestoreMat", m_Controller.BirthDuration);
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

#if UNITY_EDITOR
        /// <summary>是否仍携带旧内联 FX 配置（迁移工具据此判定是否需要导出 SO）</summary>
        public bool HasLegacyFxData
        {
            get
            {
                if (rendererSet != null && rendererSet.Count > 0) return true;
                try { return fxDic.Count > 0; }
                catch { return false; }
            }
        }

        /// <summary>迁移工具调用：把旧内联 rendererSet/fxDic 逐字段拷入共享 SO 资产（不改动自身序列化字段）</summary>
        public void ExportLegacyTo(EnemyFxData_SO target)
        {
            if (target == null) return;

            target.rendererSet = new List<RendererSetConfig>();
            if (rendererSet != null)
            {
                for (int i = 0; i < rendererSet.Count; ++i)
                {
                    var src = rendererSet[i];
                    if (src == null) continue;
                    target.rendererSet.Add(new RendererSetConfig
                    {
                        type = (MPBTypeEnum)src.type,
                        occasion = src.occasion,
                        material = src.material,
                        colorName = src.colorName,
                        defaultColor = src.defaultColor,
                        switchOccasion = src.switchOccasion,
                        switchColor = src.switchColor,
                        gradient = src.gradient,
                        duration = src.duration,
                    });
                }
            }

            foreach (var key in fxDic.Keys)
            {
                if (!fxDic.TryGet(key, out var src) || src == null) continue;
                target.fxDic[key] = new FxSetConfig
                {
                    SG = src.SG,
                    cilp = src.cilp,
                    ps = src.ps,
                    trans = src.trans,
                    go = src.go != null ? new List<ArmorBreakEffect>(src.go) : null,
                };
            }
        }
#endif


        /// <summary>取某时机的特效配置：SO 共享配置优先，旧内联 fxDic 兜底（迁移过渡期）</summary>
        protected IFxSet GetFxSet(OccasionTypeEnum type)
        {
            if (fxData.IsValid())
            {
                return fxData.fxDic.TryGet(type, out var cfg) ? cfg : null;
            }
            return fxDic.TryGet(type, out var legacy) ? legacy : null;
        }

        protected void TriggerFX(OccasionTypeEnum type,Vector3 pos,Quaternion roat,Transform parent,bool ignoreAudio =false) {
            var value = GetFxSet(type);
            if (value == null) return;
            // 有音效组用音效组，否则用单个音频剪辑
            if (!ignoreAudio && (value.SoundGroup || value.Clip.IsValid()))
            {
                if (value.SoundGroup)
                {
                    AudioSvc.PlaySound(value.SoundGroup.Get(pos));
                }
                else
                {
                    AudioSvc.PlaySound(new(value.Clip, pos,range:80, group: AudioGroups.Enemy));
                }
            }
            if (value.Particle.IsValid())
            {
                VFXManager.Creat(value.Particle.gameObject, pos, roat, parent);
            }
            if (value.SpawnObject.IsValid())
            {
                Instantiate(value.SpawnObject, pos, transform.rotation,null);
            }
            foreach (var item in value.Effects)
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


        [System.Serializable]
        protected class FxSet : IFxSet {
            public AudioClip cilp;
            public SoundGroup_SO SG;
            public ParticleSystem ps;
            public GameObject trans;//创建的物体
            public List<ArmorBreakEffect> go;

            AudioClip IFxSet.Clip => cilp;
            SoundGroup_SO IFxSet.SoundGroup => SG;
            ParticleSystem IFxSet.Particle => ps;
            GameObject IFxSet.SpawnObject => trans;
            IReadOnlyList<ArmorBreakEffect> IFxSet.Effects => go;
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