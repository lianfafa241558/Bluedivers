using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using FPSGame.Utils;
using FPSGame.Managers;

namespace FPSGame.Effect
{

[AddComponentMenu("创建场景物体/场景物品")]
public class CreatSupple : MonoBehaviour
{
    [SerializeField]
    float range, probability=50;
    [SerializeField]
    GameObject[] prefabs;

    void Start()
    {
        // ★ 静态同步（2026-10-12）：落点 / 概率 / 抽哪个 prefab 全改走**按本生成器坐标派生的流** ——
        //   理由同 CreatOOPart（生成物在战斗开始前创建 ⇒ 取派生号 ⇒ 落点一分叉号就错）。
        var rand = FPSGame.Utils.SeedUtil.NewPlacementStream(FPSGame.Data.TaskState.Seed, FPSGame.Utils.SeedStream.PropSpawn, transform.position);

        Vector2 dir = rand != null ? rand.RandomVector2() : RandomUtils.RandomVector2();
        var pos = transform.position + dir.ToVector3() * range;
        var parent = transform.parent;
        var copiedProbability = probability;
        //Debug.LogError("自注册");
        BattleManager.EnqueueInit(() =>
        {
            bool pass = rand != null ? rand.Bool(copiedProbability) : BattleManager.Instance.BattleRandom.Bool(copiedProbability);
            if (pass)
            {
                Instantiate(rand != null ? prefabs.RandomTake(rand) : prefabs.RandomTake(BattleManager.Instance.BattleRandom), pos, default, parent);
            }
        });

        Destroy(gameObject);
    }
}
}
