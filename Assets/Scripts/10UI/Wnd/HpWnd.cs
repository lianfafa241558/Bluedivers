using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;

    /// <summary>
    /// 敌人血条的创建与管理窗口。
    /// </summary>
    [AddComponentMenu("UI/窗口/血条管理")]
    public class HpWnd : Window
    {
        /// <summary>敌人距离玩家超过该距离时，受击不再显示血条</summary>
        private const float HpBarMaxDistance = 70f;

        [SerializeField]
        HpItemBase SoilderPrefab, BossPrefab;

        Dictionary<GameObject, HpItemBase> dic=new();
        AutoObjectPool<GameObject, HpItemBase> pool;

        /// <summary>Update 里"要摘掉的 Boss 条"的暂存（不能边遍历 dic 边改它）。</summary>
        readonly List<GameObject> _scrap = new();


        protected override void FirstShowWnd()
        {


            pool = new(
                (item) =>//更新
                {
                    if (!item.CanRecycle())
                    {
                        item.Tick();
                        return true;
                    }
                    else
                    {
                        item.End();
                        return false;
                    }
                },
                () =>//添加
                {
                    var item = Instantiate(SoilderPrefab, transform);
                    SetActive(item,false);
                    return item;
                },
                (item) =>//释放
                {
                    SetActive(item,false);
                },
            5);

        }

        protected override void ShowWnd()
        {

            UnitEventBus.OnEnemyCreate += OnEnemyCreate;
            UnitEventBus.OnEnemyDead += OnEnemyDead;
            UnitEventBus.OnUnitHit += OnUnitHit;
        }

        protected override void HideWnd()
        {
            UnitEventBus.OnEnemyCreate -= OnEnemyCreate;
            UnitEventBus.OnEnemyDead -= OnEnemyDead;
            UnitEventBus.OnUnitHit -= OnUnitHit;
        }
        private void Update()
        {
            pool.Update();

            // 字典里的 Boss 条按"单位还在不在"收尾（口径同池里的小兵条 CanRecycle）。
            // ⚠ 不能只靠 OnEnemyDead：单位被**外部移除**时不一定会再走那条事件 ——
            //    实测火车到站逃走时是 actor.RemoveFlag(Boss) 后再 Kill（见 MissionInterceptTrain.DetachTrain），
            //    以及载具被连带销毁这类，血条会永远停在屏幕上（2026-10-20）。
            if (dic.Count > 0)
            {
                _scrap.Clear();
                foreach (var kv in dic)
                {
                    var item = kv.Value;
                    if (item == null)
                    {
                        _scrap.Add(kv.Key);      // 条自己被销毁了 ⇒ 只摘键
                        continue;
                    }
                    if (item.CanRecycle())
                    {
                        item.End();
                        Tool.Destroy(item.gameObject, 1);
                        _scrap.Add(kv.Key);
                        continue;
                    }
                    item.Tick();
                }
                for (int i = 0; i < _scrap.Count; ++i) dic.Remove(_scrap[i]);
            }
        }



        private void OnEnemyCreate(Actor go)
        {
            //var enemy = go.GetComponent<EnemyController>();
            if (go.HasFlag( ActorFlag.Boss))
            {
                var prefab = Instantiate(BossPrefab, transform.GetChild(0));
                dic.Add(go.gameObject, prefab);
                prefab.Set(go.gameObject);
            }
        }

        private void OnEnemyDead(Actor go)
        {
            // ⚠ 别再用 go.HasFlag(ActorFlag.Boss) 当门槛：拦截火车到站逃走会先 actor.RemoveFlag(Boss) 再 Kill
            //   （车头还得算"逃走"而不是"被摧毁"）⇒ 那时再判旗标就会漏收尾，血条永远不消失（2026-10-20 实测）。
            //   dic 里有的就是"给它建过 Boss 条"的那一只，与旗标无关。
            if (dic.TryGetValue(go.gameObject, out var item))
            {
                item.End();
                Tool.Destroy(item.gameObject,1);
                dic.Remove(go.gameObject);
            }
        }

        private void OnUnitHit(GameObject victim ,GameObject attacker)
        {
            // Boss 走固定的 HUD 条：任何命中都刷新显示并重置隐藏计时，不受 70m 距离限制
            // （放在最前也避免 Boss 的 UseHpBar 为 true 时在 pool 里再生成一条士兵血条）
            if (dic.TryGetValue(victim, out var boss))
            {
                boss.Refresh();
                return;
            }

            victim.TryGetComponent(out Actor victimActor);
            // ⚠ attacker 可能为 null：持续伤害（燃烧/流血 tick）没有"直接来源"，Health 会原样发 null ⇒
            //    直接 TryGetComponent 会 NRE（每 tick 抛一次）。null 时 attackerActor 保持 null，
            //    IsEnemyTooFar 会按"视为超距"处理，与原有 attackerActor==null 分支一致。
            Actor attackerActor = null;
            if (attacker) attacker.TryGetComponent(out attackerActor);
            //距离玩家超过 70m 的敌人受击时不响应血条
            if (IsEnemyTooFar(victimActor, attackerActor)) return;

            if (pool.TryFind(victim, out var item))
            {
                item.Refresh();
            }
            else if(victimActor!=null&&victimActor.UseHpBar)
            {
                pool.Get(victim).Set(victim);
            }
        
        }

        /// <summary>受击单位是否为距离玩家超过 <see cref="HpBarMaxDistance"/>
        /// 如果伤害来源是玩家，那么不管距离多远都显示血条
        /// true则视为超过</summary>
        private static bool IsEnemyTooFar(Actor victimActor, Actor attackerActor)
        {
            if (victimActor == null|| attackerActor==null) return true;
            var player = ActorsManager.Player;
            if (player == null || !player.IsValidMono()) return true;
            if (player as Actor == attackerActor) return false;
            return Vector3.Distance(player.Pos, victimActor.Pos) > HpBarMaxDistance;
        }

    }
}
