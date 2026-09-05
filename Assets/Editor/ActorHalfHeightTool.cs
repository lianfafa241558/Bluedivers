using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.EditorExt
{
    /// <summary>
    /// 单位半高度批量设置工具
    /// 遍历 Assets 下所有带 <see cref="Actor"/> 的预制体，把 Actor 的「半高度」设置为
    /// 瞄准点(AimPoint) 相对该 Actor 物体的局部 Y 坐标。
    /// 菜单：Tools/单位半高度批量设置
    /// 管线(扫描/应用/报告)由 <see cref="PrefabBatchToolBase{TComponent}"/> 基类提供，
    /// 本类只负责组件判定与字段写入。
    /// </summary>
    public class ActorHalfHeightTool : PrefabBatchToolBase<Actor>
    {
        private const string HalfHeightField = "halfHeight";
        private bool _overwriteConfigured = true;

        [MenuItem("Tools/单位半高度批量设置")]
        private static void Open()
        {
            GetWindow<ActorHalfHeightTool>("单位半高度批量设置");
        }

        protected override string WindowTitle => "单位半高度批量设置";

        protected override string HelpText =>
            "遍历 Assets 下所有带 Actor 的预制体，把 Actor 的「半高度」设置为 AimPoint 相对该 Actor 物体的局部 Y 坐标。\n" +
            "该值用于地雷等需要区分空中/地面单位的竖直判定，为 0 时退化为不做高度过滤。";

        protected override string ApplyProgressTitle => "应用半高度";

        protected override void DrawOptions()
        {
            _overwriteConfigured = EditorGUILayout.ToggleLeft(
                "覆盖已配置过的值(不勾选则只填当前为 0 的)", _overwriteConfigured);
            base.DrawOptions();
        }

        /// <summary>扫描判定：Actor 需配置 AimPoint 且其局部 Y &gt; 0 才可应用</summary>
        protected override BatchItem CreateItem(Actor actor, string prefabPath, string objectPath)
        {
            var item = new BatchItem
            {
                PrefabPath = prefabPath,
                ObjectPath = objectPath,
                OldText = ReadHalfHeight(actor).ToString("0.###"),
            };

            if (!actor.AimPoint)
            {
                item.Reason = "未配置瞄准点(AimPoint)";
            }
            else if (SkipNestedPrefab && IsInNestedPrefab(actor.gameObject))
            {
                item.Reason = "位于嵌套预制体内(已跳过)";
            }
            else
            {
                float value = GetLocalAimHeight(actor);
                if (value <= 0f)
                {
                    item.Reason = "AimPoint 局部 Y 必须 > 0";
                }
                else
                {
                    item.Valid = true;
                    item.NewText = value.ToString("0.###");
                }
            }

            return item;
        }

        /// <summary>把 AimPoint 局部 Y 写入 Actor.halfHeight</summary>
        protected override WriteResult WriteValue(Actor actor)
        {
            var so = new SerializedObject(actor);
            var prop = so.FindProperty(HalfHeightField);
            if (prop == null)
            {
                Debug.LogWarning($"[半高度] 未找到字段 {HalfHeightField}：{actor.name}", actor);
                return WriteResult.Skipped;
            }

            if (!_overwriteConfigured && prop.floatValue > 0f)
            {
                return WriteResult.Skipped;
            }

            float value = GetLocalAimHeight(actor);
            if (value <= 0f)
            {
                return WriteResult.Skipped;
            }

            if (Mathf.Approximately(prop.floatValue, value))
            {
                return WriteResult.NoChange;
            }

            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return WriteResult.Modified;
        }

        /// <summary>读取 Actor 当前配置的半高度</summary>
        private static float ReadHalfHeight(Actor actor)
        {
            var so = new SerializedObject(actor);
            var prop = so.FindProperty(HalfHeightField);
            return prop != null ? prop.floatValue : 0f;
        }

        /// <summary>AimPoint 相对 Actor 物体的局部 Y 坐标</summary>
        private static float GetLocalAimHeight(Actor actor)
        {
            Transform aim = actor.AimPoint;
            if (!aim) return 0f;
            // 用 InverseTransformPoint 而非 localPosition，兼容 AimPoint 不是 Actor 直接子节点的情况
            return actor.transform.InverseTransformPoint(aim.position).y;
        }
    }
}
