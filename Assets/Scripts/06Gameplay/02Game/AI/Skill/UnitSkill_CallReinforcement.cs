using FPSGame.Core;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Audio;
namespace FPSGame.AI
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
            if (FPSGame.GameContract.ServiceLocator.Battle.CreatWave(WaveCreateParams.Default.Set(pos)))
            {
                _ = AudioSvc.PlaySound(new(cilp, pos, 60, AudioGroups.Enemy, 1));
                FPSGame.GameContract.ServiceLocator.Vfx.Creat(ps, pos);
            }
        }



    }

}