using UnityEngine;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.BaseTool;
using Core;

namespace Unity.FPS.Gameplay
{
    //过热的颜色变化
    public class OverheatBehavior : MonoBehaviour
    {
        [System.Serializable]
        public struct RendererIndexData
        {
            public Renderer Renderer;
            public int MaterialIndex;

            public RendererIndexData(Renderer renderer, int index)
            {
                this.Renderer = renderer;
                this.MaterialIndex = index;
            }
        }

        [Header("视觉效果")]
        [InspectorName("根据弹药比例调整生成率的VFX")]
        public ParticleSystem SteamVfx;

        [Tooltip("完全过热时效果的发射率")]
        public float SteamVfxEmissionRateMax = 8f;

        //将渐变字段设置为HDR
        [GradientUsage(true)]
        [InspectorName("根据弹药比例的过热颜色")]
        public Gradient OverheatGradient;

        [InspectorName("用于过热颜色动画的材质")]
        public Material OverheatingMaterial;

        [Header("声音")]
        [InspectorName("电池冷却时播放的声音")]
        public AudioClip CoolingCellsSound;

        [InspectorName("弹药与音量比例的曲线")]
        public AnimationCurve AmmoToVolumeRatioCurve;


        WeaponController m_Weapon;
        AudioSource m_AudioSource;
        List<RendererIndexData> m_OverheatingRenderersData;
        MaterialPropertyBlock m_OverheatMaterialPropertyBlock;
        float m_LastAmmoRatio;
        ParticleSystem.EmissionModule m_SteamVfxEmissionModule;

        void Awake()
        {
            var emissionModule = SteamVfx.emission;
            emissionModule.rateOverTimeMultiplier = 0f;

            m_OverheatingRenderersData = new List<RendererIndexData>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    if (renderer.sharedMaterials[i] == OverheatingMaterial)
                        m_OverheatingRenderersData.Add(new RendererIndexData(renderer, i));
                }
            }

            m_OverheatMaterialPropertyBlock = new MaterialPropertyBlock();
            m_SteamVfxEmissionModule = SteamVfx.emission;

            m_Weapon = GetComponent<WeaponController>();
            DebugUtility.HandleErrorIfNullGetComponent<WeaponController, OverheatBehavior>(m_Weapon, this, gameObject);

            m_AudioSource = gameObject.AddComponent<AudioSource>();
            m_AudioSource.clip = CoolingCellsSound;
            m_AudioSource.outputAudioMixerGroup = AudioManager.GetMixGroup(AudioGroups.Weapon);
        }

        void Update()
        {
            // visual smoke shooting out of the gun
            float currentAmmoRatio = m_Weapon.Magazine.ScaleValue.RawFloat;
            if (currentAmmoRatio != m_LastAmmoRatio)
            {
                m_OverheatMaterialPropertyBlock.SetColor("_EmissionColor",
                    OverheatGradient.Evaluate(1f - currentAmmoRatio));

                foreach (var data in m_OverheatingRenderersData)
                {
                    data.Renderer.SetPropertyBlock(m_OverheatMaterialPropertyBlock, data.MaterialIndex);
                }

                m_SteamVfxEmissionModule.rateOverTimeMultiplier = SteamVfxEmissionRateMax * (1f - currentAmmoRatio);
            }

            // cooling sound
            if (CoolingCellsSound)
            {
                if (!m_AudioSource.isPlaying
                    && currentAmmoRatio != 1
                    && m_Weapon.IsWeaponActive
                    && m_Weapon.InCooling)
                {
                    m_AudioSource.Play();
                }
                else if (m_AudioSource.isPlaying
                         && (currentAmmoRatio == 1 || !m_Weapon.IsWeaponActive || !m_Weapon.InCooling))
                {
                    m_AudioSource.Stop();
                    return;
                }

                m_AudioSource.volume = AmmoToVolumeRatioCurve.Evaluate(1 - currentAmmoRatio);
            }

            m_LastAmmoRatio = currentAmmoRatio;
        }
    }
}