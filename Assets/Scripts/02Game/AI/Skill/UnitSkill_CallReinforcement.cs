using Core;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
namespace Unity.FPS.AI
{
    /// <summary>
    /// 有这个的单位可以拉烟
    /// </summary>
    [AddComponentMenu("技能/拉烟", 30)]
    public class UnitSkill_CallReinforcement : UnitSkill_Base
    {

        public AudioClip cilp;
        public GameObject ps;

        protected override void SkillStart()
        {
            Vector3 pos = m_Controller.CenterPos;
            if (BattleManager.Instance.CreatWave(WaveCreateParams.Default.Set(pos)))
            {
                _ = AudioManager.PlaySound(new(cilp, pos, 60, AudioGroups.Enemy, 1));
                VFXManager.Creat(ps, pos);
            }
        }



    }

}