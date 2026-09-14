using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>
    /// 单个渲染槽位（某 Renderer 的某材质下标）的共享 MaterialPropertyBlock。
    /// Renderer.SetPropertyBlock 是“整块替换并拷贝存到 Renderer 上”的语义：同一槽位若有多个 RendererSet 条目
    /// （如 _HitColor 受击闪白 + _DissolveValue 溶解）各自持块写入，后写的会把前一个条目设的属性整块覆盖掉。
    /// 因此同一槽位只允许存在一个块，由所有命中该槽位的条目共同写入，帧末由 EnemyControllerFX 统一 Flush 一次。
    /// 块内属性会跨帧保留，条目必须在自己动画收尾时把属性写回“无效果值”（如 _HitColor 写黑）。
    /// </summary>
    public class RendererSlot
    {
        /// <summary>目标渲染器</summary>
        public readonly Renderer Renderer;

        /// <summary>目标材质下标（对应 Renderer.SetPropertyBlock 的 materialIndex）</summary>
        public readonly int MaterialIndex;

        private MaterialPropertyBlock mpb;
        private bool dirty;

        public RendererSlot(Renderer renderer, int materialIndex)
        {
            Renderer = renderer;
            MaterialIndex = materialIndex;
        }

        /// <summary>写入一条颜色属性，本帧末由 Flush 统一生效</summary>
        public void SetColor(int propertyId, Color color)
        {
            if (!Renderer.IsValid()) return;
            if (!mpb.IsValid()) mpb = new MaterialPropertyBlock();
            mpb.SetColor(propertyId, color);
            dirty = true;
        }

        /// <summary>把累积的属性块写回渲染器（本帧无写入或渲染器已销毁则跳过）</summary>
        public void Flush()
        {
            if (!dirty) return;
            dirty = false;
            if (!Renderer.IsValid()) return;
            Renderer.SetPropertyBlock(mpb, MaterialIndex);
        }
    }
}
