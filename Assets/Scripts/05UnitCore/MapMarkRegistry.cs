using System;
using System.Collections.Generic;
using FPSGame.Core.Interface;
using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 「世界关注物」→ 小地图 的登记处（家具 / 地面武器；将来别的可关注物也走这条）。
    ///
    /// <para>▍为什么不给它们挂 <c>Actor</c>：Actor 是 AI 索敌 / 伤害 / 联机号段的载体
    /// （会进 <c>ActorsManager</c>、可被打、占 NetId 号段）—— 一把躺在地上的枪不该进那套体系；
    /// 这里只登记"位置 + 图标"，代价是一张静态表。</para>
    /// <para>▍为什么不用 <c>BattleEventBus.MissionEnityShow</c>（兴趣点通道）：那条是"发现式、只增不减"，
    /// 物件被拿走 / 销毁后图标会永久留在图上（<c>MiniMapWnd</c> 没有对应的删除路径）。</para>
    /// <para>▍用法：登记方在 <c>OnEnable</c> 调 <see cref="Register"/>、<c>OnDisable</c> 调 <see cref="Unregister"/>，
    /// 让"还在世界上"与组件 enabled 严格一一对应。**按引用计数**登记：多个家具共用同一身份
    /// （如挂在子物体上、身份取父物体的 Actor/BaseObject）时，各自 enable/disable 不会互相摘掉。</para>
    /// </summary>
    public static class MapMarkRegistry
    {
        static readonly List<MapMark> marks = new();

        /// <summary>身份 → 当前有几个登记者（引用计数，见类注释）</summary>
        static readonly Dictionary<IEntity, int> refs = new();

        /// <summary>标记集合增 / 删时派发。界面侧增量同步，不必每帧全量比对。</summary>
        public static event Action OnChanged;

        /// <summary>当前全部标记（只读视图，**不要缓存**：底层列表会被就地增删）。</summary>
        public static IReadOnlyList<MapMark> All => marks;

        /// <summary>登记；同一实体重复登记 = 再 +1 引用（图标不一样则以后来的为准）。实体已销毁时忽略。</summary>
        public static void Register(IEntity entity, Sprite icon)
        {
            if (!entity.IsValidMono()) return;

            if (refs.TryGetValue(entity, out int n))
            {
                refs[entity] = n + 1;
                for (int i = 0; i < marks.Count; i++)
                {
                    if (!ReferenceEquals(marks[i].Entity, entity)) continue;
                    // 图标没变就别惊动界面（重复 OnEnable 不应该重建图标）
                    if (ReferenceEquals(marks[i].Icon, icon)) return;
                    marks[i] = new MapMark(entity, icon);
                    OnChanged?.Invoke();
                    return;
                }
                return;
            }

            refs[entity] = 1;
            marks.Add(new MapMark(entity, icon));
            OnChanged?.Invoke();
        }

        /// <summary>注销；没登记过就什么都不做（与 <see cref="Register"/> 成对调用即可）。</summary>
        public static void Unregister(IEntity entity)
        {
            // ⚠ 必须先挡 null：Dictionary 的 null 键会抛 ArgumentNullException，
            //   而"家具找不到身份组件"（ResolveIdentity 失败）时 Identity 就是 null。
            if (entity == null) return;
            if (!refs.TryGetValue(entity, out int n)) return;

            if (n > 1)
            {
                refs[entity] = n - 1;
                return;
            }

            refs.Remove(entity);
            for (int i = 0; i < marks.Count; i++)
            {
                if (!ReferenceEquals(marks[i].Entity, entity)) continue;
                marks.RemoveAt(i);
                OnChanged?.Invoke();
                return;
            }
        }

        /// <summary>该实体当前是否仍在册（界面清理已注销的点用）。</summary>
        public static bool Contains(IEntity entity) => entity != null && refs.ContainsKey(entity);
    }
}
