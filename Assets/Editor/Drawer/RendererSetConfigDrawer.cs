using UnityEditor;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>RendererSetConfig（EnemyFxData_SO.rendererSet 列表项）的折叠绘制器</summary>
    [CustomPropertyDrawer(typeof(RendererSetConfig))]
    public class RendererSetConfigDrawer : PropertyDrawer
    {
        private const float lineHeight = 20f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeProp = property.FindPropertyRelative("type");
            var typeValue = (MPBTypeEnum)typeProp.enumValueIndex;

            // 绘制折叠箭头和标签
            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, lineHeight),
                property.isExpanded, label);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            // 计算起始Y位置
            float y = position.y + lineHeight;

            float LabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = position.width * 0.3f;

            // 绘制公共属性
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, lineHeight),
                property.FindPropertyRelative("type"));
            y += lineHeight;

            // 根据类型显示不同字段
            switch (typeValue)
            {
                case MPBTypeEnum.Switch:
                    DrawSwitchProperties(position, property, ref y);
                    break;

                case MPBTypeEnum.Trigger:
                    DrawTriggerProperties(position, property, ref y);
                    break;
            }

            EditorGUIUtility.labelWidth = LabelWidth;
            EditorGUI.EndProperty();
        }

        private void DrawSwitchProperties(Rect position, SerializedProperty property, ref float y)
        {
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, lineHeight),
                property.FindPropertyRelative("material"));
            y += lineHeight * 1.1f;
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, lineHeight),
                property.FindPropertyRelative("colorName"));
            y += lineHeight * 1.5f;

            float LabelWidth = EditorGUIUtility.labelWidth;

            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width / 2, lineHeight),
                property.FindPropertyRelative("occasion"));
            EditorGUI.PropertyField(
                new Rect(position.x + position.width / 2 + 20, y, position.width / 2 - 20, lineHeight),
                property.FindPropertyRelative("defaultColor"));
            y += lineHeight;
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width / 2, lineHeight),
                property.FindPropertyRelative("switchOccasion"));
            EditorGUI.PropertyField(
                new Rect(position.x + position.width / 2 + 20, y, position.width / 2 - 20, lineHeight),
                property.FindPropertyRelative("switchColor"));
            y += lineHeight;
        }

        private void DrawTriggerProperties(Rect position, SerializedProperty property, ref float y)
        {
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, lineHeight),
                property.FindPropertyRelative("occasion"));
            y += lineHeight;

            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, lineHeight),
                property.FindPropertyRelative("material"));
            y += lineHeight;

            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, lineHeight),
                property.FindPropertyRelative("colorName"));
            y += lineHeight * 1.1f;
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, lineHeight),
                property.FindPropertyRelative("gradient"));
            y += lineHeight;

            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, lineHeight),
                property.FindPropertyRelative("duration"));
            y += lineHeight;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return lineHeight;

            var typeProp = property.FindPropertyRelative("type");
            int lineCount = 3; // 基础属性(occasion+material+type)

            if (typeProp.enumValueIndex == (int)MPBTypeEnum.Switch)
                lineCount += 3; // switch模式属性
            else
                lineCount += 3; // trigger模式属性

            // 如果是数组中的元素则增加额外间距
            if (property.propertyPath.Contains(".Array.data["))
                lineCount += 1;

            return lineCount * lineHeight;
        }
    }
}
