using System.Collections.Generic;
using FPSGame.Audio;
using FPSGame.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace FPSGame.Weapon
{
    /// <summary>
    /// 枪身的"过热"表现：材质自发光 + 蒸汽粒子 + 散热音效。
    /// 三者共用一个"危险度"（0 = 冷/满弹，1 = 满热/空弹）：
    /// 热量武器读 <see cref="WeaponBaseController.HeatRatio"/>；没有热量的武器退回按弹匣剩余（保持模板原有表现）。
    /// </summary>
    public class OverheatBehavior : MonoBehaviour
    {
        /// <summary>材质自发光属性名（缓存 ID，避免每帧拼字符串）</summary>
        private int EmissionColorId;

        [Header("视觉")]
        [InspectorName("蒸汽粒子")]
        [Tooltip("发射率随危险度线性上升；不配就跳过蒸汽表现")]
        [FormerlySerializedAs("SteamVfx")]
        [SerializeField]
        private ParticleSystem _steamVfx;

        [InspectorName("满热时的粒子发射率")]
        [FormerlySerializedAs("SteamVfxEmissionRateMax")]
        [SerializeField]
        private float _steamEmissionRateMax = 8f;

        [InspectorName("过热颜色（0=冷 → 1=满热）")]
        [FormerlySerializedAs("OverheatGradient")]
        [GradientUsage(true)] 
        [SerializeField]
        private Gradient _overheatGradient;

        [InspectorName("参与过热变色的材质")]
        [Tooltip("只刷 sharedMaterial 正好是这一份的槽位，别把整把枪都变自发光")]
        [FormerlySerializedAs("OverheatingMaterial")]
        [SerializeField]
        private Material _overheatingMaterial;
        [SerializeField]
        private string _overheatingMaterialName;

        [Header("声音")]
        [InspectorName("散热时循环播放的音效")]
        [FormerlySerializedAs("CoolingCellsSound")]
        [SerializeField]
        private AudioClip _coolingSound;

        [InspectorName("危险度 → 音量")]
        [FormerlySerializedAs("AmmoToVolumeRatioCurve")]
        [SerializeField]
        private AnimationCurve _dangerToVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private WeaponController _weapon;
        private AudioSource _audioSource;
        private MaterialPropertyBlock _propertyBlock;
        private ParticleSystem.EmissionModule _steamEmission;
        private readonly List<(Renderer Renderer, int MaterialIndex)> _overheatSlots = new();
        private float _lastDanger = -1f;

        private void Awake()
        {
            EmissionColorId=Shader.PropertyToID(_overheatingMaterialName);
            _weapon = GetComponent<WeaponController>();
            if (!_weapon)
            {
                Debug.LogError("[过热表现] 同物体上没有 WeaponController，已停用：" + name, this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            CollectOverheatSlots();

            if (_steamVfx)
            {
                _steamEmission = _steamVfx.emission;
                _steamEmission.rateOverTimeMultiplier = 0f;
            }

            if (_coolingSound)
            {
                // 要的只是"挂在 Weapon 混音组 + 3D + loop"，交给音频入口建源，别手写 AddComponent + GetMixGroup
                _audioSource = AudioSvc.CreatSource(gameObject, AudioGroups.Weapon);
                _audioSource.clip = _coolingSound;
                _audioSource.playOnAwake = false;
                _audioSource.volume = 0f;
            }
        }

        private void CollectOverheatSlots()
        {
            if (!_overheatingMaterial) return;

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; ++r)
            {
                var materials = renderers[r].sharedMaterials;
                for (int i = 0; i < materials.Length; ++i)
                {
                    if (materials[i] == _overheatingMaterial) _overheatSlots.Add((renderers[r], i));
                }
            }
        }

        private void Update()
        {
            float danger;
            if (_weapon.HasHeatSystem)
            {
                danger = _weapon.HeatRatio;
            }
            else if (_weapon.Magazine)
            {
                // 没有热量的武器：弹匣越空越"热"，与模板口径一致
                danger = 1f - _weapon.Magazine.ScaleValue.RawFloat;
            }
            else
            {
                return;   // 还没 LogicInit（预览实例）⇒ 这一帧没有可表现的数据
            }

            // 值没变就别写：SetPropertyBlock / 发射率 / 音量都会让渲染与音频状态重算
            if (Mathf.Abs(danger - _lastDanger) < 0.001f) return;
            _lastDanger = danger;

            if (_overheatGradient != null && _overheatSlots.Count > 0)
            {
                _propertyBlock.SetColor(EmissionColorId, _overheatGradient.Evaluate(danger));
                for (int i = 0; i < _overheatSlots.Count; ++i)
                {
                    _overheatSlots[i].Renderer.SetPropertyBlock(_propertyBlock, _overheatSlots[i].MaterialIndex);
                }
            }

            if (_steamVfx) _steamEmission.rateOverTimeMultiplier = _steamEmissionRateMax * danger;

            UpdateCoolingSound(danger);
        }

        private void UpdateCoolingSound(float danger)
        {
            if (!_audioSource) return;

            // ⚠ `InCooling` 只在"自动换弹"分支里被赋值 ⇒ 热量武器上恒为 false，必须用 IsCoolingDown 判"正在散热"
            bool cooling = danger > 0f && _weapon.IsWeaponActive
                           && (_weapon.HasHeatSystem ? _weapon.IsCoolingDown : _weapon.InCooling);

            if (cooling && !_audioSource.isPlaying) _audioSource.Play();
            else if (!cooling && _audioSource.isPlaying) _audioSource.Stop();

            _audioSource.volume = _dangerToVolume != null ? _dangerToVolume.Evaluate(danger) : danger;
        }
    }
}
