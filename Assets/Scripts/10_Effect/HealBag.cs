using FPSGame.Gameplay;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;

namespace FPSGame.Effect
{

/// <summary>
/// 治疗包的表现与治疗效果。
/// </summary>
[AddComponentMenu("特效/治疗包")]
public class HealBag : MonoBehaviour
{
    /// <summary>
    /// 检视器里面调用的
    /// </summary>
    public void HelpPlayer()
    {
        //到目标点了，尝试对着拉人
        ActorsManager.Players.ForEach((item) => {
            if (Vector3.Distance(item.Pos, transform.position) <= 5
                && item.transform.TryGetComponent(out Furniture_PlayerDown furn))
            {
                if (furn.Handle(gameObject))
                {
                    item.transform.GetComponent<IHealth>().Heal(100);
                }

            }
        });
    }
}
}
