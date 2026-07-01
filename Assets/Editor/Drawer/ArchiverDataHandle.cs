
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using static ArchivesData_SO;
#endif


#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ArchivesFloat))]
public class ArchivesFloatDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 鑾峰彇value灞炴€?
        SerializedProperty valueProp = property.FindPropertyRelative("value");

        // 鐩存帴鍦ㄥ悓涓€涓鏄剧ずlabel鍜寁alue瀛楁
        EditorGUI.PropertyField(position, valueProp, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 杩斿洖鏍囧噯鍗曡楂樺害
        return EditorGUIUtility.singleLineHeight;
    }
}
//涓嶇煡閬撲负浠€涔堜笉鑳藉鐞唋ist<KVP>涓殑
[CustomPropertyDrawer(typeof(ArchSettingData))]
public class ArchSettingDataDrawer : PropertyDrawer
{
    private const float FoldoutIndent = 15f;
    private const float LineSpacing = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 澶勭悊鏁扮粍鍏冪礌鐨勬儏鍐碉紙褰揚ropertyDrawer鐢ㄤ簬List涓殑鍏冪礌鏃讹級
        if (property.propertyType == SerializedPropertyType.Generic && property.isArray)
        {
            // 濡傛灉鏄暟缁勫厓绱狅紝鍒欑洿鎺ョ粯鍒惰鍏冪礌
            DrawSingleProperty(position, property, label);
        }
        else
        {
            // 鏅€氬瓧娈电殑缁樺埗閫昏緫
            DrawSingleProperty(position, property, label);
        }

        EditorGUI.EndProperty();
    }

    private void DrawSingleProperty(Rect position, SerializedProperty property, GUIContent label)
    {
        // 璁剧疆鎶樺彔鐘舵€?
        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            property.isExpanded,
            label,
            true
        );

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            position.y += EditorGUIUtility.singleLineHeight + LineSpacing;

            // 鑾峰彇鎵€鏈夊簭鍒楀寲灞炴€?
            SerializedProperty titleProp = property.FindPropertyRelative("titile");
            SerializedProperty typeProp = property.FindPropertyRelative("type");
            SerializedProperty valueProp = property.FindPropertyRelative("value");
            SerializedProperty showTextsProp = property.FindPropertyRelative("showTexts");
            SerializedProperty sliderRangeProp = property.FindPropertyRelative("sliderRange");
            SerializedProperty sliderSuffixProp = property.FindPropertyRelative("sliderSuffix");

            // 缁樺埗鏍囬锛堝甫缂╄繘锛?
            Rect currentPosition = new Rect(
                position.x + FoldoutIndent,
                position.y,
                position.width - FoldoutIndent,
                EditorGUIUtility.singleLineHeight
            );

            EditorGUI.PropertyField(currentPosition, titleProp);
            currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;

            // 缁樺埗绫诲瀷
            EditorGUI.PropertyField(currentPosition, typeProp);
            currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;

            // 缁樺埗鍊?
            EditorGUI.PropertyField(currentPosition, valueProp);
            currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;

            // 鏍规嵁绫诲瀷鏄剧ず涓嶅悓鐨勫瓧娈?
            SettingBtnType type = (SettingBtnType)typeProp.enumValueIndex;

            if (type == SettingBtnType.Dropdown)
            {
                EditorGUI.PropertyField(currentPosition, showTextsProp, new GUIContent("鏄剧ず鏂囨湰鍒楄〃"));
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;
            }
            else if (type == SettingBtnType.Slider)
            {
                EditorGUI.PropertyField(currentPosition, sliderRangeProp, new GUIContent("婊戝姩鑼冨洿"));
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;
                EditorGUI.PropertyField(currentPosition, sliderSuffixProp, new GUIContent("婊戝姩鍚庣紑"));
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;
            }

            EditorGUI.indentLevel--;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight; // 鎶樺彔琛岄珮搴?

        if (property.isExpanded)
        {
            height += EditorGUIUtility.singleLineHeight * 3; // 鍩虹瀛楁锛氭爣棰樸€佺被鍨嬨€佸€?
            height += LineSpacing * 3;

            SerializedProperty typeProp = property.FindPropertyRelative("type");
            SettingBtnType type = (SettingBtnType)typeProp.enumValueIndex;
            
            // 鏍规嵁绫诲瀷娣诲姞棰濆楂樺害
            if (type == SettingBtnType.Dropdown)
            {
                var count = property.FindPropertyRelative("showTexts").CountInProperty()+1;
                height += count*( (EditorGUIUtility.singleLineHeight) + LineSpacing);

            }
            else if (type == SettingBtnType.Slider)
            {
                height +=2*(EditorGUIUtility.singleLineHeight + LineSpacing);
            }
        }

        return height;
    }
}


#endif