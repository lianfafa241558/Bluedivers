using FPSGame.Core.Interface;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 系统消息的统一入口与文案格式（由消息窗 <c>MsgWnd</c> 显示）。
    ///
    /// <para>▍为什么要有这一层：颜色标签与措辞集中一处，调用点只表达语义
    /// 颜色只从 <see cref="Who"/> 来 —— 角色色取 <c>IActor.Color</c>，与玩家 HUD / 小地图上的颜色**同源**。
    /// 落地链仍是 <c>GlobalEventBus.Msg</c> → <c>WndManager</c> → 契约层 <c>WindowRegistry.Msg</c> → 消息窗，
    /// 玩法层不出现任何 UI 类型。</para>
    /// </summary>
    public static class GameMsg
    {
        /// <summary>警示色（击倒等负面结果）。</summary>
        private const string Warn = "#F51111";

        /// <summary>播报里的"是谁"：玩家名 + 该玩家所选角色的颜色。用 <see cref="Of"/> 从单位身份取。</summary>
        public readonly struct Who
        {
            public readonly string Name;
            public readonly Color Color;

            public Who(string name, Color color)
            {
                Name = name;
                Color = color;
            }

            /// <summary>取不到身份时的中性值（名字回落"玩家"、颜色回落白）。</summary>
            public static readonly Who None = new Who(null, Color.white);

            /// <summary>显示用名字（空 ⇒ "玩家"）。</summary>
            public string DisplayName => string.IsNullOrEmpty(Name) ? "玩家" : Name;
        }

        /// <summary>从单位身份取"是谁"（<c>null</c> / 已销毁 ⇒ <see cref="Who.None"/>）。
        ///
        /// <para>▍名字口径：**玩家单位取玩家名**（<see cref="IPlayerUnit.PlayerName"/> —— 敌人 / 兴趣点没有它，
        /// 自然走 <see cref="IEntity.ShowName"/>）。颜色一律取身份上的角色色（<see cref="IEntity.Color"/>），
        /// 与 HUD / 盟友列表上的配色同源。</para></summary>
        public static Who Of(IEntity entity)
        {
            if (!entity.IsValidMono()) return Who.None;

            string name = PlayerNameOf(entity.gameObject);      // 玩家单位 ⇒ 昵称
            return new Who(string.IsNullOrEmpty(name) ? entity.ShowName : name, entity.Color);
        }

        /// <summary>从物体取"是谁"（伤害来源这类手里只有 <c>GameObject</c> 的场合）：自身 → 父物体找身份。</summary>
        public static Who Of(GameObject unit)
        {
            if (!unit) return Who.None;

            var identity = unit.GetComponent<IEntity>();
            if (!identity.IsValidMono()) identity = unit.GetComponentInParent<IEntity>();
            if (identity.IsValidMono()) return Of(identity);

            string name = PlayerNameOf(unit);                   // 连身份组件都没有：至少还给得出玩家名
            return string.IsNullOrEmpty(name) ? Who.None : new Who(name, Color.white);
        }

        /// <summary>播报用的显示名（认不出 ⇒ <c>null</c>，调用方按"省掉这一段"处理）：见 <see cref="Of(GameObject)"/>。</summary>
        public static string NameOf(GameObject unit) => Of(unit).Name;

        /// <summary>玩家名：自身 → 父物体上的 <see cref="IPlayerUnit"/>；非玩家单位 / 认不出 ⇒ <c>null</c>（回落角色名）。</summary>
        private static string PlayerNameOf(GameObject unit)
        {
            if (!unit) return null;
            var player = unit.GetComponent<IPlayerUnit>();
            if (player == null) player = unit.GetComponentInParent<IPlayerUnit>();
            return player != null ? player.PlayerName : null;
        }

        /// <summary>玩家被击倒：<c>玩家名</c>用其角色色，**只有"击倒"两字**用警示色
        /// （<paramref name="source"/> 认不出 ⇒ 省掉"被谁"）。</summary>
        public static void PlayerDown(Who who, string source)
        {
            string by = string.IsNullOrEmpty(source) ? string.Empty : source;
            Push($"<color={Hex(who.Color)}>{who.DisplayName}</color>被{by}<color={Warn}>击倒</color>了");
        }

        /// <summary>发现目标：<c>XX发现黑匣子</c> / <c>XX发现补给箱</c>（任务点与兴趣点共用，整行用触发者颜色）。
        /// <para>▍为什么不写"发现任务点："这类前缀：目标自己的名字（黑匣子 / 补给箱）已经把话说完了。</para></summary>
        public static void Discover(Who who, string target)
            => Push(Tinted(who, $"{who.DisplayName}发现{Fallback(target, "未知目标")}"));

        /// <summary>加入任务：<c>XX加入任务</c>（不带任务名，整行用触发者颜色）。
        /// <para>▍触发点：<c>TeamNetBridge</c> 的**玩家名单变化**（<c>PlayerListSync</c>/4004）—— 有人进房即"加入本局任务"。</para></summary>
        public static void JoinMission(Who who)
            => Push(Tinted(who, $"{who.DisplayName}加入任务"));

        /// <summary>退出任务：<c>XX退出任务</c>（不带任务名，整行用触发者颜色）。
        /// <para>▍触发点：同 <see cref="JoinMission"/>（名单里少了一条）；战斗期名单冻结，改由 <c>PlayerLeftNtf</c>(4029) 报。</para></summary>
        public static void ExitMission(Who who)
            => Push(Tinted(who, $"{who.DisplayName}退出任务"));

        /// <summary>原样推一条（调用方自带富文本标签）。</summary>
        public static void Push(string text) => GlobalEventBus.Msg(text);

        private static string Tinted(Who who, string text) => $"<color={Hex(who.Color)}>{text}</color>";

        /// <summary>颜色 → TMP 的 <c>#RRGGBB</c>。
        /// <para>⚠ 兜底：颜色未配置（<c>(0,0,0,0)</c> 或纯黑）会让整行看不见 ⇒ 回落白色。</para></summary>
        private static string Hex(Color color)
        {
            if (color.a <= 0.01f || (color.r <= 0.01f && color.g <= 0.01f && color.b <= 0.01f)) return "#FFFFFF";
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private static string Fallback(string value, string fallback)
            => string.IsNullOrEmpty(value) ? fallback : value;
    }
}
