using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 可截停空投舱：挂了这个组件的碰撞体，空投舱撞上去算「落地」——会播落地音效、生成内容物。
    ///
    /// <para>▍为什么要有它（2026-11-16）：这件事原本是靠**层**兼职表达的（`Default` 当地面、`Unit` 当单位），
    /// 但单位肢体的碰撞体有一半挂 `Default`、一半挂 `Unit` ⇒ 空投舱会被**一半肢体**误判成落地。
    /// 把「能不能截停」从层语义里拆出来做成显式标记，就不再取决于碰撞体挂在哪个层。</para>
    ///
    /// <para>▍和护盾同款形态：纯标记组件、空类，判定逻辑集中在 <see cref="IsStoppingHit"/> 一处。
    /// 只在撞击那一瞬间读一次（一次下落最多几下），不用缓存。</para>
    /// </summary>
    [AddComponentMenu("单位/可截停空投舱")]
    public class AirdropStopper : MonoBehaviour
    {
        /// <summary>
        /// 这次命中算不算「落地 / 被截停」。
        /// <list type="bullet">
        ///   <item><c>collider == null</c> ⇒ true —— 来自 <c>AirdropPod.Land()</c>，已经到落地高度；</item>
        ///   <item>挂了本组件 ⇒ true —— 显式标记（箱子 / 平台 / 想让舱停住的单位）；</item>
        ///   <item>在 <c>GroundLayers</c> 里且**不是单位部位** ⇒ true —— 兼容原有的「地形/道具挡舱」；</item>
        ///   <item>其余（单位肢体、护盾……）⇒ false —— 舱穿过它继续落，伤害照旧结算。</item>
        /// </list>
        /// </summary>
        public static bool IsStoppingHit(Collider col)
        {
            // Land() 那条：collider 为 null（含已被销毁的假 null，与改造前口径一致）
            if (col == null) return true;

            if (col.GetComponent<AirdropStopper>()) return true;

            if ((LayerDefinition.GroundLayers.value & (1 << col.gameObject.layer)) == 0) return false;

            // ⚠ GroundLayers 里混着 Default(0)，而单位肢体也有一半在 Default ⇒ 必须排除"部位"才算真地面。
            //   护甲片（TransferDamageable）自己不扣血，但同样是部位。
            if (col.GetComponentInParent<Damageable>()) return false;
            if (col.GetComponentInParent<TransferDamageable>()) return false;
            return true;
        }
    }
}
