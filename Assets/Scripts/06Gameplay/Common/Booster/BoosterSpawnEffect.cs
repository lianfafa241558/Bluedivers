using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Utils;      // Tool.Contains(this LayerMask, int)
using FPSGame.Weapon;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 全队强化 · 生成一个单位（房主唯一生成 + 4063 <c>Kind=Table</c> 复刻；成员端**什么都不做**，等广播）。
    ///
    /// <para>▍**纯组件**：它不认识空投舱、不认识任何宿主。要在什么时候生效，由外部调它的方法：
    /// <list type="bullet">
    ///   <item>绑 <c>AirdropPod.OnLanded</c> → <c>EvaluateNow()</c>（推荐，落地这一次才触发）；</item>
    ///   <item>绑 <c>AirdropPod.OnHit</c> → <see cref="OnGroundHit"/>（撞到单位也会来一发，所以这里要过滤）；</item>
    ///   <item>触发时机选「开打时」则不用绑任何事件（适合摆在场景里的锚点）。</item>
    /// </list></para>
    ///
    /// <para>▍⚠ 生成物**不回收**：4063 只有"生成"没有"撤销"，两端各一份、都不回收才能保持一致；
    /// 生命周期交给生成物自己（<c>LimitedLife</c> 之类）。</para>
    /// </summary>
    [AddComponentMenu("全队强化/生成单位")]
    public class BoosterSpawnEffect : BoosterEffectBehaviour
    {
        [SerializeField, InspectorName("生成的单位")] private GameObject _prefab;
        [SerializeField, InspectorName("相对宿主偏移")] private Vector3 _offset = new Vector3(0f, 0.2f, 0f);
        [SerializeField, InspectorName("沿宿主朝向")] private bool _alignToHost = true;

        /// <summary>可生成单位表的编号：从预制体根的 <see cref="NetObject"/> 取（拖预制体就够，别手填 int）。</summary>
        private int _prefabKey;

        private void Awake()
        {
            if (_prefab == null) return;

            var net = _prefab.GetComponent<NetObject>();
            if (net == null)
            {
                Debug.LogWarning($"[全队强化]「{name}」的「生成的单位」根上没有 NetObject（网络编号）⇒ 联机下创建不出来：{_prefab.name}", this);
                return;
            }
            _prefabKey = net.Id;
        }

        /// <summary>【UnityEvent 入口】命中点是**地面**才算落地，然后按强化条件生成。
        /// <para>用于绑 <c>AirdropPod.OnHit</c>（它连"下落中撞到单位"也会触发）；有 <c>OnLanded</c> 时优先绑那个。</para></summary>
        public void OnGroundHit(ProjectileHitData hit)
        {
            // 落地那一次 collider 为 null（AirdropPod.Land 的 hitData），撞到实物的那次带 collider
            if (hit.collider && !LayerDefinition.GroundLayers.Contains(1 << hit.collider.gameObject.layer)) return;
            EvaluateNow();
        }

        protected override void OnApply()
        {
            if (_prefabKey == 0) return;   // Awake 已经报过（没拖预制体 / 预制体没挂 NetObject）

            var rot = _alignToHost ? transform.rotation : Quaternion.identity;

            // 房主/单机 ⇒ 真生成并广播 4063；成员 ⇒ null（等复刻，本端什么都不做）
            BattleHub.Current.SpawnAt(_prefabKey, transform.TransformPoint(_offset), rot.eulerAngles.y);
        }
    }
}
