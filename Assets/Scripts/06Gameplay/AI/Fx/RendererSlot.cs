using FPSGame.Utils;
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

        /// <summary>
        /// 本槽位的**共享块**（惰性创建）—— 供"长期外观"写入者（如载具皮肤的 <c>BattleApplyVehicleData</c>）使用。
        ///
        /// <para>▍为什么长期外观也必须写进这里：<c>Renderer.SetPropertyBlock(block)</c>（不按下标）与
        /// <c>SetPropertyBlock(block, index)</c> 是**两套**存储；本槽位一旦被受击闪白/溶解按下标 Flush，
        /// 别人写在渲染器级（下标 -1）的属性就被**永久顶掉**（本类注释开头说的就是这个坑，
        /// 2026-10-14 实测后果：机甲空投落地摔一下触发闪白，皮肤从此变回默认）。</para>
        ///
        /// <para>▍用法（外部写入者）：写完后**必须**调 <see cref="MarkDirty"/>（或让本类的
        /// <c>Flush</c> 在下一次刷新时带上它）。</para>
        /// </summary>
        public MaterialPropertyBlock Block
        {
            get
            {
                if (!mpb.IsValid()) mpb = new MaterialPropertyBlock();
                return mpb;
            }
        }

        /// <summary>标记"本槽位有写入，需要回写"（下帧由 <c>Flush</c> 生效）。</summary>
        public void MarkDirty() => dirty = true;

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
