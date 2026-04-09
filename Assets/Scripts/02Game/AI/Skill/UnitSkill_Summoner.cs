using System.Collections;
using System.Collections.Generic;
using Core;
using Unity.BaseTool;
using Unity.FPS.AI;
using UnityEngine;
namespace Unity.FPS.AI
{
    /// <summary>
    /// 召唤者
    /// </summary>
    [AddComponentMenu("技能/召唤者", 30)]
    public class UnitSkill_Summoner : UnitSkill_Base
    {
        public AudioClip cilp;
        [Header("召唤物")]
        [CustomLabel("召唤物")]
        public List<KVP<GameObject,Vector3>> summoned;

        protected override void SkillStart()
        {
            foreach (var item in summoned)
            {
                var go = Instantiate(item.Key, transform.TransformPoint(item.Value), transform.rotation, transform.parent);
                //var go = BattleManager.Instance.CreatUnit();
                go.GetComponent<EnemyController>().SetNavDestination(GetComponent<EnemyController>().Target.Pos);
            }
            Vector3 pos = m_Controller.CenterPos;
            _ = AudioManager.PlaySound(new(cilp, pos, 30, AudioGroups.Enemy, 1));
        }
    }
}