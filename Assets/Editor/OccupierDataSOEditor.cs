using System.Collections.Generic;
using System.Linq;
using FPSGame.Data;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{
/// <summary>
/// `OccupierData_SO` 专属检视器：势力名册（名字 + 图标）+ 每张地图 × 每个任务点一份势力表。
///
///  1) 顶部体检（地图 / 任务点 / 空任务点 / 合计≠100），一眼看出哪张图还没填；
///  2) 势力名走「名册下拉」+ 行首图标预览，名册里没有的名字标红（打错字不会报错，只会静默没有图标）；
///  3) 「按 MapData_SO 重建 / 对齐」按**任务点名**对齐：地图加点/减点自动补齐，任务点改名也靠同下标接回已填数值。
///
/// <para>▍⚠ 这里**不用 SerializedProperty 改结构**（2026-10-28 实测踩到）：增删行走 property 时，
/// 面板外任何一次 <c>serializedObject.Update()</c> 都能把"还没 Apply 的删除"吞掉 ⇒ 点 × 毫无反应。
/// 现在增删直接改对象 + <c>Undo.RecordObject</c> + <c>SetDirty</c> + <c>GUIUtility.ExitGUI()</c>，
/// 结果只取决于这几行代码，不看面板的刷新次序。</para>
///
/// <para>▍⚠ 每个势力占**两行**（名字一行、滑块一行）：挤一行时窄面板会把数值框和 × 推出可视区，
/// 表现就是"数值打不进去 / 点了没反应"。</para>
/// </summary>
[CustomEditor(typeof(OccupierData_SO))]
public class OccupierDataSOEditor : DataSOEditorBase
{
    private const string MapPrefix = "MD_";

    private static readonly Color Accent = new Color(0.86f, 0.45f, 0.32f);

    private static List<MapData_SO> s_maps = new List<MapData_SO>();
    private static Dictionary<int, MapData_SO> s_mapsByKey = new Dictionary<int, MapData_SO>();
    private static double s_lastReload;

    private static GUIStyle s_okMini;
    private static GUIStyle s_warnMini;

    /// <summary>每张地图的展开状态（key = mapId）。只活在检视器实例里，切资产即重置。</summary>
    private readonly Dictionary<string, bool> _open = new Dictionary<string, bool>();

    private OccupierData_SO So => (OccupierData_SO)target;

    //========================================================
    //  入口
    //========================================================

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        EnsureStatic();
        RefreshMaps();

        // 不用 EditorGUI.BeginChangeCheck：结构改动会 GUIUtility.ExitGUI() 提前收尾，
        // 成对的 Begin/End 会被打断（计数器泄漏）。所有控件都是直接写对象，用 GUI.changed 兜落盘就够。
        GUI.changed = false;

        DrawScriptHeader();
        ColorBar(Accent);
        DrawHealth(So);

        SectionTitle("势力名册", Accent);
        DrawRoster(So);

        SectionTitle("地图势力表", Accent);
        DrawToolbar(So);
        DrawMaps(So);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
            ScheduleSave();
        }
    }

    //========================================================
    //  体检 / 名册 / 工具条
    //========================================================

    private void DrawHealth(OccupierData_SO so)
    {
        int points = 0, empty = 0, badSum = 0;
        foreach (var map in so.maps)
        {
            if (map == null || map.points == null) continue;
            foreach (var point in map.points)
            {
                ++points;
                int sum = SumOf(point);
                if (sum == 0) ++empty;
                else if (sum != 100) ++badSum;
            }
        }

        string text = $"地图 {so.maps.Count} 张 · 任务点 {points} 个 · 空 {empty} · 合计≠100 {badSum}";
        EditorGUILayout.LabelField(text, empty + badSum == 0 ? s_okMini : s_warnMini);

        // 结算加控制度只认名册里这个字（OccupierData_SO.SuperFactionName）⇒ 名字对不上就白涨
        if (so.FindFaction(OccupierData_SO.SuperFactionName) == null)
            EditorGUILayout.LabelField($"⚠ 名册里没有「{OccupierData_SO.SuperFactionName}」⇒ 兜底收益无处记（友方势力不受影响）", s_warnMini);
        else if (!HasFriendly(so))
            EditorGUILayout.LabelField($"提示：名册里还没有任何「友方」势力 ⇒ 结算一直涨给「{OccupierData_SO.SuperFactionName}」", s_greyMiniStyle);
    }

    private static bool HasFriendly(OccupierData_SO so)
    {
        foreach (var f in so.factions)
            if (f != null && f.isFriendly) return true;
        return false;
    }

    private void DrawRoster(OccupierData_SO so)
    {
        BeginBlockHeader("名册", so.factions.Count, 0f, "个");
        if (GUILayout.Button(new GUIContent("从数据补全", "把下面势力行用到、但名册里没有的名字加进来（图标要自己拖）"),
                EditorStyles.miniButton, GUILayout.Width(76)))
        {
            GatherRoster(so);
            GUIUtility.ExitGUI();
        }
        if (GUILayout.Button(new GUIContent("＋", "添加一个势力（名字 + 图标）"), EditorStyles.miniButton, GUILayout.Width(22)))
        {
            Touch("添加势力");
            so.factions.Add(new OccupierFaction { name = string.Empty });
            GUIUtility.ExitGUI();
        }
        EndBlockHeader();

        for (int i = 0; i < so.factions.Count; i++)
        {
            var faction = so.factions[i];
            if (faction == null)
            {
                faction = new OccupierFaction();
                so.factions[i] = faction;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{i + 1}.", EditorStyles.miniLabel, GUILayout.Width(24));
            faction.name = EditorGUILayout.TextField(faction.name, GUILayout.MinWidth(70));
            faction.icon = (Sprite)EditorGUILayout.ObjectField(faction.icon, typeof(Sprite), false, GUILayout.Width(80));
            faction.isFriendly = EditorGUILayout.ToggleLeft(
                new GUIContent("友方", "打赢后控制度涨给这个势力（本局任务点里有友方势力时才涨给它，没有才涨给超级夏莱）"),
                faction.isFriendly, GUILayout.Width(52));
            if (string.IsNullOrEmpty(faction.name)) GUILayout.Label("空名", s_warnMini, GUILayout.Width(32));
            else if (faction.icon == null) GUILayout.Label("无图", s_warnMini, GUILayout.Width(32));
            bool delete = GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();

            if (delete)
            {
                Touch("删除势力");
                so.factions.RemoveAt(i);
                GUIUtility.ExitGUI();
            }
        }

        if (so.factions.Count == 0)
            GUILayout.Label("名册为空 ⇒ 下面的势力行只能手打名字，且选图界面上没有图标。", s_greyMiniStyle);
    }

    private void DrawToolbar(OccupierData_SO so)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("按 MapData_SO 重建",
                "按任务点名对齐全部地图：补齐缺失的地图/任务点，已填的势力值尽量保留（任务点改名时按同下标接续）"),
                GUILayout.Height(22)))
        {
            Rebuild(so);
            GUIUtility.ExitGUI();
        }
        if (GUILayout.Button("全部展开", EditorStyles.miniButton, GUILayout.Width(64)))
        {
            foreach (var map in so.maps)
                if (map != null) _open[map.mapId] = true;
        }
        if (GUILayout.Button("全部折叠", EditorStyles.miniButton, GUILayout.Width(64)))
            _open.Clear();
        EditorGUILayout.EndHorizontal();
    }

    //========================================================
    //  地图 / 任务点 / 势力行
    //========================================================

    private void DrawMaps(OccupierData_SO so)
    {
        if (so.maps.Count == 0)
        {
            GUILayout.Label("还没有地图条目 —— 点上面「按 MapData_SO 重建」生成全部地图的任务点骨架。", s_greyMiniStyle);
            return;
        }

        for (int m = 0; m < so.maps.Count; m++)
        {
            var map = so.maps[m];
            if (map == null)
            {
                map = new OccupierMapData();
                so.maps[m] = map;
            }

            MapData_SO mapAsset = FindMapAsset(map.mapId);
            int expect = mapAsset != null && mapAsset.mapItemInfos != null ? mapAsset.mapItemInfos.Length : -1;
            bool aligned = expect >= 0 && expect == map.points.Count;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 第一行：折叠 + 名字（名字可能很长 ⇒ 独占一行，别和按钮抢宽度）
            bool open = IsOpen(map.mapId);
            bool newOpen = EditorGUILayout.Foldout(open, MapLabel(map.mapId, mapAsset), true);
            if (newOpen != open) _open[map.mapId] = newOpen;

            // 第二行：任务点计数 + 操作
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(expect < 0 ? "⚠ 无对应地图资产" : $"任务点 {map.points.Count}/{expect}",
                aligned ? s_greyMiniStyle : s_warnMini);
            GUILayout.FlexibleSpace();

            bool align = GUILayout.Button(new GUIContent("对齐", "按这张图的 MapData_SO 任务点补齐/改名（已填数值尽量保留）"),
                EditorStyles.miniButton, GUILayout.Width(40));
            bool deleteMap = GUILayout.Button(new GUIContent("删除", "删掉这张地图的势力条目"),
                EditorStyles.miniButton, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            if (align)
            {
                if (mapAsset == null)
                {
                    Debug.LogWarning($"[势力] 找不到 mapId={map.mapId} 对应的 MapData_SO，未对齐。");
                }
                else
                {
                    Touch("对齐势力表任务点");
                    AlignPoints(mapAsset, map);
                }
                GUIUtility.ExitGUI();
            }

            if (deleteMap)
            {
                Touch("删除地图条目");
                so.maps.RemoveAt(m);
                GUIUtility.ExitGUI();
            }

            if (newOpen) DrawPoints(so, map);
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawPoints(OccupierData_SO so, OccupierMapData map)
    {
        List<string> roster = RosterNames();

        for (int p = 0; p < map.points.Count; p++)
        {
            var point = map.points[p];
            if (point == null)
            {
                point = new OccupierPointData();
                map.points[p] = point;
            }

            string expect = AssetPointName(map, p);
            int sum = SumOf(point);

            // 第一行：序号 + 任务点名（可能与资产名不一致）
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{p + 1}. {point.pointName}", s_boldStyle);
            if (expect != null && expect != point.pointName)
                GUILayout.Label($"⚠ 资产为「{expect}」（点「对齐」接回）", s_warnMini);
            EditorGUILayout.EndHorizontal();

            // 第二行：合计 + 操作
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"合计 {sum}%", sum == 100 ? s_greyMiniStyle : s_warnMini, GUILayout.Width(80));
            GUILayout.FlexibleSpace();

            bool copy = GUILayout.Button(new GUIContent("复制", "把这个任务点的势力复制到本图其它任务点"),
                EditorStyles.miniButton, GUILayout.Width(40));
            bool addRow = GUILayout.Button(new GUIContent("＋势力", "添加一行势力"), EditorStyles.miniButton, GUILayout.Width(54));
            bool deletePoint = GUILayout.Button(new GUIContent("删除", "删除这个任务点（只删势力表条目，不动 MapData_SO）"),
                EditorStyles.miniButton, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            if (copy)
            {
                Touch("复制势力到其它任务点");
                CopyPoint(point, map);
            }
            if (addRow)
            {
                Touch("添加势力行");
                point.occupiers.Add(new OccupierEntry
                {
                    name = roster.Count > 0 ? roster[0] : string.Empty,
                    value = point.occupiers.Count == 0 ? 100 : 0,   // 第一行默认给满（独占），后续从 0 起
                });
            }
            if (deletePoint)
            {
                Touch("删除任务点");
                map.points.RemoveAt(p);
                GUIUtility.ExitGUI();
            }

            EditorGUI.indentLevel++;
            if (point.occupiers.Count == 0)
                GUILayout.Label("（空：这个任务点在选图界面不显示势力）", s_greyMiniStyle);
            for (int e = 0; e < point.occupiers.Count; e++)
                DrawEntry(so, point, e, roster);
            EditorGUI.indentLevel--;
            GUILayout.Space(4);
        }
    }

    private void DrawEntry(OccupierData_SO so, OccupierPointData point, int index, List<string> roster)
    {
        var entry = point.occupiers[index];
        if (entry == null)
        {
            entry = new OccupierEntry();
            point.occupiers[index] = entry;
        }

        // 第一行：图标预览 + 势力下拉 + 删除
        EditorGUILayout.BeginHorizontal();
        Sprite icon = so.GetIcon(entry.name);
        Rect iconRect = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f), GUILayout.Height(16f));
        if (icon != null) GUI.DrawTexture(iconRect, icon.texture, ScaleMode.ScaleToFit, true);
        else DrawTintedIcon(iconRect, null, new Color(1f, 1f, 1f, 0.06f));

        entry.name = DrawFactionPopup(entry.name, roster);
        bool delete = GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();

        if (delete)
        {
            Touch("删除势力行");
            point.occupiers.RemoveAt(index);
            GUIUtility.ExitGUI();
        }

        // 第二行：滑块（拖）+ 数值框（敲）—— 分开两个控件，输入不会被滑块吃掉
        // ⚠ 这一行里的所有控件都必须"不换行 + 给足宽度"：miniLabel 默认 wordWrap=true，
        //   在 12px 宽的 "%" 上会折成两行、把字挤到下面去（2026-10-28 实测错行）
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(20f);
        entry.value = Mathf.RoundToInt(GUILayout.HorizontalSlider(entry.value, 0f, 100f, GUILayout.MinWidth(60f)));
        entry.value = Mathf.Clamp(EditorGUILayout.IntField(entry.value, GUILayout.Width(46)), 0, 100);
        GUILayout.Label("%", EditorStyles.miniLabel, GUILayout.Width(18f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>势力名：名册下拉；名册里没有的名字（手打 / 名册被改）把当前值塞成首项显示，选别的才覆盖。</summary>
    private static string DrawFactionPopup(string cur, List<string> roster, params GUILayoutOption[] options)
    {
        if (roster.Count == 0) return EditorGUILayout.TextField(cur, options);

        int sel = roster.IndexOf(cur);
        if (sel >= 0)
        {
            int pick = EditorGUILayout.Popup(sel, roster.ToArray(), EditorStyles.popup, options);
            return pick == sel ? cur : roster[pick];
        }

        var opts = new string[roster.Count + 1];
        opts[0] = string.IsNullOrEmpty(cur) ? "（请选择势力）" : cur + " ⚠未在名册";
        for (int i = 0; i < roster.Count; i++) opts[i + 1] = roster[i];

        int index = EditorGUILayout.Popup(0, opts, EditorStyles.popup, options);
        return index > 0 ? roster[index - 1] : cur;
    }

    //========================================================
    //  结构操作（直接改对象；调用方随后 ExitGUI 收尾）
    //========================================================

    /// <summary>改结构前的统一处理：可撤销 + 立即落盘（值改动不走这里，走 EndChangeCheck）。</summary>
    private void Touch(string undoLabel)
    {
        Undo.RecordObject(target, undoLabel);
        EditorUtility.SetDirty(target);
        Repaint();
    }

    private void Rebuild(OccupierData_SO so)
    {
        var all = OrderedMaps();
        Touch("按 MapData_SO 重建势力表");

        int addedMap = 0, aligned = 0;
        foreach (var mapAsset in all)
        {
            string mapId = MapIdOf(mapAsset);
            var entry = so.FindMapData(mapId);
            if (entry == null)
            {
                entry = new OccupierMapData { mapId = mapId };
                so.maps.Add(entry);
                ++addedMap;
            }
            if (AlignPoints(mapAsset, entry)) ++aligned;
        }
        Debug.Log($"[势力] 按 MapData_SO 重建：{all.Count} 张图（新增 {addedMap}），任务点对齐 {aligned} 张");
    }

    /// <summary>把一条地图条目对齐到 <paramref name="mapAsset"/> 的任务点；返回是否改动过。</summary>
    private static bool AlignPoints(MapData_SO mapAsset, OccupierMapData entry)
    {
        var infos = mapAsset.mapItemInfos;
        int count = infos != null ? infos.Length : 0;

        var old = entry.points ?? new List<OccupierPointData>();
        if (old.Count == count)
        {
            bool same = true;
            for (int i = 0; i < count; ++i)
            {
                if (old[i] == null || old[i].pointName != infos[i].name) { same = false; break; }
            }
            if (same) return false;
        }

        var used = new bool[old.Count];
        var list = new List<OccupierPointData>(count);
        for (int i = 0; i < count; ++i)
        {
            string pointName = infos[i].name;

            // 先按名字找（任务点被插到中间时数值跟着名字走）；找不到再看同下标（任务点被改名时按位置接续）
            int pick = -1;
            for (int k = 0; k < old.Count; ++k)
            {
                if (used[k] || old[k] == null) continue;
                if (old[k].pointName == pointName) { pick = k; break; }
            }
            if (pick < 0 && i < old.Count && !used[i]) pick = i;

            OccupierPointData point;
            if (pick >= 0)
            {
                used[pick] = true;
                point = old[pick];
                point.pointName = pointName;
                if (point.occupiers == null) point.occupiers = new List<OccupierEntry>();
            }
            else
            {
                point = new OccupierPointData { pointName = pointName };
            }
            list.Add(point);
        }

        entry.points = list;
        return true;
    }

    private static void CopyPoint(OccupierPointData src, OccupierMapData map)
    {
        foreach (var other in map.points)
        {
            if (other == null || other == src) continue;
            other.occupiers.Clear();
            foreach (var entry in src.occupiers)
            {
                if (entry == null) continue;
                other.occupiers.Add(new OccupierEntry { name = entry.name, value = entry.value });
            }
        }
    }

    private void GatherRoster(OccupierData_SO so)
    {
        Touch("补全势力名册");

        int added = 0;
        foreach (var map in so.maps)
        {
            if (map == null || map.points == null) continue;
            foreach (var point in map.points)
            {
                if (point == null || point.occupiers == null) continue;
                foreach (var entry in point.occupiers)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.name) || so.FindFaction(entry.name) != null) continue;
                    so.factions.Add(new OccupierFaction { name = entry.name });   // 只补名字，图标要自己拖
                    ++added;
                }
            }
        }
        Debug.Log($"[势力] 名册补全：新增 {added} 个，当前 {so.factions.Count} 个");
    }

    //========================================================
    //  小工具
    //========================================================

    private static void RefreshMaps()
        => RefreshAssetCache("t:MapData_SO", ref s_lastReload, ref s_maps, ref s_mapsByKey, m => m.GetInstanceID());

    private static List<MapData_SO> OrderedMaps()
        => s_maps.Where(m => m != null).OrderBy(MapIdOf, System.StringComparer.Ordinal).ToList();

    private static MapData_SO FindMapAsset(string mapId)
        => s_maps.Find(m => m != null && MapIdOf(m) == mapId);

    /// <summary>mapId = 资产名去掉 <c>MD_</c> 前缀（与 <c>TaskManager.MapData</c> 的键同一套规则）。</summary>
    private static string MapIdOf(MapData_SO map)
        => map.name.StartsWith(MapPrefix) ? map.name.Substring(MapPrefix.Length) : map.name;

    private static string MapLabel(string mapId, MapData_SO mapAsset)
    {
        string areaName = mapAsset != null && !string.IsNullOrEmpty(mapAsset.AreaName) ? mapAsset.AreaName : "？";
        return $"{areaName}　（{mapId}）";
    }

    /// <summary>该下标在 MapData_SO 里的任务点名（没有地图资产 / 越界 ⇒ null）。</summary>
    private static string AssetPointName(OccupierMapData map, int index)
    {
        var mapAsset = FindMapAsset(map.mapId);
        if (mapAsset == null || mapAsset.mapItemInfos == null) return null;
        return index < mapAsset.mapItemInfos.Length ? mapAsset.mapItemInfos[index].name : null;
    }

    private bool IsOpen(string mapId)
        => _open.TryGetValue(mapId, out bool open) && open;

    /// <summary>名册里的名字（下拉候选用；跳过空名/空项）。</summary>
    private List<string> RosterNames()
    {
        var re = new List<string>();
        var factions = So.factions;
        if (factions == null) return re;
        foreach (var f in factions)
            if (f != null && !string.IsNullOrEmpty(f.name)) re.Add(f.name);
        return re;
    }

    private static int SumOf(OccupierPointData point)
    {
        if (point == null || point.occupiers == null) return 0;
        int sum = 0;
        foreach (var entry in point.occupiers)
            if (entry != null) sum += entry.value;
        return sum;
    }

    private static void EnsureStatic()
    {
        if (s_okMini != null) return;
        // ⚠ 这两个样式专给"行内小格子"用：**必须** wordWrap = false。
        //   miniLabel 自带 wordWrap=true，配 GUILayout.Width(小值) 时会把「无图」「%」折成两行、
        //   字掉到下面一行去（2026-10-28 在 % 上实测错行）。需要自动换行的长句子用 base 的 s_greyMiniStyle。
        s_okMini = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.45f, 0.85f, 0.45f) } };
        s_warnMini = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.65f, 0.35f) } };
    }
}
}
