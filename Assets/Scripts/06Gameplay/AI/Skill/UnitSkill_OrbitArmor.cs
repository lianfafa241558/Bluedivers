using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Weapon;

namespace FPSGame.AI
{
    /// <summary>
    /// 环绕装甲：以单位为中心，每 360/count 度、指定距离处生成装甲单位，
    /// 并将生成单位的 Health 主体注册到主单位 PartController 的无敌装甲列表。
    /// 技能无固定持续时间：无敌期间主单位速度归零，全部装甲被摧毁后技能结束恢复速度。
    /// </summary>
    [AddComponentMenu("单位/_技能/环绕装甲", 30)]
    public class UnitSkill_OrbitArmor : UnitSkill_Base, INetSpawner
    {
        [InspectorName("装甲单位")]
        [SerializeField]
        private List<GameObject> armors;
        [InspectorName("环绕距离")]
        [SerializeField]
        private float orbitDistance = 5f;

        /// <summary>技能是否生效中（装甲尚未全部摧毁）</summary>
        private bool m_IsActive;
        private PartController m_PartController;

        protected override void SkillStart()
        {
            // 无固定持续时间，由"全部装甲被摧毁"事件结束，禁用基类时长倒计时
            nowDurationTime = 0;

            if (m_IsActive) return;
            if (!EnsurePartController()) return;

            int count = armors.Count;
            if (count == 0)
            {
                Debug.LogWarning("技能/环绕装甲: 装甲列表为空", gameObject);
                return;
            }

            // 每 360/count 度环绕排列
            int registered = 0;
            float stepAngle = 360f / count;
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = armors[i];
                if (prefab == null) continue;

                float angle = i * stepAngle * Mathf.Deg2Rad;
                Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitDistance;

                // ★ 联机（2026-10-12）：装甲**也是单位**（自己会从 `Actor.Awake` 领 NetId）⇒ 必须走统一生成入口：
                //   房主（或单机）造 + 上报 4052，成员端拿到 null（不生成、等复刻）—— 否则两端各 Instantiate 一份，
                //   号段立刻错位（其后所有单位错位：伤害打到别的怪、移动指令发错对象）。
                //   ⚠ 成员端那只由 `ReplaySpawn` 建出来，并**自己**注册进无敌体系（见下）。
                GameObject go = FPSGame.GameContract.BattleHub.Current.CreatUnit(prefab, pos, transform.rotation, this, i);
                if (go == null) continue;               // 成员端（等复刻）/ 战斗服务未就位
                if (RegisterArmor(go)) registered++;
            }

            // 造出来至少一只才算开始（无敌期间速度归零）。
            // ⚠ 成员端这里 registered 恒 0（CreatUnit 返回 null）⇒ 由 ReplaySpawn 启动，别在这儿回滚。
            if (registered > 0) BeginArmorPhase();
        }

        /// <summary>懒取主单位的 <see cref="PartController"/>（没有 ⇒ 报一次并放弃）。</summary>
        private bool EnsurePartController()
        {
            if (m_PartController == null) m_PartController = GetComponent<PartController>();
            if (m_PartController == null)
            {
                Debug.LogWarning("技能/环绕装甲: 主单位缺少 PartController，无法注册无敌装甲", gameObject);
                return false;
            }
            return true;
        }

        /// <summary>进入"装甲战"：订阅"全部装甲被摧毁" + 无敌期间主单位速度归零（幂等 ⇒ 成员端由复刻触发）。</summary>
        private void BeginArmorPhase()
        {
            if (m_IsActive) return;
            m_IsActive = true;
            m_Controller.Speed.AddModifier(ModifierType.Factor, 0);
            m_PartController.OnAllInvincibleArmorDestroyed -= OnAllArmorDestroyed;
            m_PartController.OnAllInvincibleArmorDestroyed += OnAllArmorDestroyed;
        }

        /// <summary>把一只装甲单位的主躯干注册为主单位的无敌装甲（没配主躯干 ⇒ false）。</summary>
        private bool RegisterArmor(GameObject armor)
        {
            if (armor == null) return false;

            var health = armor.GetComponent<IHealth>();
            if (health == null || !health.GetMainPart().IsValidMono())
            {
                Debug.LogWarning("技能/环绕装甲: 创建的" + armor.name + "没有配置主体部位(MainPart)", armor);
                return false;
            }

            m_PartController.AddInvincibleArmor(health.GetMainPart() as Damageable);
            return true;
        }

        /// <summary>所有无敌装甲被摧毁：技能结束</summary>
        private void OnAllArmorDestroyed()
        {
            if (!m_IsActive) return;
            m_IsActive = false;
            if (m_PartController != null)
            {
                m_PartController.OnAllInvincibleArmorDestroyed -= OnAllArmorDestroyed;
            }
            SkillEnd();
        }

        protected override void SkillEnd()
        {
            // 无敌结束恢复速度
            m_Controller.Speed.AddModifier(ModifierType.Factor, 1);
        }

        protected override void Uninit()
        {
            if (m_PartController != null)
            {
                m_PartController.OnAllInvincibleArmorDestroyed -= OnAllArmorDestroyed;
            }
        }

        #region INetSpawner（房主唯一生成 / 成员复刻，见 IBattleService.CreatUnit）

        /// <summary>登记为"生成器"（⚠ 必须在 <c>OnEnable</c>：成员端要能在复刻消息到达前就找到它；
        /// 复刻出来的单位在 <c>Instantiate</c> 内只跑过 Awake/OnEnable，Start 还没跑）。</summary>
        private void OnEnable() => SpawnerRegistry.Register(this);

        private void OnDisable() => SpawnerRegistry.Unregister(this);

        /// <summary>生成者身份键 = 宿主单位的号（同 <c>EnemyNestBuild</c>/<c>UnitSkill_Summoner</c> 的口径：
        /// 有号用号、无号退回"身份 + 0.1m 量化位置"哈希，见 <c>NetSpawnKey.Of</c>）。</summary>
        public int SpawnSourceKey => NetSpawnKey.Of(gameObject);

        /// <summary>复刻即建即得，没有"等前置对象"的挂起状态。</summary>
        public bool HasPending => false;

        /// <summary>成员侧复刻房主下发的装甲：<paramref name="slot"/> = 装甲表下标 ⇒ 两端同一把 prefab、同一数量。</summary>
        public GameObject ReplaySpawn(int slot, int netId, Vector3 pos, Quaternion rot, Vector3 target, string prefabId)
        {
            if (slot < 0 || slot >= armors.Count) return null;
            var prefab = armors[slot];
            if (prefab == null) return null;

            var go = Instantiate(prefab, pos, rot,
                ActorsManager.Instance != null ? ActorsManager.Instance.transform : null);
            var actor = go.GetComponent<Actor>();
            if (actor != null) actor.NetId = netId;      // 号随房主来（复刻期间 Actor.Awake 一个号都没领）

            // ★ 成员端也要进"无敌装甲"体系：房主打的是另一份世界，本端不注册的话主单位在本端是"没装甲可打"的
            if (EnsurePartController())
            {
                BeginArmorPhase();                        // 幂等：主单位速度归零 + 订阅"全部装甲被摧毁"
                RegisterArmor(go);
            }
            return go;
        }

        #endregion
    }
}
