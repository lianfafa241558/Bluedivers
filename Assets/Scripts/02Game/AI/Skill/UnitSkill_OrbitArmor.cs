using System.Collections.Generic;
using Core;
using Unity.FPS.Game;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>
    /// 环绕装甲：以单位为中心，每 360/count 度、指定距离处生成装甲单位，
    /// 并将生成单位的 Health 主体注册到主单位 PartController 的无敌装甲列表。
    /// 技能无固定持续时间：无敌期间主单位速度归零，全部装甲被摧毁后技能结束恢复速度。
    /// </summary>
    [AddComponentMenu("技能/环绕装甲", 30)]
    public class UnitSkill_OrbitArmor : UnitSkill_Base
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

            if (m_PartController == null)
            {
                m_PartController = GetComponent<PartController>();
                if (m_PartController == null)
                {
                    Debug.LogWarning("技能/环绕装甲: 主单位缺少 PartController，无法注册无敌装甲", gameObject);
                    return;
                }
            }

            int count = armors.Count;
            if (count == 0)
            {
                Debug.LogWarning("技能/环绕装甲: 装甲列表为空", gameObject);
                return;
            }

            m_IsActive = true;
            // 无敌期间速度归零
            m_Controller.Speed.AddModifier(ModifierType.Factor, 0);
            m_PartController.OnAllInvincibleArmorDestroyed -= OnAllArmorDestroyed;
            m_PartController.OnAllInvincibleArmorDestroyed += OnAllArmorDestroyed;

            // 每 360/count 度环绕排列
            int registered = 0;
            float stepAngle = 360f / count;
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = armors[i];
                if (prefab == null) continue;

                float angle = i * stepAngle * Mathf.Deg2Rad;
                Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitDistance;

                GameObject go = Instantiate(prefab, pos, transform.rotation, transform.parent);

                // 将创建单位的 Health 主体注册为主单位的无敌装甲
                Health health = go.GetComponent<Health>();
                if (health != null && health.MainPart != null)
                {
                    m_PartController.AddInvincibleArmor(health.MainPart);
                    registered++;
                }
                else
                {
                    Debug.LogWarning("技能/环绕装甲: 创建的" + go.name + "没有配置主体部位(MainPart)", go);
                }
            }

            // 没有成功注册任何装甲则回滚，避免永久静止
            if (registered == 0)
            {
                m_IsActive = false;
                m_PartController.OnAllInvincibleArmorDestroyed -= OnAllArmorDestroyed;
                m_Controller.Speed.AddModifier(ModifierType.Factor, 1);
            }
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
    }
}
