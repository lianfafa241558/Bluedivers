using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Core;
using FPSGame.AI;
using Unity.FPS.Game;
using Unity.FPS.EditorExt;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPSGame.AI.EditorTools
{
    /// <summary>
    /// 敌人特效二次归并工具（阶段 B）：
    /// - 把每个单位旧 EnemyFxData_SO 里的 fxDic 拷贝成独立 EnemyFxEventData_SO 并挂到组件 fxEvent（每 prefab 一份，便于后续手动去重）；
    /// - 把 rendererSet 按"忽略材质的内容签名"归并成共享模板（条目材质清空），多 prefab 指向同一模板；
    /// - 组件材质统一写到 fxMaterial（config.material 非空仍优先，故归并仅针对单一材质单位；多材质单位保留自带材质不入模板）。
    /// 写入采用资产级 SerializedObject + SavePrefabAsset（对 prefab 变体安全），不走 LoadPrefabContents 烘焙。
    /// </summary>
    public class EnemyFxConsolidateWindow : PrefabBatchToolBase<EnemyControllerFX>
    {
        private const string FxRoot = "Assets/Resources/GameData/EnemyFx";
        private const string TemplateRoot = FxRoot + "/Templates";
        private const string EventRoot = FxRoot + "/Events";

        private static readonly string[] SearchFolders =
        {
            "Assets/Resources/Prefabs",
            "Assets/Art/Modle/Enemy",
        };

        // 扫描结果（复用基类 CreateItem/WriteValue 之外，扫描与应用都按我们的逻辑走）
        private readonly List<BatchItem> _mine = new List<BatchItem>();
        private bool _scanned;

        // ===== 文案 =====

        protected override string WindowTitle => "敌人特效归并(材质下沉+fxDic拆分)";
        protected override string HelpText =>
            "1.扫描：预览每个 EnemyControllerFX 将如何归并（模板/事件/材质）。\n" +
            "2.应用：生成共享模板 EnemyFxData_SO(rendererSet 材质清空) + 每单位 EnemyFxEventData_SO(fxDic 副本)，" +
            "组件写 fxData/fxEvent/fxMaterial，删除不再被引用的旧 EFX_*.asset。\n" +
            "提示：多材质单位保留自带材质不入模板；未挂 fxData 的单位不处理（需手动补齐）。";

        protected override string ScanButtonText => "1.扫描并预览归并方案";
        protected override string ApplyButtonText => "2.执行归并";

        protected override void DrawOptions()
        {
            // 归并规则固定，不显示基类默认的嵌套开关
        }

        /// <summary>本工具不走基类逐组件扫描管线（已 override Scan/Apply），此抽象钩子仅返回 null 占位。</summary>
        protected override BatchItem CreateItem(EnemyControllerFX comp, string prefabPath, string objectPath)
        {
            return null;
        }

        // ===== 入口 =====

        [MenuItem("Tools/敌人特效/归并-材质下沉与fxDic拆分")]
        public static void Open()
        {
            GetWindow<EnemyFxConsolidateWindow>(false, "敌人特效归并");
        }

        // ===== 扫描（只读预览） =====

        protected override void Scan()
        {
            _mine.Clear();
            _scanned = true;

            var lines = new StringBuilder();
            int total = 0;
            int okTemplate = 0;
            int okEventOnly = 0;
            int skipManual = 0;
            int skipNoData = 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
            try
            {
                for (int i = 0; i < guids.Length; ++i)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (EditorUtility.DisplayCancelableProgressBar("扫描归并方案", path,
                            i / (float)Mathf.Max(1, guids.Length)))
                    {
                        break;
                    }

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;

                    var comps = prefab.GetComponentsInChildren<EnemyControllerFX>(true);
                    for (int j = 0; j < comps.Length; ++j)
                    {
                        var comp = comps[j];
                        if (!OwnsDataHere(comp, path)) continue;
                        ++total;

                        var oldFx = comp.fxData;
                        if (!oldFx.IsValid())
                        {
                            ++skipManual;
                            lines.AppendLine($"{path}  [未挂 fxData] 跳过（需手动补齐）");
                            continue;
                        }
                        if (oldFx.rendererSet == null || oldFx.rendererSet.Count == 0)
                        {
                            ++skipNoData;
                            lines.AppendLine($"{path}  [rendererSet 空] 仅事件拷贝");
                            continue;
                        }

                        bool hasEvent = oldFx.fxDic != null && oldFx.fxDic.Count > 0;
                        string matInfo = TryGetSingleMaterial(oldFx, out Material mat);
                        if (matInfo != null)
                        {
                            ++okEventOnly;
                            lines.AppendLine($"{path}  事件→EVT；{matInfo}（保留自带材质，不入模板）");
                        }
                        else
                        {
                            string sig = RendererSetSignature(oldFx);
                            ++okTemplate;
                            lines.AppendLine($"{path}  模板[{(sig.Length > 12 ? sig.Substring(0, 12) + "…" : sig)}]" +
                                             $"{(hasEvent ? " + 事件EVT" : "")}；材质→组件 fxMaterial({mat.name})");
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string msg = $"扫描完成：命中单位 {total}；可归并入模板 {okTemplate}；仅事件拷贝 {okEventOnly}；" +
                         $"未挂fxData跳过 {skipManual}；rendererSet空 {skipNoData}\n\n{lines}";
            Debug.Log(msg);
        }

        // ===== 应用（资产级安全写入） =====

        protected override void Apply()
        {
            if (!_scanned)
            {
                Debug.LogWarning("[敌人特效归并] 请先扫描");
                return;
            }
            if (!EditorUtility.DisplayDialog("执行归并",
                    "将：生成共享模板 EnemyFxData_SO + 每单位 EnemyFxEventData_SO，\n" +
                    "并把组件引用改为 fxData(模板)/fxEvent(事件)/fxMaterial(单位材质)，删除无引用旧 EFX_*。\n" +
                    "建议先做版本控制快照。\n\n继续？",
                    "执行", "取消"))
            {
                return;
            }

            // 1) 收集需要处理的 prefab + 组件
            var records = CollectRecords();
            if (records.Count == 0)
            {
                Debug.LogWarning("[敌人特效归并] 没有可处理单位");
                return;
            }

            // 2) 生成/复用模板资产（rendererSet 材质清空）
            EnsureFolders();
            var sigToTemplate = LoadExistingTemplates();
            var prefixCount = sigToTemplate.Count;
            var assignedTemplates = new Dictionary<string, EnemyFxData_SO>(); // 每个模板签名 -> 资产

            foreach (var rec in records)
            {
                var oldFx = rec.OldFx;
                string matInfo = TryGetSingleMaterial(oldFx, out Material mat);
                if (matInfo != null)
                {
                    rec.MaterialOverride = mat; // 多材质/特殊 → 保留 fxData 原样，仅 fxMaterial 供 config 为空时兜底
                    continue;
                }
                rec.TemplateMaterial = mat;

                string sig = RendererSetSignature(oldFx);
                if (!assignedTemplates.TryGetValue(sig, out var template))
                {
                    if (!sigToTemplate.TryGetValue(sig, out template))
                    {
                        template = CreateTemplateAsset(sig, ++prefixCount);
                        sigToTemplate[sig] = template;
                    }
                    assignedTemplates[sig] = template;
                }
                rec.Template = template;
            }

            // 3) 逐 prefab 写入组件引用（资产级 + SavePrefabAsset）
            int changed = 0;
            var byPrefab = new Dictionary<string, List<Record>>();
            foreach (var rec in records)
            {
                if (!byPrefab.TryGetValue(rec.PrefabPath, out var list))
                {
                    list = new List<Record>();
                    byPrefab.Add(rec.PrefabPath, list);
                }
                list.Add(rec);
            }

            int step = 0;
            foreach (var kv in byPrefab)
            {
                if (EditorUtility.DisplayCancelableProgressBar("归并写入", kv.Key,
                        step++ / (float)byPrefab.Count))
                {
                    break;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Key);
                if (prefab == null) continue;
                bool dirty = false;

                foreach (var rec in kv.Value)
                {
                    if (rec.Comp == null) continue;

                    // 事件 SO：每单位一份副本
                    EnemyFxEventData_SO evt = rec.Comp.fxEvent;
                    if (evt == null && rec.OldFx.fxDic != null && rec.OldFx.fxDic.Count > 0)
                    {
                        evt = CreateEventAsset(kv.Key, rec.OldFx);
                    }

                    var ser = new SerializedObject(rec.Comp);
                    bool recDirty = false;

                    if (rec.Template != null && rec.Comp.fxData != rec.Template)
                    {
                        ser.FindProperty("fxData").objectReferenceValue = rec.Template;
                        recDirty = true;
                    }
                    if (rec.TemplateMaterial != null && rec.Comp.fxMaterial != rec.TemplateMaterial)
                    {
                        ser.FindProperty("fxMaterial").objectReferenceValue = rec.TemplateMaterial;
                        recDirty = true;
                    }
                    if (evt != null && rec.Comp.fxEvent != evt)
                    {
                        ser.FindProperty("fxEvent").objectReferenceValue = evt;
                        recDirty = true;
                    }

                    if (recDirty)
                    {
                        ser.ApplyModifiedPropertiesWithoutUndo();
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                    ++changed;
                }
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();

            // 4) 清理不再被引用的旧 EFX_* 资产（顶层目录，模板/事件目录除外）
            int deleted = DeleteOrphanFxAssets();

            Debug.Log($"[敌人特效归并] 完成：写入 prefab {changed} 个，删除旧资产 {deleted} 个。");
        }

        // ===== 数据 =====

        private sealed class Record
        {
            public string PrefabPath;
            public EnemyControllerFX Comp;
            public EnemyFxData_SO OldFx;
            public EnemyFxData_SO Template;
            public Material TemplateMaterial;
            public Material MaterialOverride; // 多材质：保留 config.material，仅兜底用
        }

        private List<Record> CollectRecords()
        {
            var records = new List<Record>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
            try
            {
                for (int i = 0; i < guids.Length; ++i)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (EditorUtility.DisplayCancelableProgressBar("收集单位", path,
                            i / (float)Mathf.Max(1, guids.Length)))
                    {
                        break;
                    }

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    var comps = prefab.GetComponentsInChildren<EnemyControllerFX>(true);
                    for (int j = 0; j < comps.Length; ++j)
                    {
                        var comp = comps[j];
                        if (!OwnsDataHere(comp, path)) continue;
                        if (!comp.fxData.IsValid()) continue; // 手动补齐后再跑一次即可
                        records.Add(new Record
                        {
                            PrefabPath = path,
                            Comp = comp,
                            OldFx = comp.fxData,
                        });
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return records;
        }

        /// <summary>该组件的数据是否属于当前 prefab（变体算；非变体嵌套实例跳过）</summary>
        private static bool OwnsDataHere(EnemyControllerFX comp, string prefabPath)
        {
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null && PrefabUtility.GetCorrespondingObjectFromSource(prefab) != null)
                    return true; // 变体顶层组件
                return PrefabUtility.GetCorrespondingObjectFromSource(comp) == null;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// 尝试取单位唯一材质：返回 null 表示成功（out 得到唯一材质）；返回非空字符串为失败原因。
        /// </summary>
        private static string TryGetSingleMaterial(EnemyFxData_SO oldFx, out Material material)
        {
            material = null;
            if (oldFx.rendererSet == null || oldFx.rendererSet.Count == 0)
                return "rendererSet为空";

            Material single = null;
            for (int i = 0; i < oldFx.rendererSet.Count; ++i)
            {
                var cfg = oldFx.rendererSet[i];
                if (cfg == null) continue;
                if (cfg.material == null) continue;
                if (single == null)
                {
                    single = cfg.material;
                    continue;
                }
                if (single != cfg.material)
                    return "多个不同材质(保留config.material，不入模板)";
            }

            if (single == null)
                return "rendererSet 未配置材质";

            material = single;
            return null;
        }

        /// <summary>rendererSet 内容签名（忽略材质），用于归并分组</summary>
        private static string RendererSetSignature(EnemyFxData_SO fx)
        {
            var sb = new StringBuilder(128);
            sb.Append("n").Append(fx.rendererSet != null ? fx.rendererSet.Count : 0);
            if (fx.rendererSet != null)
            {
                for (int i = 0; i < fx.rendererSet.Count; ++i)
                {
                    var c = fx.rendererSet[i];
                    if (c == null)
                    {
                        sb.Append("[n]");
                        continue;
                    }
                    sb.Append('[').Append((int)c.type).Append(',').Append((int)c.occasion).Append(',')
                      .Append((int)c.switchOccasion).Append(',')
                      .Append(c.colorName ?? "").Append(',')
                      .Append(c.defaultColor.r).Append(',').Append(c.defaultColor.g).Append(',')
                      .Append(c.defaultColor.b).Append(',').Append(c.defaultColor.a).Append(',')
                      .Append(c.switchColor.r).Append(',').Append(c.switchColor.g).Append(',')
                      .Append(c.switchColor.b).Append(',').Append(c.switchColor.a).Append(',')
                      .Append(c.duration).Append(']');
                    AppendGradient(sb, c.gradient);
                }
            }
            return sb.ToString();
        }

        private static void AppendGradient(StringBuilder sb, Gradient g)
        {
            if (g == null)
            {
                sb.Append("gn");
                return;
            }
            sb.Append("g:").Append((int)g.mode);
            var cks = g.colorKeys;
            sb.Append(";c").Append(cks.Length);
            for (int i = 0; i < cks.Length; ++i)
            {
                sb.Append(',').Append(cks[i].time).Append(':').Append(cks[i].color.r).Append(',')
                  .Append(cks[i].color.g).Append(',').Append(cks[i].color.b).Append(',').Append(cks[i].color.a);
            }
            var aks = g.alphaKeys;
            sb.Append(";a").Append(aks.Length);
            for (int i = 0; i < aks.Length; ++i)
            {
                sb.Append(',').Append(aks[i].time).Append(':').Append(aks[i].alpha);
            }
        }

        // ===== 资产创建/复用 =====

        private static Dictionary<string, EnemyFxData_SO> LoadExistingTemplates()
        {
            var map = new Dictionary<string, EnemyFxData_SO>();
            string[] guids = AssetDatabase.FindAssets("t:EnemyFxData_SO", new[] { TemplateRoot });
            foreach (var guid in guids)
            {
                var so = AssetDatabase.LoadAssetAtPath<EnemyFxData_SO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so == null) continue;
                string sig = RendererSetSignature(so);
                if (!map.ContainsKey(sig)) map[sig] = so;
            }
            return map;
        }

        private static EnemyFxData_SO CreateTemplateAsset(string sig, int index)
        {
            string path = $"{TemplateRoot}/Template_{index:00}.asset";
            while (AssetDatabase.LoadAssetAtPath<EnemyFxData_SO>(path) != null)
            {
                ++index;
                path = $"{TemplateRoot}/Template_{index:00}.asset";
            }

            // 以第一条含相同签名的旧数据为源生成模板（材质清空）
            var source = FindFirstWithSignature(sig);
            var template = ScriptableObject.CreateInstance<EnemyFxData_SO>();
            template.rendererSet = new List<RendererSetConfig>();
            if (source != null && source.rendererSet != null)
            {
                for (int i = 0; i < source.rendererSet.Count; ++i)
                {
                    var c = source.rendererSet[i];
                    if (c == null) continue;
                    template.rendererSet.Add(CloneCfg(c, clearMaterial: true));
                }
            }
            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.SaveAssets();
            Debug.Log("生成共享模板：" + path);
            return template;
        }

        private static EnemyFxData_SO FindFirstWithSignature(string sig)
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyFxData_SO", new[] { FxRoot });
            foreach (var guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.StartsWith(TemplateRoot, StringComparison.Ordinal) ||
                    p.StartsWith(EventRoot, StringComparison.Ordinal))
                {
                    continue;
                }
                var so = AssetDatabase.LoadAssetAtPath<EnemyFxData_SO>(p);
                if (so != null && RendererSetSignature(so) == sig) return so;
            }
            return null;
        }

        private static EnemyFxEventData_SO CreateEventAsset(string prefabPath, EnemyFxData_SO oldFx)
        {
            string dir = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            string mirror = dir.StartsWith("Assets/", StringComparison.Ordinal)
                ? dir.Substring("Assets/".Length)
                : dir;
            string name = "EVT_" + Sanitize(Path.GetFileNameWithoutExtension(prefabPath));
            string path = $"{EventRoot}/{mirror}/{name}.asset";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            var evt = AssetDatabase.LoadAssetAtPath<EnemyFxEventData_SO>(path);
            if (evt == null)
            {
                evt = ScriptableObject.CreateInstance<EnemyFxEventData_SO>();
                AssetDatabase.CreateAsset(evt, path);
            }

            if (oldFx.fxDic != null)
            {
                foreach (var key in oldFx.fxDic.Keys)
                {
                    if (!oldFx.fxDic.TryGet(key, out var src) || src == null) continue;
                    evt.fxDic[key] = CloneFx(src);
                }
            }
            EditorUtility.SetDirty(evt);
            AssetDatabase.SaveAssets();
            return evt;
        }

        private static RendererSetConfig CloneCfg(RendererSetConfig c, bool clearMaterial)
        {
            return new RendererSetConfig
            {
                material = clearMaterial ? null : c.material,
                type = c.type,
                occasion = c.occasion,
                colorName = c.colorName,
                defaultColor = c.defaultColor,
                switchOccasion = c.switchOccasion,
                switchColor = c.switchColor,
                gradient = c.gradient,
                duration = c.duration,
            };
        }

        private static FxSetConfig CloneFx(FxSetConfig s)
        {
            if (s == null) return null;
            return new FxSetConfig
            {
                SG = s.SG,
                cilp = s.cilp,
                ps = s.ps,
                trans = s.trans,
                go = s.go != null ? new List<ArmorBreakEffect>(s.go) : null,
            };
        }

        private static void EnsureFolders()
        {
            EnsureFolder(TemplateRoot);
            EnsureFolder(EventRoot);
        }

        private static void EnsureFolder(string dir)
        {
            string[] parts = dir.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; ++i)
            {
                string parent = cur;
                cur = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(cur))
                {
                    AssetDatabase.CreateFolder(parent, parts[i]);
                }
            }
        }

        private static int DeleteOrphanFxAssets()
        {
            // 收集仍被任何 EnemyControllerFX 引用（fxData/fxEvent）的资产路径
            var used = new HashSet<string>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
            foreach (var guid in guids)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;
                var comps = prefab.GetComponentsInChildren<EnemyControllerFX>(true);
                for (int i = 0; i < comps.Length; ++i)
                {
                    if (comps[i].fxData != null) used.Add(AssetDatabase.GetAssetPath(comps[i].fxData));
                    if (comps[i].fxEvent != null) used.Add(AssetDatabase.GetAssetPath(comps[i].fxEvent));
                }
            }

            int deleted = 0;
            string[] fxGuids = AssetDatabase.FindAssets("t:EnemyFxData_SO", new[] { FxRoot });
            foreach (var fxGuid in fxGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(fxGuid);
                if (p.StartsWith(TemplateRoot, StringComparison.Ordinal) ||
                    p.StartsWith(EventRoot, StringComparison.Ordinal))
                {
                    continue; // 只清顶层旧 EFX_*，不碰模板/事件目录
                }
                if (used.Contains(p)) continue;
                AssetDatabase.DeleteAsset(p);
                Debug.Log("删除旧资产：" + p);
                ++deleted;
            }
            return deleted;
        }

        private static string Sanitize(string name)
        {
            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; ++i)
            {
                if (chars[i] == ' ' || chars[i] == ':' || chars[i] == '?' || chars[i] == '!' ||
                    chars[i] == '*' || chars[i] == '"' || chars[i] == '<' || chars[i] == '>' ||
                    chars[i] == '|')
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }


    }
}
