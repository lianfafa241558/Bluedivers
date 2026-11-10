using FPSGame.Core;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 全队强化「样本拯救者」· 凯伊护盾：强化命中时**由房主造一个** <see cref="KeiShield"/>，
    /// 成员端什么都不做、等 4063 <c>Kind=Table</c> 复刻（号也随消息来）。
    ///
    /// <para>▍为什么挂在 Kei 根上而不是挂在盾上：盾在成员端是"收到消息才有"的对象，
    /// 它没法自己判断"这次强化有没有生效"；而 Kei 两端都在、时机也一致 ⇒ 由它当**请求方**。</para>
    ///
    /// <para>▍为什么走 <c>SpawnAt(编号)</c> 而不是 <c>CreatUnit(prefab, source, slot)</c>：
    /// 后者的钥匙是"生成者自己"，成员端必须已经存在同一个生成者对象；而护盾属于"凭空该出现一个"，
    /// 用全局表（编号 → prefab）最直接。</para>
    ///
    /// <para>▍⚠ 4063 只有"生成"没有"撤销"：强化生效后不再回收；换场景时随整个战斗场景一起消失。</para>
    /// </summary>
    [AddComponentMenu("全队强化/凯伊护盾")]
    public class KeiShieldEffect : BoosterEffectBehaviour
    {
        [SerializeField, InspectorName("护盾预制体")]
        private GameObject _shieldPrefab;
        [SerializeField, InspectorName("相对凯伊的偏移")]
        private Vector3 _offset = new Vector3(0f, 0.6f, 0f);

        /// <summary>可生成单位表的编号：从预制体根的 <see cref="NetObject"/> 取（拖预制体就够，别手填 int）。</summary>
        private int _prefabKey;

        /// <summary>房主/单机造出来的那一份（成员端恒 null —— 它那份由 4063 复刻，不归这里管）。</summary>
        private GameObject _spawned;

        private void Awake()
        {
            if (_shieldPrefab == null) return;

            var net = _shieldPrefab.GetComponent<NetObject>();
            if (net == null)
            {
                Debug.LogWarning($"[全队强化]「{name}」的「护盾预制体」根上没有 NetObject（网络编号）⇒ 联机下创建不出来：{_shieldPrefab.name}", this);
                return;
            }
            _prefabKey = net.Id;
        }

        protected override void OnApply()
        {
            if (_spawned != null) return;   // 幂等：基类可能因"已就绪"与事件各来一次
            if (_prefabKey == 0) return;    // Awake 已经报过（没拖预制体 / 预制体没挂 NetObject）

            // 房主/单机 ⇒ 真生成并广播 4063；成员 ⇒ null（等复刻，本端什么都不做）
            _spawned = BattleHub.Current.SpawnAt(_prefabKey, transform.position + _offset, transform.eulerAngles.y);
        }
    }
}
