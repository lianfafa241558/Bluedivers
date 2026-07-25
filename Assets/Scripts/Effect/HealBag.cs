using FPSGame.Furn;
using Unity.FPS.Game;
using UnityEngine;

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
                    item.transform.GetComponent<Health>().Heal(100);
                }

            }
        });
    }
}
