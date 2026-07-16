using UnityEditor;
using UnityEngine;
using Unity.FPS.AI;

namespace Unity.FPS.AI.Editor
{
    /// <summary>
    /// AIInputUnitController.Turret 的自定义 PropertyDrawer
    /// </summary>
    [CustomPropertyDrawer(typeof(AIInputUnitController.Turret))]
    public class TurretDrawer : PropertyDrawer
    {
        private const float LineSpacing = 2f;
        private const float FieldHeight = 18f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return FieldHeight + LineSpacing;

            // 折叠时 + 展开后共 11 行：
            // 1 foldout
            // 1 "结构" 标题
            // 1 chassis + barrel 同行
            // 1 weapon
            // 1 "瞄准参数" 标题
            // 1 limitRotation + aimSharpness 同行
            // 1 detectionFireDelay + aimBlendTime 同行
            // 1 "偏移与调试" 标题
            // 1 barrelSetOffset
            // 1 allowDeviation
            // 1 dot 只读
            const int lines = 11;
            return lines * (FieldHeight + LineSpacing);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var chassisProp = property.FindPropertyRelative("chassis");
            var barrelProp = property.FindPropertyRelative("barrel");
            var weaponProp = property.FindPropertyRelative("weapon");
            var aimSharpnessProp = property.FindPropertyRelative("aimSharpness");
            var detectionFireDelayProp = property.FindPropertyRelative("detectionFireDelay");
            var aimBlendTimeProp = property.FindPropertyRelative("aimBlendTime");
            var limitRotationProp = property.FindPropertyRelative("limitRotation");
            var barrelSetOffsetProp = property.FindPropertyRelative("barrelSetOffset");
            var allowDeviationProp = property.FindPropertyRelative("allowDeviation");
            var dotProp = property.FindPropertyRelative("dot");

            float y = position.y;
            float fullWidth = position.width;
            float rowH = FieldHeight + LineSpacing;

            // ---- Foldout 头部 ----
            Rect foldoutRect = new Rect(position.x, y, fullWidth, FieldHeight);
            var foldoutLabel = BuildFoldoutLabel(property, chassisProp, barrelProp, weaponProp);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, foldoutLabel, true);
            y += rowH;

            if (!property.isExpanded)
                return;

            // ---- Section 1: 结构 ----
            DrawSectionHeader(position.x, ref y, fullWidth, "结构");

            // 底盘 + 炮管 同行
            Rect chassisRect = new Rect(position.x, y, (fullWidth - 8f) * 0.5f, FieldHeight);
            Rect barrelRect = new Rect(chassisRect.xMax + 8f, y, (fullWidth - 8f) * 0.5f, FieldHeight);
            DrawObjectFieldWithChinese(chassisRect, chassisProp, "底盘");
            DrawObjectFieldWithChinese(barrelRect, barrelProp, "炮管");
            y += rowH;

            // 绑定武器（独立行）
            Rect weaponRect = new Rect(position.x, y, fullWidth, FieldHeight);
            DrawObjectFieldWithChinese(weaponRect, weaponProp, "绑定武器");
            y += rowH;

            // ---- Section 2: 参数 ----
            DrawSectionHeader(position.x, ref y, fullWidth, "瞄准参数");

            // 限制旋转角度 + 瞄准锐度 同行
            float halfW = (fullWidth - 8f) * 0.5f;
            Rect limitRect = new Rect(position.x, y, halfW, FieldHeight);
            Rect sharpnessRect = new Rect(limitRect.xMax + 8f, y, halfW, FieldHeight);
            DrawIntSliderWithChinese(limitRect, limitRotationProp, "限制旋转角度", 0, 90);
            DrawFloatFieldWithChinese(sharpnessRect, aimSharpnessProp, "瞄准锐度");
            y += rowH;

            // 侦测开火延迟 + 瞄准时间 同行
            Rect delayRect = new Rect(position.x, y, halfW, FieldHeight);
            Rect blendRect = new Rect(delayRect.xMax + 8f, y, halfW, FieldHeight);
            DrawFloatFieldWithChinese(delayRect, detectionFireDelayProp, "开火延迟");
            DrawFloatFieldWithChinese(blendRect, aimBlendTimeProp, "瞄准时间");
            y += rowH;

            // ---- Section 3: 偏移与调试 ----
            DrawSectionHeader(position.x, ref y, fullWidth, "偏移与调试");

            // 炮管手动偏移
            Rect offsetRect = new Rect(position.x, y, fullWidth, FieldHeight);
            EditorGUI.PropertyField(offsetRect, barrelSetOffsetProp, new GUIContent("炮管手动偏移"));
            y += rowH;

            // 允许偏差弧度
            Rect devRect = new Rect(position.x, y, halfW, FieldHeight);
            DrawFloatFieldWithChinese(devRect, allowDeviationProp, "允许偏差(弧度)");
            y += rowH;

            // dot 只读显示
            Rect dotRect = new Rect(position.x, y, halfW, FieldHeight);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.FloatField(dotRect, "瞄准就绪度(dot)", dotProp.floatValue);
            }

            // 应用修改
            if (GUI.changed)
                property.serializedObject.ApplyModifiedProperties();
        }

        // ---- Helper: 构建 Foldout 标签（显示摘要信息） ----
        private static GUIContent BuildFoldoutLabel(SerializedProperty property,
            SerializedProperty chassis, SerializedProperty barrel, SerializedProperty weapon)
        {
            string displayName = property.displayName;
            var parts = new System.Text.StringBuilder();

            if (chassis?.objectReferenceValue != null)
                parts.Append("底盘✓ ");
            else
                parts.Append("底盘✗ ");

            if (barrel?.objectReferenceValue != null)
                parts.Append("炮管✓ ");
            else
                parts.Append("炮管✗ ");

            if (weapon?.objectReferenceValue != null)
                parts.Append("武器✓");
            else
                parts.Append("武器✗");

            return new GUIContent($" {displayName}  [{parts}]");
        }

        // ---- Helper: 绘制 Section 标题 ----
        private void DrawSectionHeader(float x, ref float y, float width, string title)
        {
            var headerRect = new Rect(x, y, width, FieldHeight);
            EditorGUI.LabelField(headerRect, title, EditorStyles.boldLabel);
            y += FieldHeight + LineSpacing;
        }

        // ---- Helper: 带中文标签的 ObjectField ----
        private static void DrawObjectFieldWithChinese(Rect rect, SerializedProperty prop, string chineseLabel)
        {
            float labelWidth = CalcChineseLabelWidth(chineseLabel);
            var lblRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            var fldRect = new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height);
            EditorGUI.LabelField(lblRect, chineseLabel);
            EditorGUI.PropertyField(fldRect, prop, GUIContent.none);
        }

        // ---- Helper: 带中文标签的 FloatField ----
        private static void DrawFloatFieldWithChinese(Rect rect, SerializedProperty prop, string chineseLabel)
        {
            float labelWidth = CalcChineseLabelWidth(chineseLabel);
            var lblRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            var fldRect = new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height);
            EditorGUI.LabelField(lblRect, chineseLabel);
            prop.floatValue = EditorGUI.FloatField(fldRect, GUIContent.none, prop.floatValue);
        }

        // ---- Helper: 带中文标签的 IntSlider ----
        private static void DrawIntSliderWithChinese(Rect rect, SerializedProperty prop, string chineseLabel, int min, int max)
        {
            float labelWidth = CalcChineseLabelWidth(chineseLabel);
            var lblRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            var fldRect = new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height);
            EditorGUI.LabelField(lblRect, chineseLabel);
            prop.intValue = EditorGUI.IntSlider(fldRect, GUIContent.none, prop.intValue, min, max);
        }

        // ---- Helper: 计算中文标签宽度 ----
        private static float CalcChineseLabelWidth(string text)
        {
            // 中文字符约 14px 宽，英文约 7px，加 8px 边距
            float w = 8f;
            foreach (char c in text)
            {
                w += c > 127 ? 14f : 8f;
            }
            return w;
        }
    }
}
