using System.Collections.Generic;
using FPSGame.Core;
using UnityEngine;
using FPSGame.Audio;
using FPSGame.Game;
using FPSGame.GameContract;
namespace FPSGame.AI
{
    /// <summary>
    /// 召唤师
    /// </summary>
    [AddComponentMenu("单位/_技能/召唤师", 30)]
    public class UnitSkill_Summoner : UnitSkill_Base, INetSpawner
    {
        public AudioClip cilp;
        [Header("召唤师")]
        [InspectorName("召唤师")]
        public List<KVP<GameObject,Vector3>> summoned;

        protected override void SkillStart()
        {
            var comp = GetComponent<EnemyController>();
            // ⚠ 目标是"本端当前锁定的玩家位置"（两端不同）⇒ 随 4052 一起下发，成员用同一份
            Vector3 target = comp != null ? comp.Target.Pos : default;

            for (int i = 0; i < summoned.Count; ++i)
            {
                // ★ 联机：房主唯一生成；成员这一句返回 null（等房主的 4052 复刻）—— 各生各的会把号段推歪
                var go = BattleHub.Current.CreatUnit(summoned[i].Key, transform.TransformPoint(summoned[i].Value),
                    transform.rotation, this, i, target);
                if (go == null) continue;

                var goComp = go.GetComponent<EnemyController>();
                if (goComp != null) goComp.PatrolPos = comp.PatrolPos;
            }
            Vector3 pos = m_Controller.CenterPos;
            _ = AudioSvc.PlaySound(new(cilp, pos, 30, AudioGroups.Enemy, 1));
        }

        #region 联机 · 房主唯一生成 / 成员复刻（4052）

        /// <summary>登记为"生成器"（⚠ 必须在 OnEnable：复刻出来的单位在 Instantiate 内只跑过 Awake/OnEnable）。</summary>
        private void OnEnable() => SpawnerRegistry.Register(this);

        private void OnDisable() => SpawnerRegistry.Unregister(this);

        /// <summary>生成者身份键（房主上报、成员据此找回本端这一份）。</summary>
        public int SpawnSourceKey => NetSpawnKey.Of(gameObject);

        /// <summary>复刻即建即得，没有"等前置对象"的挂起状态。</summary>
        public bool HasPending => false;

        /// <summary>【成员】按房主下发的槽位/号复刻一只。⚠ 覆盖 <c>NetId</c> 必须早于设目标点。
        /// <para>⚠ <c>PatrolPos</c> 不在消息里（宿主侧只是提示，后续由 4024 的移动意图接管）。</para></summary>
        public GameObject ReplaySpawn(int slot, int netId, Vector3 pos, Quaternion rot, Vector3 target, string prefabId)
        {
            if (summoned == null || slot < 0 || slot >= summoned.Count) return null;

            var go = Instantiate(summoned[slot].Key, pos, rot,
                ActorsManager.Instance != null ? ActorsManager.Instance.transform : null);
            var actor = go.GetComponent<Actor>();
            if (actor != null) actor.NetId = netId;

            var ctrl = go.GetComponent<EnemyController>();
            if (ctrl != null && target.sqrMagnitude > 0.0001f) ctrl.ApplyRemoteDestination(target);
            return go;
        }

        #endregion
    }
}