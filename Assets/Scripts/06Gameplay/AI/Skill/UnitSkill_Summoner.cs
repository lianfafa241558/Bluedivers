using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
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

        /// <summary>本端实际生成的召唤物（母体被骇入时要一起灌满骇入槽）</summary>
        private readonly List<Actor> _summons = new();
        /// <summary>母体的生命组件（听它自己的骇入满槽）</summary>
        private IHealth _health;
        /// <summary>母体的单位（召唤物队伍最终要对齐它）</summary>
        private Actor _self;
        /// <summary>骇入满槽的造成者（召唤物的积蓄槽要记同一个来源）</summary>
        private GameObject _hackerSource;

        protected override void Init()
        {
            base.Init();
            _self = GetComponent<Actor>();
            _health = GetComponent<IHealth>();
            if (_health != null) _health.OnAboStateFullChanged += OnAboStateFullChanged;
        }

        protected override void Uninit()
        {
            if (_health.IsValidMono()) _health.OnAboStateFullChanged -= OnAboStateFullChanged;
            _summons.Clear();
            base.Uninit();
        }

        private void OnAboStateFullChanged(DamageTypeEnum type, GameObject source, bool full)
        {
            if (type != DamageTypeEnum.Hacker || !full) return;

            _hackerSource = source;
            for (int i = _summons.Count - 1; i >= 0; i--)
            {
                var summon = _summons[i];
                if (summon == null)
                {
                    _summons.RemoveAt(i);
                    continue;
                }
                HackerSummon(summon);
            }
        }

        /// <summary>召唤物跟着母体被骇入：积蓄槽灌满 + 队伍改成母体（骇入后）的队伍。
        /// <para>⚠ 召唤物若是炮塔/巢穴，它自己的骇入逻辑是"自毁"，这里照灌不误 —— 口径由各状态机自己定。</para></summary>
        private void HackerSummon(Actor summon)
        {
            var hp = summon.GetComponent<IHealth>();
            if (hp != null) hp.FillAboGauge(DamageTypeEnum.Hacker, _hackerSource);

            // 召唤物自己的骇入逻辑（EnemyMobile_AboState）也会改一次队伍，值相同 ⇒ 幂等
            if (TryResolveHackedTeam(out int team)) summon.Team = team;
        }

        /// <summary>骇入后母体所在的队伍：优先取骇入来源的队伍（倒戈就是加入它的队），
        /// 取不到再退回母体当前 Team。
        /// <para>⚠ 不能用母体 Team 当首选：两个满槽处理器谁先订阅谁先跑，此刻母体可能还没倒戈完。</para></summary>
        private bool TryResolveHackedTeam(out int team)
        {
            if (_hackerSource != null)
            {
                var actor = _hackerSource.GetComponent<Actor>();
                if (actor == null) actor = _hackerSource.GetComponentInParent<Actor>();
                if (actor != null)
                {
                    team = actor.Team;
                    return true;
                }
            }
            if (_self != null)
            {
                team = _self.Team;
                return true;
            }
            team = 0;
            return false;
        }

        /// <summary>登记本端生成的召唤物；母体早已被骇入（技能是冷却制、会反复放）时立刻补灌。</summary>
        private void TrackSummon(GameObject go)
        {
            if (go == null) return;
            var actor = go.GetComponent<Actor>();
            if (actor == null) return;

            _summons.Add(actor);
            if (_health != null && _health.IsAboStateFull(DamageTypeEnum.Hacker))
            {
                HackerSummon(actor);
            }
        }

        protected override void SkillStart()
        {
            var comp = GetComponent<EnemyController>();
            // ⚠ 目标是"本端当前锁定的玩家位置"（两端不同）⇒ 随 4063 一起下发，成员用同一份
            Vector3 target = comp != null ? comp.Target.Pos : default;

            for (int i = 0; i < summoned.Count; ++i)
            {
                // ★ 联机：房主唯一生成；成员这一句返回 null（等房主的 4063 复刻）—— 各生各的会把号段推歪
                var go = BattleHub.Current.CreatUnit(summoned[i].Key, transform.TransformPoint(summoned[i].Value),
                    transform.rotation, this, i, target);
                if (go == null) continue;

                var goComp = go.GetComponent<EnemyController>();
                if (goComp != null) goComp.PatrolPos = comp.PatrolPos;

                TrackSummon(go);
            }
            Vector3 pos = m_Controller.CenterPos;
            _ = AudioSvc.PlaySound(new(cilp, pos, 30, AudioGroups.Enemy, 1));
        }

        #region 联机 · 房主唯一生成 / 成员复刻（4063）

        /// <summary>登记为"生成器"（⚠ 必须在 OnEnable：复刻出来的单位在 Instantiate 内只跑过 Awake/OnEnable）。</summary>
        private void OnEnable() => SpawnerRegistry.Register(this);

        private void OnDisable() => SpawnerRegistry.Unregister(this);

        /// <summary>生成者身份键（房主上报、成员据此找回本端这一份）。</summary>
        public int SpawnSourceKey => NetSpawnKey.Of(gameObject);

        /// <summary>复刻即建即得，没有"等前置对象"的挂起状态。</summary>
        public bool HasPending => false;

        /// <summary>【成员】按房主下发的槽位/号复刻一只。⚠ 覆盖 <c>NetId</c> 必须早于设目标点。
        /// <para>⚠ <c>PatrolPos</c> 不在消息里（宿主侧只是提示，后续由 4052 的移动意图接管）。</para></summary>
        public GameObject ReplaySpawn(int slot, int netId, Vector3 pos, Quaternion rot, Vector3 target, string prefabId)
        {
            if (summoned == null || slot < 0 || slot >= summoned.Count) return null;

            var go = Instantiate(summoned[slot].Key, pos, rot,
                ActorsManager.Instance != null ? ActorsManager.Instance.transform : null);
            var actor = go.GetComponent<Actor>();
            if (actor != null) actor.NetId = netId;

            var ctrl = go.GetComponent<EnemyController>();
            if (ctrl != null && target.sqrMagnitude > 0.0001f) ctrl.ApplyRemoteDestination(target);
            TrackSummon(go);   // 成员端这份副本也要能被母体骇入继承
            return go;
        }

        #endregion
    }
}