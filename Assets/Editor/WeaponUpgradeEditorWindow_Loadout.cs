using System.Collections.Generic;

using PEMaths;
using UnityEditor;
using UnityEngine;
using FPSGame.EditorExt;
using FPSGame.GameData;
using FPSGame.Weapon;

namespace FPSGame.Game.Editor
{
    /// <summary>
    /// 评分页的「改装模拟」：一排 6 张卡片（5 档升级 + 1 个模组），
    /// 卡片上画图标 + 名称 + 效果摘要，点一下弹 <see cref="SOPickerPopup{T}"/> 选（和升级/模组页同一个选择框）。
    /// <para>▍一套状态对应一把武器：当前武器用 <c>_loadout</c>，对比武器用 <c>_compareLoadout</c>
    /// （<see cref="LoadoutState"/> 自己记 owner，换武器自动清空选择）。</para>
    /// <para>▍口径：选中的 <c>WeaponUpgradeData_SO</c> / <c>WeaponModuleData_SO</c> 的 <c>modifys</c>
    /// 逐条作用到一份 <see cref="WeaponCfg"/> 副本上（<c>Final = (base + ΣBase) × (1 + ΣFactor) + ΣExtra</c>），
    /// 再用副本替掉 <c>weapon.cfg</c> 跑分，算完立刻还原 —— 不动资产、不跑 <c>InitAttribute</c>。</para>
    /// <para>▍不模拟的东西：<c>Special</c>（专有机制）只认数值，玩法效果（切弹种/全自动等）不参与评分。</para>
    /// </summary>
    public partial class WeaponUpgradeEditorWindow
    {
        /// <summary>升级卡片数：prefab 的 <c>Upgrade</c> 实测最多 5 档（星辰脉冲/逻辑/皇室红茶），第 6 张留给模组。</summary>
        private const int LoadoutUpgradeSlots = 5;
        private const int LoadoutSlotCount = LoadoutUpgradeSlots + 1;

        private const float LoadoutCardHeight = 66f;
        private const float LoadoutCardGap = 3f;
        /// <summary>图标在卡片左侧独占两行高度（名称 + 效果），所以给到 40。</summary>
        private const float LoadoutIconSize = 40f;
        /// <summary>评级文字列宽（固定住，卡片行的可用宽度才好算；再窄长文案会被裁）。</summary>
        private const float LoadoutLabelWidth = 120f;

        private readonly LoadoutState _loadout = new();
        private readonly LoadoutState _compareLoadout = new();

        /// <summary>一把武器的改装选择（当前武器 / 对比武器各一份）。</summary>
        private sealed class LoadoutState
        {
            public WeaponPlayerController Owner;
            public SerializedObject Serialized;
            public readonly int[] Select = { -1, -1, -1, -1, -1 };   // 每档选中的选项下标，-1 = 不选
            public int Module;                                      // 选中的模组下标（0 = 空模组）

            /// <summary>绑定到某把武器；换了武器就把选择清空（档位表不同，旧下标没有意义）。</summary>
            public void Bind(WeaponPlayerController weapon, SerializedObject serialized)
            {
                Serialized = serialized;
                if (Owner == weapon) return;
                Owner = weapon;
                for (var i = 0; i < Select.Length; i++) Select[i] = -1;
                Module = 0;
            }
        }

        /// <summary>评分要用的改装增量（空 = 按原始数据评）。</summary>
        private sealed class LoadoutMods
        {
            public readonly List<ModifyAttrData> Items = new();
            /// <summary>专有属性（Special）累计值：不进 cfg，但会影响评分（如「穿透单位」）。
            /// 运行时由 <c>WeaponUniqueAttributeApplier</c> 按 value&gt;0 写回 BulletFlag 等。</summary>
            public readonly Dictionary<string, float> Specials = new();
            public int SelectedCount;   // 选了几档升级 + 是否装了模组（只用于文案）
            public string Text;         // 摘要："攻击强化 + WMD_xxx"
            public bool IsEmpty => Items.Count == 0 && Specials.Count == 0;

            /// <summary>这个专有属性是否被选中的改装点开（value &gt; 0）。</summary>
            public bool HasSpecial(string name)
                => !string.IsNullOrEmpty(name) && Specials.TryGetValue(name, out var value) && value > 0f;
        }

        // ========== 选择状态 ==========

        /// <summary>prefab 的 <c>Upgrade</c>（私有序列化字段）⇒ 每档的选项列表；空档位是空列表。</summary>
        private static List<List<WeaponUpgradeData_SO>> GetLoadoutTiers(SerializedObject serialized)
        {
            var tiers = new List<List<WeaponUpgradeData_SO>>();
            var upgradeProp = serialized?.FindProperty("Upgrade");
            if (upgradeProp == null) return tiers;

            for (var i = 0; i < upgradeProp.arraySize; i++)
            {
                var options = new List<WeaponUpgradeData_SO>();
                var listProp = upgradeProp.GetArrayElementAtIndex(i).FindPropertyRelative("Value");
                if (listProp != null)
                {
                    for (var j = 0; j < listProp.arraySize; j++)
                    {
                        if (listProp.GetArrayElementAtIndex(j).objectReferenceValue is WeaponUpgradeData_SO so && so != null)
                        {
                            options.Add(so);
                        }
                    }
                }
                tiers.Add(options);
            }
            return tiers;
        }

        private static WeaponModuleData_SO GetSelectedModule(LoadoutState state)
        {
            var modules = state.Owner != null ? state.Owner.Modules : null;
            return modules != null && state.Module > 0 && state.Module < modules.Count
                ? modules[state.Module]
                : null;
        }

        /// <summary>当前选择的改装增量（每次重算，改装项本身是 SO 引用，读起来很便宜）。</summary>
        private LoadoutMods GetLoadoutMods(LoadoutState state, WeaponPlayerController weapon, SerializedObject serialized)
        {
            state.Bind(weapon, serialized);
            var mods = new LoadoutMods();
            if (weapon == null) return mods;

            var tiers = GetLoadoutTiers(state.Serialized);
            var names = new List<string>(LoadoutSlotCount);

            for (var i = 0; i < tiers.Count && i < LoadoutUpgradeSlots; i++)
            {
                var select = state.Select[i];
                if (select < 0 || select >= tiers[i].Count) continue;
                var upgrade = tiers[i][select];
                if (upgrade == null) continue;
                AppendMods(mods, upgrade.modifys);
                mods.SelectedCount++;
                names.Add(upgrade.name);
            }

            var module = GetSelectedModule(state);
            if (module != null)
            {
                AppendMods(mods, module.modifys);
                mods.SelectedCount++;
                names.Add(module.name);
            }

            mods.Text = string.Join(" + ", names);
            return mods;
        }

        /// <summary>
        /// 拆改装项：数值类进 <c>Items</c>（会叠到 cfg 副本），<c>Special</c> 进 <c>Specials</c>
        /// （它们不进 cfg，但会影响评分，比如「穿透单位」决定清杂的目标数）。
        /// <para>⚠ 别再把 Special 直接丢掉：那样装了「贯穿弹头」评分一点反应都没有。</para>
        /// </summary>
        private static void AppendMods(LoadoutMods mods, List<ModifyAttrData> source)
        {
            if (source == null) return;
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (item.type != WeaponAttrType.Special)
                {
                    mods.Items.Add(item);
                    continue;
                }

                var key = item.name ?? "";
                mods.Specials.TryGetValue(key, out var oldValue);
                mods.Specials[key] = oldValue + item.value;
            }
        }

        // ========== 属性副本 ==========

        /// <summary>
        /// 造一份"装了这些改装"的 cfg：先原样复制 prefab 的 <c>_fakeAttrs</c>（那里存的是 prime，不含 modifier），
        /// 再把 modifys 逐条 <c>AddModifier</c> 上去。
        /// </summary>
        private WeaponCfg BuildLoadoutCfg(WeaponPlayerController weapon, SerializedObject serialized, LoadoutMods mods)
        {
            var cfg = new WeaponCfg();
            cfg.Init();     // 先把属性字典建出来，后面的 Add / 索引才不 NRE

            var attrsProp = serialized?.FindProperty("cfg")?.FindPropertyRelative("_fakeAttrs");
            if (attrsProp != null)
            {
                for (var i = 0; i < attrsProp.arraySize; i++)
                {
                    var item = attrsProp.GetArrayElementAtIndex(i);
                    var key = (WeaponAttrType)item.FindPropertyRelative("Key").intValue;
                    var value = item.FindPropertyRelative("Value").floatValue;
                    if (cfg[key] == null) cfg.Add(key, value);
                }
            }

            var damages = weapon.Damages;
            var data = damages != null && damages.Count > 0
                ? damages[Mathf.Clamp(weapon.UseDamageIndex, 0, damages.Count - 1)]
                : null;

            for (var i = 0; i < mods.Items.Count; i++)
            {
                var mod = mods.Items[i];
                var attr = cfg[mod.type];
                // prefab 的 cfg 里没有这个键 ⇒ 补一条基线，否则 Factor 会乘在 0 上（升级看起来没效果）
                if (attr == null) attr = cfg.Add(mod.type, MissingBaseValue(data, mod.type));
                if (attr == null) continue;     // 属性没在 attributeConfigs 注册（工厂会报错），跳过
                attr.AddModifier(mod.modifier, new PEInt(mod.value));
            }
            return cfg;
        }

        /// <summary>
        /// cfg 缺键时的基线值：伤害/弹速/穿甲/最大射程这类是运行时 <c>InitAttribute</c> 从 <c>Damages</c> 补进来的，
        /// prefab 上看不到 ⇒ 拿 <see cref="DamageData.GetAttr"/> 的原值当 base，与运行时口径一致。
        /// </summary>
        private static float MissingBaseValue(DamageData data, WeaponAttrType type)
        {
            var value = data != null ? data.GetAttr(type) : 0f;
            switch (type)
            {
                // 运行时是"常量注入 1"而不是从 Damages 取（WeaponPlayerController.InitAttribute），
                // 缺键时按 0 起步会让 Factor 型改装直接归零
                case WeaponAttrType.Recoil:
                case WeaponAttrType.NoiseScale:
                case WeaponAttrType.BulletsPerShot:
                    return Mathf.Max(1f, value);
                default:
                    return value;
            }
        }

        // ========== 一排卡片 ==========

        /// <summary>6 张卡片从左到右平铺：前 5 张是升级档、最后一张是模组。</summary>
        private void DrawLoadoutStrip(LoadoutState state, float availableWidth)
        {
            var tiers = GetLoadoutTiers(state.Serialized);
            var cardWidth = Mathf.Clamp(
                (availableWidth - (LoadoutSlotCount - 1) * LoadoutCardGap) / LoadoutSlotCount, 60f, 200f);

            using (new EditorGUILayout.HorizontalScope())
            {
                for (var slot = 0; slot < LoadoutSlotCount; slot++)
                {
                    if (slot > 0) GUILayout.Space(LoadoutCardGap);
                    DrawLoadoutCard(state, slot, cardWidth, tiers);
                }
            }
        }

        private void DrawLoadoutCard(LoadoutState state, int slot, float width, List<List<WeaponUpgradeData_SO>> tiers)
        {
            var isModuleSlot = slot >= LoadoutUpgradeSlots;
            var rect = GUILayoutUtility.GetRect(width, LoadoutCardHeight,
                GUILayout.Width(width), GUILayout.Height(LoadoutCardHeight));

            Sprite icon = null;
            var title = "";
            var effect = "";
            var stateText = "";
            Color effectColor = new(0.62f, 0.88f, 0.62f);
            var tip = "";
            var selected = false;
            var hasContent = true;

            if (isModuleSlot)
            {
                var modules = state.Owner != null ? state.Owner.Modules : null;
                var module = GetSelectedModule(state);
                if (modules == null || modules.Count <= 1)
                {
                    hasContent = false;
                    title = "—";
                }
                else if (module != null)
                {
                    selected = true;
                    icon = module.icon;
                    title = module.name;
                    effect = EffectSummary(module.modifys, 2);   // 卡片窄，只留前两条（完整内容在 tooltip/列表里）
                    effectColor = EffectColor(module.modifys);
                    stateText = "已选 · 右键清空";
                    tip = $"{module.typeName}\n{DescribeModule(module)}";
                }
                else
                {
                    title = "模组";
                    effect = "未装";
                    stateText = $"可选 {modules.Count - 1} 个";
                    tip = "点一下挑一个模组（右键清空）";
                }
            }
            else
            {
                var options = slot < tiers.Count ? tiers[slot] : null;
                if (options == null || options.Count == 0)
                {
                    hasContent = false;
                    title = "—";
                }
                else
                {
                    var select = state.Select[slot];
                    if (select >= 0 && select < options.Count && options[select] != null)
                    {
                        var upgrade = options[select];
                        selected = true;
                        icon = upgrade.icon;
                        title = upgrade.name;
                        effect = GetEffectSummary(upgrade, 2);
                        effectColor = GetEffectColor(upgrade);
                        stateText = "已选 · 右键清空";
                        tip = $"{upgrade.type}\n{upgrade.desc}";
                    }
                    else
                    {
                        title = $"升级{slot + 1}";
                        effect = $"{options.Count} 选 1";
                        stateText = $"解锁 Lv.{TierLevel(state.Serialized, slot)}";
                        tip = $"第 {slot + 1} 档（解锁等级 {TierLevel(state.Serialized, slot)}）：点一下挑（右键清空）";
                    }
                }
            }

            // 背景：选中偏绿、空档压暗，和左侧升级/模组卡片同一套样式
            if (Event.current.type == EventType.Repaint)
            {
                var style = selected ? _cardSelectedStyle : _cardStyle;
                if (style != null) style.Draw(rect, false, false, false, false);
                if (selected) EditorGUI.DrawRect(rect, new Color(0.35f, 0.75f, 0.45f, 0.28f));
                else if (!hasContent) EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 0.35f));
            }

            if (hasContent)
            {
                // 左侧大图标纵向占满两行（名称 + 效果），名称在右上、效果在右下、状态第三行
                var iconRect = new Rect(rect.x + 5, rect.y + (rect.height - LoadoutIconSize) * 0.5f,
                    LoadoutIconSize, LoadoutIconSize);
                if (icon != null) GUI.DrawTexture(iconRect, icon.texture, ScaleMode.ScaleToFit);
                else EditorGUI.DrawRect(iconRect, new Color(0.28f, 0.28f, 0.28f));

                var textX = iconRect.xMax + 5;
                var textW = rect.xMax - textX - 4;
                EditorGUI.LabelField(new Rect(textX, rect.y + 7, textW, 15),
                    new GUIContent(FitTitle(title, textW), tip), EditorStyles.miniBoldLabel);

                var oldColor = GUI.contentColor;
                GUI.contentColor = selected ? effectColor : new Color(0.72f, 0.72f, 0.72f);
                EditorGUI.LabelField(new Rect(textX, rect.y + 25, textW, 14), new GUIContent(effect, tip),
                    EditorStyles.miniLabel);
                GUI.contentColor = selected ? new Color(0.68f, 0.86f, 0.68f) : new Color(0.58f, 0.58f, 0.58f);
                EditorGUI.LabelField(new Rect(textX, rect.y + 42, textW, 13), new GUIContent(stateText, tip),
                    EditorStyles.miniLabel);
                GUI.contentColor = oldColor;
            }
            else
            {
                EditorGUI.LabelField(rect, "无此档", EditorStyles.centeredGreyMiniLabel);
            }

            var evt = Event.current;
            if (evt.type != EventType.MouseDown || !rect.Contains(evt.mousePosition)) return;
            if (!hasContent)
            {
                evt.Use();
                return;
            }

            if (evt.button == 1)
            {
                if (isModuleSlot) state.Module = 0;
                else state.Select[slot] = -1;
                evt.Use();
                Repaint();
                return;
            }

            if (evt.button == 0)
            {
                ShowLoadoutPicker(state, slot, rect, tiers);
                evt.Use();
            }
        }

        /// <summary>点卡片弹出候选列表：与升级/模组页同一个 <see cref="SOPickerPopup{T}"/>（只列本档的选项）。</summary>
        private void ShowLoadoutPicker(LoadoutState state, int slot, Rect activator,
            List<List<WeaponUpgradeData_SO>> tiers)
        {
            if (slot >= LoadoutUpgradeSlots)
            {
                var modules = state.Owner != null ? state.Owner.Modules : null;
                if (modules == null || modules.Count <= 1) return;
                var list = new List<WeaponModuleData_SO>(modules.Count);
                for (var i = 1; i < modules.Count; i++)
                {
                    if (modules[i] != null) list.Add(modules[i]);
                }
                if (list.Count == 0) return;

                PopupWindow.Show(activator, new SOPickerPopup<WeaponModuleData_SO>(
                    list,
                    so => { state.Module = modules.IndexOf(so); Repaint(); },
                    so => so.icon, so => so.name,
                    so => so.typeName, so => so.color,
                    so => ($"Assets/Resources/Images/Icon/Frame_Module{so.type}.png", so.color),
                    false, so => EffectSummary(so.modifys)));
                return;
            }

            var options = slot < tiers.Count ? tiers[slot] : null;
            if (options == null || options.Count == 0) return;
            PopupWindow.Show(activator, new SOPickerPopup<WeaponUpgradeData_SO>(
                options,
                so => { state.Select[slot] = options.IndexOf(so); Repaint(); },
                so => so.icon, so => so.name, so => so.type, null, null, false,
                so => GetEffectSummary(so), so => GetEffectColor(so)));
        }

        /// <summary>档位的解锁等级（prefab 的 <c>Upgrade[i].Key</c>），只用于 tooltip。</summary>
        private static int TierLevel(SerializedObject serialized, int slot)
        {
            var upgradeProp = serialized?.FindProperty("Upgrade");
            if (upgradeProp == null || slot >= upgradeProp.arraySize) return 0;
            return upgradeProp.GetArrayElementAtIndex(slot).FindPropertyRelative("Key")?.intValue ?? 0;
        }

        /// <summary>
        /// 按可用宽度截标题（miniBoldLabel 汉字约 11px，加粗再多一点）：放不下的截断加省略号。
        /// <para>⚠ 别指望 LabelField 自己"显示完整"：它不换行，超宽直接裁一半字，比省略号更难看。</para>
        /// </summary>
        private static string FitTitle(string title, float width)
        {
            if (string.IsNullOrEmpty(title)) return "";
            var max = Mathf.Max(2, Mathf.FloorToInt(width / 11.5f));
            return title.Length <= max ? title : title.Substring(0, max - 1) + "…";
        }

        /// <summary>任一 modifys 的效果摘要（"射速 +15% 弹匣 +2"）：先列前 3 条，超出用省略号。</summary>
        private static string EffectSummary(List<ModifyAttrData> modifys, int maxParts = 3)
        {
            if (modifys == null || modifys.Count == 0) return "";
            var parts = new List<string>(maxParts);
            for (var i = 0; i < modifys.Count; i++)
            {
                if (i >= maxParts)
                {
                    parts.Add("…");
                    break;
                }
                var item = modifys[i];
                if (item.type == WeaponAttrType.Special) continue;
                // ModifierType 是 [Flags]，All 也含 Factor ⇒ 用位与判断而不是相等
                var isFactor = (item.modifier & ModifierType.Factor) != 0;
                var value = isFactor ? item.value * 100f : item.value;
                parts.Add($"{(value >= 0f ? "+" : "")}{value:0.##}{(isFactor ? "%" : "")}");
            }
            return string.Join(" ", parts);
        }

        /// <summary>有负值（代价）就转暖色，纯增益保持绿色。</summary>
        private static Color EffectColor(List<ModifyAttrData> modifys)
            => modifys != null && modifys.Exists(m => m.value < 0f)
                ? new Color(0.95f, 0.68f, 0.42f)
                : new Color(0.62f, 0.88f, 0.62f);

        /// <summary>模组摘要：desc 是"增益/代价"的成对行，拼成多行塞进 tooltip。</summary>
        private static string DescribeModule(WeaponModuleData_SO module)
        {
            if (module == null) return "";
            if (module.desc == null || module.desc.Count == 0) return module.describe ?? "";
            var lines = new List<string>(module.desc.Count);
            foreach (var item in module.desc)
            {
                lines.Add((item.Key ? "↑ " : "↓ ") + item.Value);
            }
            return string.Join("\n", lines);
        }
    }
}
