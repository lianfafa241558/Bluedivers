using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.Game
{

    [CreateAssetMenu(fileName = "WMD_", menuName = "Data/武器模组")]
    public class WeaponModuleData_SO : ScriptableObject
    {
        public new string name;
        [InspectorName("模组类型")]
        public ModuleType type;
        public Sprite icon;

        [InspectorName("描述")]
        public List<SKVP<bool, string>> desc;

        [InspectorName("修改属性")]
        public List<ModifyAttrData> modifys;


        public Sprite frame => ResManager.Instance.LoadSprite("Icon/Frame_Module"+ type.ToString(),true);

        public string typeName => type switch {
            ModuleType.Clean => "无暇改装模组",
            ModuleType.Balanced => "均衡改装模组",
            ModuleType.Unstable => "危险改装模组",
            _=> "",
        };
        public Color color => type switch {
            ModuleType.Clean => new(0.26f, 0.61f, 0.37f),
            ModuleType.Balanced => new(0.88f,0.78f,0.24f),
            ModuleType.Unstable => new(0.78f, 0.21f, 0f),
            _ => Color.gray,
        };

        public enum ModuleType
        {
            [InspectorName("无")]
            /// <summary>无</summary>
            None,
            [InspectorName("无暇")]
            /// <summary>无暇</summary>
            Clean,
            [InspectorName("平衡")]
            /// <summary>平衡</summary>
            Balanced,
            [InspectorName("危险")]
            /// <summary>危险</summary>
            Unstable,
        }

    }

#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(ModifyAttrData))]
    public class ModifyAttrDataDrawer : PropertyDrawer
    {
        private const int SpecialValue = 999;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty nameProp = property.FindPropertyRelative("name");
            SerializedProperty typeProp = property.FindPropertyRelative("type");
            SerializedProperty modifierProp = property.FindPropertyRelative("modifier");
            SerializedProperty valueProp = property.FindPropertyRelative("value");

            bool isSpecial = typeProp.intValue == SpecialValue;

            float spacing = 3f;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float labelW = 22f;
            float x = position.x;

            if (isSpecial)
            {
                // Show name + type + modifier + value
                float nameW = position.width * 0.22f;
                float typeW = position.width * 0.18f;
                float modW = 60f;
                float valW = position.width - nameW - typeW - modW - spacing * 4 - labelW * 2;

                EditorGUI.LabelField(new Rect(x, position.y, labelW, lineHeight), "名");
                x += labelW;
                EditorGUI.PropertyField(new Rect(x, position.y, nameW - labelW, lineHeight), nameProp, GUIContent.none);
                x += nameW - labelW + spacing;

                EditorGUI.LabelField(new Rect(x, position.y, labelW, lineHeight), "型");
                x += labelW;
                EditorGUI.PropertyField(new Rect(x, position.y, typeW - labelW, lineHeight), typeProp, GUIContent.none);
                x += typeW - labelW + spacing;

                EditorGUI.PropertyField(new Rect(x, position.y, modW, lineHeight), modifierProp, GUIContent.none);
                x += modW + spacing;

                EditorGUI.PropertyField(new Rect(x, position.y, valW, lineHeight), valueProp, GUIContent.none);
            }
            else
            {
                // Hide name, only show type + modifier + value
                float typeW = position.width * 0.22f;
                float modW = 60f;
                float valW = position.width - typeW - modW - spacing * 3 - labelW;

                EditorGUI.LabelField(new Rect(x, position.y, labelW, lineHeight), "型");
                x += labelW;
                EditorGUI.PropertyField(new Rect(x, position.y, typeW - labelW, lineHeight), typeProp, GUIContent.none);
                x += typeW - labelW + spacing;

                EditorGUI.PropertyField(new Rect(x, position.y, modW, lineHeight), modifierProp, GUIContent.none);
                x += modW + spacing;

                EditorGUI.PropertyField(new Rect(x, position.y, valW, lineHeight), valueProp, GUIContent.none);
            }

            EditorGUI.EndProperty();
        }
    }
#endif
}