using System.Collections.Generic;
using Core;
using UnityEngine;
namespace FPSGame.AI
{
    /// <summary>
    /// 召唤师
    /// </summary>
    [AddComponentMenu("技能/召唤师", 30)]
    public class UnitSkill_Summoner : UnitSkill_Base
    {
        public AudioClip cilp;
        [Header("召唤师")]
        [InspectorName("召唤师")]
        public List<KVP<GameObject,Vector3>> summoned;

        protected override void SkillStart()
        {
            var comp = GetComponent<EnemyController>();
            foreach (var item in summoned)
            {
                var go = Instantiate(item.Key, transform.TransformPoint(item.Value), transform.rotation, transform.parent);
                //var go = BattleManager.Instance.CreatUnit();
                var goComp = go.GetComponent<EnemyController>();
                goComp.SetNavDestination(comp.Target.Pos);
                goComp.PatrolPos = comp.PatrolPos;
            }
            Vector3 pos = m_Controller.CenterPos;
            _ = AudioSvc.PlaySound(new(cilp, pos, 30, AudioGroups.Enemy, 1));
        }
    }
}