using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>材质槽位信息</summary>
    public struct RendererIndexData
    {
        public Renderer Renderer;
        public int MaterialIndex;

        public RendererIndexData(Renderer renderer, int index)
        {
            Renderer = renderer;
            MaterialIndex = index;
        }
    }

    /// <summary>
    /// 运行态 MPB 闪变条目：由 EnemyControllerFX.InitRS 按共享 EnemyFxData_SO.rendererSet 配置创建。
    /// 配置只读引用共享对象，本类只持有实例私有状态（命中槽位引用、触发计时与 PropertyID 缓存）。不参与序列化。
    /// 颜色写进 RendererSlot 的共享块（同一槽位可能被多个条目写入），由 EnemyControllerFX 帧末统一 Flush。
    /// </summary>
    public class RendererSet
    {
        /// <summary>切换式固定渐变时长（秒）</summary>
        private const float SwitchDuration = 2f;

        /// <summary>共享配置（运行时只读）</summary>
        public RendererSetConfig Config;

        /// <summary>命中的渲染槽位（槽位对象由 EnemyControllerFX 按“渲染器+材质下标”唯一创建，多个条目可共享同一槽位）</summary>
        private readonly List<RendererSlot> slots = new();
        private OccasionTypeEnum lastOccasion;
        private float lastTriggerTime = float.NegativeInfinity;

        /// <summary>渐变是否播放中。播完终点色后置 false，避免终点色被反复写入或停在中途色</summary>
        private bool playing;

        // 颜色属性名运行时缓存（用 bool 标记解析状态，勿用默认值当哨兵）
        private int colorId;
        private bool colorIdResolved;

        /// <summary>登记一个命中本条目的渲染槽位</summary>
        public void AddSlot(RendererSlot slot)
        {
            slots.Add(slot);
        }

        public void Trigger(OccasionTypeEnum occasion)
        {
            switch (Config.type)
            {
                case MPBTypeEnum.Trigger:
                    if (Config.occasion == occasion)
                    {
                        lastTriggerTime = Time.time;
                        playing = true;
                    }
                    break;

                case MPBTypeEnum.Switch:
                    if (Config.occasion == occasion)
                    {
                        if (slots.Count > 0)
                        {
                            lastOccasion = occasion;
                            lastTriggerTime = Time.time;
                            playing = true;
                        }
                    }
                    else if (Config.switchOccasion == occasion && slots.Count > 0)
                    {
                        lastOccasion = occasion;
                        lastTriggerTime = Time.time;
                        playing = true;
                    }
                    break;
            }
        }

        public void Update()
        {
            if (slots.Count == 0 || !playing) return;
            switch (Config.type)
            {
                case MPBTypeEnum.Trigger:
                {
                    // 用 Clamp01 收尾：超时那一帧也必须把终点色写下去。
                    // 否则动画会在中途被“冻结”，当帧颜色永久留在 Renderer 的 PropertyBlock 上，表现为亮斑不消失。
                    float elapsed = Time.time - lastTriggerTime;
                    float progress = Config.duration > 0f ? Mathf.Clamp01(elapsed / Config.duration) : 1f;
                    ApplyColor(Config.gradient.Evaluate(progress));
                    if (elapsed >= Config.duration) playing = false;
                    break;
                }

                case MPBTypeEnum.Switch:
                {
                    // 同上：收敛到终点色后停止，避免停在两种时机之间的中间色
                    float elapsed = Time.time - lastTriggerTime;
                    var a = lastOccasion == Config.switchOccasion ? Config.defaultColor : Config.switchColor;
                    var b = lastOccasion != Config.switchOccasion ? Config.defaultColor : Config.switchColor;
                    ApplyColor(Color.Lerp(a, b, Mathf.Clamp01(elapsed / SwitchDuration)));
                    if (elapsed >= SwitchDuration) playing = false;
                    break;
                }
            }
        }

        /// <summary>把颜色写入本条目命中的所有槽位（帧末由 EnemyControllerFX 统一 Flush 到 Renderer）</summary>
        private void ApplyColor(Color color)
        {
            int id = GetColorId();
            for (int i = 0; i < slots.Count; ++i)
            {
                slots[i].SetColor(id, color);
            }
        }

        private int GetColorId()
        {
            if (!colorIdResolved)
            {
                string name = string.IsNullOrEmpty(Config.colorName)
                    ? (Config.type == MPBTypeEnum.Switch ? "_EmissionColor" : "_HitColor")
                    : Config.colorName;
                colorId = Shader.PropertyToID(name);
                colorIdResolved = true;
            }
            return colorId;
        }
    }
}
