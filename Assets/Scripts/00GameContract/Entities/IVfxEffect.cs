using FPSGame.Core.Interface;
using UnityEngine;

namespace FPSGame.GameContract
{
    public interface IVfxEffect : IMonoVaild
    {
        public void SetOrigin(GameObject owner, GameObject weaponRoot, Collider target, Vector3 point);

    }
}
