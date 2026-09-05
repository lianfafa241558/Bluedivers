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
    /// 配置只读引用共享对象，本类只持有实例私有状态（MPB、材质槽位匹配结果、触发计时与 PropertyID 缓存）。不参与序列化。
    /// </summary>
    public class RendererSet
    {
        /// <summary>共享配置（运行时只读）</summary>
        public RendererSetConfig Config;

        private MaterialPropertyBlock mpb;
        private readonly List<(Renderer, int)> renderers = new();
        private OccasionTypeEnum lastOccasion;
        private float lastTriggerTime = float.NegativeInfinity;

        // 颜色属性名运行时缓存（用 bool 标记解析状态，勿用默认值当哨兵）
        private int colorId;
        private bool colorIdResolved;

        public void Add(Renderer renderer, int materialIndex)
        {
            renderers.Add((renderer, materialIndex));
            if (!mpb.IsValid()) mpb = new MaterialPropertyBlock();
        }

        public void Trigger(OccasionTypeEnum occasion)
        {
            switch (Config.type)
            {
                case MPBTypeEnum.Trigger:
                    if (Config.occasion == occasion)
                    {
                        lastTriggerTime = Time.time;
                    }
                    break;

                case MPBTypeEnum.Switch:
                    if (Config.occasion == occasion)
                    {
                        if (mpb.IsValid())
                        {
                            lastOccasion = occasion;
                            lastTriggerTime = Time.time;
                        }
                    }
                    else if (Config.switchOccasion == occasion && mpb.IsValid())
                    {
                        lastOccasion = occasion;
                        lastTriggerTime = Time.time;
                    }
                    break;
            }
        }

        public void Update()
        {
            if (!mpb.IsValid()) return;
            switch (Config.type)
            {
                case MPBTypeEnum.Trigger:
                    if ((Time.time - lastTriggerTime) <= Config.duration)
                    {
                        float progress = Config.duration > 0 ? (Time.time - lastTriggerTime) / Config.duration : 1f;
                        Color currentColor = Config.gradient.Evaluate(progress);
                        mpb.SetColor(GetColorId(), currentColor);
                        ApplyToRenderers();
                        mpb.Clear();
                    }
                    break;

                case MPBTypeEnum.Switch:
                    if ((Time.time - lastTriggerTime) <= 2)
                    {
                        var a = lastOccasion == Config.switchOccasion ? Config.defaultColor : Config.switchColor;
                        var b = lastOccasion != Config.switchOccasion ? Config.defaultColor : Config.switchColor;
                        Color currentColor = Color.Lerp(a, b, (Time.time - lastTriggerTime) / 2);
                        mpb.SetColor(GetColorId(), currentColor);
                        ApplyToRenderers();
                        mpb.Clear();
                    }
                    break;
            }
        }

        private void ApplyToRenderers()
        {
            for (int i = 0; i < renderers.Count; ++i)
            {
                renderers[i].Item1.SetPropertyBlock(mpb, renderers[i].Item2);
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
