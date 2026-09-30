using System.Collections.Generic;
using Core;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace FPSGame.AI
{
    /// <summary>
    /// 表现控制器基类：**与 AI 脱钩**的「受击 / 死亡」表现基建。
    /// <para>提供：① 渲染模板条目（rendererSet）的命中闪变；② 事件特效 SO（fxEvent）的音效/粒子/挂点；
    /// ③ Animator 参数助手；④ 统一的受击 / 死亡表现播放（含音效节流与动画触发器）。</para>
    /// <para>事件来源由子类决定：AI 单位走 <c>I_AIController</c>（见 EnemyControllerFX），
    /// 没有 AI 但有生命值的物体走 <c>IHealth</c>（见 HitDeathFX）。</para>
    /// </summary>
    public abstract class FxControllerBase : MonoBehaviour
    {
        [Header("特效")]
        public Animator Animator;

        /// <summary>共享渲染模板 SO（rendererSet；条目材质留空时用 fxMaterial）</summary>
        [InspectorName("特效模板 SO")]
        public EnemyFxData_SO fxData;

        /// <summary>事件特效 SO（fxDic：受击/死亡等时机的音效粒子），可被同类单位共享</summary>
        [InspectorName("事件特效 SO")]
        public EnemyFxEventData_SO fxEvent;

        /// <summary>单位自身生效材质：RendererSetConfig.material 为空时用它做匹配/生效材质</summary>
        [InspectorName("生效材质")]
        public Material fxMaterial;

        [SerializeField]
        private List<KVP<OccasionTypeEnum, UnityEvent>> events;

        /// <summary>运行态 MPB 闪变条目（由 fxData.rendererSet 在 InitFx 构建），不参与序列化</summary>
        private List<RendererSet> rendererSet = new();

        /// <summary>渲染槽位（每个"渲染器+材质下标"唯一一个共享 MPB），多个条目可命中同一槽位</summary>
        private readonly List<RendererSlot> slots = new();

        /// <summary>是否已提示过"特效条目缺材质"（只提示一次）</summary>
        private bool _warnedNoMaterial;

        /// <summary>上次受击音效时间（受击音效节流）</summary>
        protected float m_lastDamageTime;

        /// <summary>死亡表现已播放（子类可用它停掉持续表现，如移动音效/速度参数）</summary>
        protected bool allowDeath;

        /// <summary>
        /// 构建渲染槽位与特效条目。子类在自己的 Start 里调用一次。
        /// </summary>
        protected virtual void InitFx()
        {
            OnFxInitBegin();

            rendererSet.Clear();
            slots.Clear();
            _warnedNoMaterial = false;

            if (fxData.IsValid() && fxData.rendererSet != null)
            {
                // 由共享配置构建运行态条目（实例私有状态：命中槽位/计时）
                for (int c = 0; c < fxData.rendererSet.Count; ++c)
                {
                    var cfg = fxData.rendererSet[c];
                    if (cfg == null) continue;
                    rendererSet.Add(new RendererSet { Config = cfg });
                }
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    // 同一槽位（渲染器+材质下标）只建一个共享块，命中的条目共用它，避免互相整块覆盖
                    RendererSlot slot = null;
                    for (int u = 0; u < rendererSet.Count; ++u)
                    {
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
                        if (mats[i] != mat) continue;
                        if (slot == null)
                        {
                            slot = new RendererSlot(renderer, i);
                            slots.Add(slot);
                        }
                        rendererSet[u].AddSlot(slot);
                    }
                }

                OnFxRendererReady(renderer);
            }
        }

        /// <summary>状态复位钩子（InitFx 最开始调用）</summary>
        protected virtual void OnFxInitBegin() { }

        /// <summary>每个渲染器扫描完成的钩子（如诞生材质替换）</summary>
        /// <param name="renderer">本次扫描到的渲染器</param>
        protected virtual void OnFxRendererReady(Renderer renderer) { }

        /// <summary>每帧刷新闪变（子类在自己的 Update 里调用）</summary>
        protected void UpdateFx()
        {
            for (int u = 0; u < rendererSet.Count; ++u)
            {
                rendererSet[u].Update();
            }
            // 所有条目写完后再统一回写：一个槽位每帧最多一次 SetPropertyBlock，多条目也不会互相覆盖
            for (int i = 0; i < slots.Count; ++i)
            {
                slots[i].Flush();
            }
        }

        /// <summary>特效条目生效材质：config.material 非空优先（特例覆盖），否则回落单位 fxMaterial（模板条目通常留空）</summary>
        private Material GetFxMaterial(RendererSet rs)
        {
            if (rs == null || rs.Config == null) return fxMaterial;
            return rs.Config.material != null ? rs.Config.material : fxMaterial;
        }

        protected void TriggerRS(OccasionTypeEnum type)
        {
            for (int u = 0; u < rendererSet.Count; ++u)
            {
                rendererSet[u].Trigger(type);
            }
        }

        /// <summary>取某时机的特效配置（读事件 SO fxEvent；未挂 SO/未配置返回 null）</summary>
        protected FxSetConfig GetFxSet(OccasionTypeEnum type)
        {
            if (fxEvent.IsValid() && fxEvent.fxDic.TryGet(type, out var cfg))
            {
                return cfg;
            }
            return null;
        }

        protected void TriggerFX(OccasionTypeEnum type, Vector3 pos, Quaternion roat, Transform parent, bool ignoreAudio = false)
        {
            if (events.TryGet(type, out var item))
            {
                item?.Invoke();
            }
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
                    AudioSvc.PlaySound(new(value.cilp, pos, range: 40, group: AudioGroups.Enemy));
                }
            }
            if (value.ps.IsValid())
            {
                VFXManager.Creat(value.ps.gameObject, pos, roat, parent);
            }
            if (value.trans.IsValid())
            {
                Instantiate(value.trans, pos, transform.rotation, null);
            }
        }

        /// <summary>
        /// 受击表现：闪变 + 命中点特效（音效按 LoginFrame 节流）+ 受击动画触发器。
        /// </summary>
        /// <param name="collider">被命中的碰撞体；为空时表现位置用 fallbackPos</param>
        /// <param name="fallbackPos">没有碰撞体时的表现位置（通常传单位中心或自身位置）</param>
        protected void PlayHitFx(Vector3 pos,Vector3 normal)
        {
            TriggerRS(OccasionTypeEnum.Hit);

            
            //每 LoginFrame 秒最多触发一次音效
            bool ignoreAudio = Time.time < m_lastDamageTime + Constants.LoginFrame.RawFloat;
            if (!ignoreAudio)
            {
                m_lastDamageTime = Time.time;
            }
            TriggerFX(OccasionTypeEnum.Hit, pos, Quaternion.LookRotation(normal), default, ignoreAudio);
            SetTrigger(Constants.k_AnimOnDamagedParameter, true);
        }

        /// <summary>死亡表现：闪变 + 死亡特效 + 关闭激活动画参数 + 死亡触发器</summary>
        /// <param name="pos">死亡表现位置（通常传单位位置）</param>
        protected void PlayDieFx(Vector3 pos)
        {
            allowDeath = true;
            TriggerRS(OccasionTypeEnum.Die);
            TriggerFX(OccasionTypeEnum.Die, pos, Quaternion.identity, null);
            SetBool(Constants.k_AnimIsActiveParameter, false);
            SetTrigger(Constants.k_AnimOnDeathParameter, true);
        }

        //只给血条fx用了，单位的还没，因为一般用不到
        /// <summary>复活表现：特效 + 开启激活动画参数</summary>
        /// <param name="pos">死亡表现位置（通常传单位位置）</param>
        protected void PlayResolveFx(Vector3 pos)
        {
            allowDeath = false;
            TriggerRS(OccasionTypeEnum.Revive);
            TriggerFX(OccasionTypeEnum.Revive, pos, Quaternion.identity, null);
            SetBool(Constants.k_AnimIsActiveParameter, true);
            //SetTrigger(Constants.k_AnimOnDeathParameter, true);
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
