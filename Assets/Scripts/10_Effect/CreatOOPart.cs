using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using FPSGame.Utils;
using FPSGame.Managers;

namespace FPSGame.Effect
{
[AddComponentMenu("创建场景物体/样品")]
public class CreatOOPart : MonoBehaviour
{
    [SerializeField]
    float range, probability=50;

    void Start()
    {
        // ★ 静态同步（2026-10-12）：落点抖动 + 概率都改走**按本生成器坐标派生的流**（见 SeedUtil.NewPlacementStream）——
        //   原来吃全局静态流 RandomUtils / 共享的战斗流 ⇒ 两端落点不同，而生成物在**战斗开始前**创建
        //   ⇒ Actor.Awake 取的是**派生号**（含位置）⇒ 两端号也跟着错（打它/移它会打到别的单位）。
        var rand = FPSGame.Utils.SeedUtil.NewPlacementStream(FPSGame.Data.TaskState.Seed, FPSGame.Utils.SeedStream.PropSpawn, transform.position);

        Vector2 dir = rand != null ? rand.RandomVector2() : RandomUtils.RandomVector2();
        var pos = transform.position + dir.ToVector3() * range;
        var parent = transform.parent;
        var copiedProbability = probability;

        BattleManager.EnqueueInit(() =>
        {
            bool pass = rand != null ? rand.Bool(copiedProbability) : BattleManager.Instance.BattleRandom.Bool(copiedProbability);
            if (pass)
            {
                Instantiate(PropertyManager.Instance.CreatOOPart(), pos, default, parent);
            }
        });

        Destroy(gameObject);
    }
}
}
