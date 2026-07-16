using System.Collections.Generic;
using System.Linq;
using Core;
using GameContract;
using Unity.BaseTool;


#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using static UnityEditor.Progress;
#endif


[CreateAssetMenu(fileName = "new Data", menuName = "Data/阵营配置")]
public class CampData_SO : ScriptableObject
{

  

    /// <summary>波次间隔</summary>
    //[Foldout("波次", true)]
    [InspectorName("波次间隔(:秒)")]
    public int WaveCool;
    /// <summary>波次使用物体</summary>
    [InspectorName("波次使用物体")]
    public List<GameObject> WaveUseObject;

    [InspectorName("波次模板")]
    public List<CampTemplate> templates;

    //[Foldout("巡逻队",true)]

    [Space(16)]
    [InspectorName("巡逻队创建基准值(:秒)")]
    public int PatrolCreatValue;
    [InspectorName("巡逻队模板")]
    public List<PatrolCfg> patrolCfgs;

    [Foldout("基本", true)]
    /// <summary>类型</summary>
    [InspectorName("类型")]
    public EnemyVarietyType enemyVarietyType;
    /// <summary>名称</summary>
    [InspectorName("名称")]
    public string ShowName;
    /// <summary>图标</summary>
    [InspectorName("图标")]
    public Sprite Sprite;
    /// <summary>击杀图标</summary>
    [InspectorName("击杀图标")]
    public Sprite KillSprite;

    /// <summary>颜色</summary>
    [InspectorName("颜色")]
    public Color Color;

    /// <summary>语音后缀</summary>
    [InspectorName("语音后缀")]
    public string Suffix;

    /// <summary>描述</summary>
    [TextArea(3, 5)]
    public string Desc;


    [Foldout("任务", true)]

    [InspectorName("允许的主线类型")]
    public MissionEnum[] mainTypes;
    [InspectorName("允许的额外类型")]
    public MissionEnum[] extraTypes;

    [InspectorName("巢穴类型")]
    public MissionEnum[] nestTypes;

    [InspectorName("备份的主线类型")]
    public MissionEnum[] mainTypesBackup;



    /*
    [ContextMenu("转换")]
    public void trans()
    {
        Templates.ForEach((key, value) => templates.Add(new() {
            name = key,
            //Template = new(value.Template),
            PatrolTemplate = new(value.PatrolTemplate)
        }));
    }*/


    [System.Serializable]
    public class PatrolCfg
    {
        public string name;
        public List<SKVP<UnitTier, int>> units;
    }

    [System.Serializable]
    public class CampTemplate
    {
        public string name;

        public List<TierCfg> template;

        [Header("巡逻队类型")]
        public List<string> PatrolTemplate;
    }

    [System.Serializable]
    public struct UnitWeightCfg
    {
        /// <summary>预制体 </summary>
        public GameObject unit;
        /// <summary>概率 </summary>
        public int weight;
        /// <summary>占据补给量 </summary>
        public int size;
    }

    [System.Serializable]
    public struct TierCfg
    {
        public UnitTier tier;
        //[InspectorName("权重")]
        public int weight;
        //[Header("单位类型")]
        public List<UnitWeightCfg> unitWeights;
    }


}




#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(CampData_SO.UnitWeightCfg))]
public class UnitWeightCfgDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty unitProp = property.FindPropertyRelative("unit");
        SerializedProperty weightProp = property.FindPropertyRelative("weight");
        SerializedProperty sizeProp = property.FindPropertyRelative("size");

        float spacing = 5f;
        float lineHeight = EditorGUIUtility.singleLineHeight;

        // 计算标签宽度
        float unitLabelW = GUI.skin.label.CalcSize(new GUIContent("预制体")).x + 4f;
        float weightLabelW = GUI.skin.label.CalcSize(new GUIContent("概率")).x + 4f;
        float sizeLabelW = GUI.skin.label.CalcSize(new GUIContent("人口")).x + 4f;

        float totalLabelW = unitLabelW + weightLabelW + sizeLabelW;
        float fieldW = (position.width - totalLabelW - spacing * 2) / 3f;
        float curX = position.x;

        // 预制体
        EditorGUI.LabelField(new Rect(curX, position.y, unitLabelW, lineHeight), "预制体");
        curX += unitLabelW;
        EditorGUI.PropertyField(new Rect(curX, position.y, fieldW, lineHeight), unitProp, GUIContent.none);
        curX += fieldW + spacing;

        // 概率
        EditorGUI.LabelField(new Rect(curX, position.y, weightLabelW, lineHeight), "概率");
        curX += weightLabelW;
        EditorGUI.PropertyField(new Rect(curX, position.y, fieldW, lineHeight), weightProp, GUIContent.none);
        curX += fieldW + spacing;

        // 人口
        EditorGUI.LabelField(new Rect(curX, position.y, sizeLabelW, lineHeight), "人口");
        curX += sizeLabelW;
        EditorGUI.PropertyField(new Rect(curX, position.y, fieldW, lineHeight), sizeProp, GUIContent.none);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}

/// <summary>
/// PatrolCfg 绘制器：平铺 units 列表（不显示默认折叠），每项右边加删除按钮，右下角加添加按钮
/// </summary>
[CustomPropertyDrawer(typeof(CampData_SO.PatrolCfg))]
public class PatrolCfgDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        float h = lineHeight; // name

        SerializedProperty unitsProp = property.FindPropertyRelative("units");
        if (unitsProp != null)
        {
            h += lineHeight; // 标题
            for (int i = 0; i < unitsProp.arraySize; i++)
            {
                h += EditorGUI.GetPropertyHeight(unitsProp.GetArrayElementAtIndex(i)) + EditorGUIUtility.standardVerticalSpacing;
            }
            h += lineHeight; // 添加按钮行
        }
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float y = position.y;
        float x = position.x;
        float w = position.width;

        // name
        SerializedProperty nameProp = property.FindPropertyRelative("name");
        Rect nameRect = new Rect(x, y, w, lineHeight);
        EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("名称"));
        y += lineHeight + spacing;

        // ======== units 平铺 ========
        SerializedProperty unitsProp = property.FindPropertyRelative("units");

        // 标题
        Rect titleRect = new Rect(x, y, w, lineHeight);
        EditorGUI.LabelField(titleRect, "单位配置", EditorStyles.boldLabel);
        y += lineHeight + spacing;

        for (int i = 0; i < unitsProp.arraySize; i++)
        {
            SerializedProperty itemProp = unitsProp.GetArrayElementAtIndex(i);
            float itemH = EditorGUI.GetPropertyHeight(itemProp);

            float innerW = w - 56;
            Rect itemRect = new Rect(x + 2, y, innerW, itemH);
            EditorGUI.PropertyField(itemRect, itemProp, GUIContent.none);

            // 删除按钮（右侧）
            Rect delBtnRect = new Rect(x + w - 52, y + 2, 50, lineHeight);
            if (GUI.Button(delBtnRect, "删除"))
            {
                unitsProp.DeleteArrayElementAtIndex(i);
                unitsProp.serializedObject.ApplyModifiedProperties();
                EditorGUI.EndProperty();
                return;
            }

            y += itemH + spacing;
        }

        // 添加按钮（右下角）
        Rect addBtnRect = new Rect(x + w - 80, y, 80, lineHeight);
        if (GUI.Button(addBtnRect, "+ 添加项"))
        {
            unitsProp.arraySize++;
            unitsProp.serializedObject.ApplyModifiedProperties();
        }
        y += lineHeight + spacing;

        EditorGUI.EndProperty();
    }
}

/// <summary>
/// TierCfg 的绘制器：平铺 unitWeights 列表（不显示默认折叠），每项右边加删除按钮，右下角加添加按钮
/// </summary>
[CustomPropertyDrawer(typeof(CampData_SO.TierCfg))]
public class TierCfgDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        // tier + weight 合并为 1 行
        float h = lineHeight;

        SerializedProperty uwProp = property.FindPropertyRelative("unitWeights");
        if (uwProp != null)
        {
            // unitWeights 标题行
            h += lineHeight;
            for (int i = 0; i < uwProp.arraySize; i++)
            {
                h += EditorGUI.GetPropertyHeight(uwProp.GetArrayElementAtIndex(i)) + EditorGUIUtility.standardVerticalSpacing;
            }
            // 添加按钮行
            h += lineHeight;
        }
        h += EditorGUIUtility.singleLineHeight;//留空
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float y = position.y;
        float x = position.x;
        float w = position.width;

        y += EditorGUIUtility.singleLineHeight/2;
        // tier + weight 合并到一行（紧凑样式）
        SerializedProperty tierProp = property.FindPropertyRelative("tier");
        SerializedProperty weightProp = property.FindPropertyRelative("weight");

        float tierLabelW = GUI.skin.label.CalcSize(new GUIContent("层级")).x + 4f;
        float weightLabelW = GUI.skin.label.CalcSize(new GUIContent("权重")).x + 4f;
        float fieldW = (w - tierLabelW - weightLabelW - 10) / 2;
        float curX = x;

        // tier 标签 + 字段
        EditorGUI.LabelField(new Rect(curX, y, tierLabelW, lineHeight), "层级");
        curX += tierLabelW;
        EditorGUI.PropertyField(new Rect(curX, y, fieldW, lineHeight), tierProp, GUIContent.none);
        curX += fieldW + 10;

        // weight 标签 + 字段（用 IntField 避免 labelWidth 干扰）
        EditorGUI.LabelField(new Rect(curX, y, weightLabelW, lineHeight), "权重");
        curX += weightLabelW;
        weightProp.intValue = EditorGUI.IntField(new Rect(curX, y, fieldW, lineHeight), GUIContent.none, weightProp.intValue);
        y += lineHeight + spacing;

        // ======== unitWeights 平铺 ========
        SerializedProperty uwProp = property.FindPropertyRelative("unitWeights");

        // 标题
        Rect uwTitleRect = new Rect(x, y, w, lineHeight);
        EditorGUI.LabelField(uwTitleRect, "单位类型", EditorStyles.boldLabel);
        y += lineHeight + spacing;

        // 每个 UnitWeightCfg 项
        for (int i = 0; i < uwProp.arraySize; i++)
        {
            SerializedProperty itemProp = uwProp.GetArrayElementAtIndex(i);
            float itemH = EditorGUI.GetPropertyHeight(itemProp);
            /*
            // 背景框
            Rect bgRect = new Rect(x, y, w, itemH);
            EditorGUI.HelpBox(bgRect, "", MessageType.None);
            */
            // 内容区域（留出删除按钮空间）
            float innerW = w - 56;
            Rect itemRect = new Rect(x + 2, y, innerW, itemH);
            EditorGUI.PropertyField(itemRect, itemProp, GUIContent.none);

            // 删除按钮（右侧）
            Rect delBtnRect = new Rect(x + w - 52, y + 2, 50, lineHeight);
            if (GUI.Button(delBtnRect, "删除"))
            {
                uwProp.DeleteArrayElementAtIndex(i);
                uwProp.serializedObject.ApplyModifiedProperties();
                EditorGUI.EndProperty();
                return;
            }

            y += itemH + spacing;
        }
        y += spacing;
        // 添加按钮（右下角）
        Rect addBtnRect = new Rect(x + w - 80, y, 80, lineHeight);
        if (GUI.Button(addBtnRect, "+ 添加项"))
        {
            uwProp.arraySize++;
            uwProp.serializedObject.ApplyModifiedProperties();
        }
        y += lineHeight + spacing;

        EditorGUI.EndProperty();
    }
}

/// <summary>
/// CampTemplate 绘制器：只自定义 PatrolTemplate 为下拉框，name 和 template 走默认
/// </summary>
[CustomPropertyDrawer(typeof(CampData_SO.CampTemplate))]
public class CampTemplateDrawer : PropertyDrawer
{
    // 用 propertyPath 作为 key 来保存每个实例的折叠状态
    private static readonly Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

    private bool IsExpanded(SerializedProperty property)
    {
        string key = property.propertyPath;
        if (!foldoutStates.TryGetValue(key, out bool expanded))
        {
            foldoutStates[key] = true;
            return true;
        }
        return expanded;
    }

    private void SetExpanded(SerializedProperty property, bool expanded)
    {
        foldoutStates[property.propertyPath] = expanded;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        if (!IsExpanded(property))
        {
            // 折叠时只显示标题行
            return lineHeight;
        }

        float h = lineHeight; // name

        SerializedProperty templateProp = property.FindPropertyRelative("template");
        if (templateProp != null)
        {
            h += EditorGUI.GetPropertyHeight(templateProp, true);
        }

        SerializedProperty patrolTemplateProp = property.FindPropertyRelative("PatrolTemplate");
        if (patrolTemplateProp != null)
        {
            h += lineHeight; // 标题
            for (int i = 0; i < patrolTemplateProp.arraySize; i++)
            {
                h += lineHeight; // 每行下拉框
            }
            h += lineHeight; // 添加按钮行
        }
        h += lineHeight;
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float y = position.y;
        float x = position.x;
        float w = position.width;

        // 可折叠的标题行
        SerializedProperty nameProp = property.FindPropertyRelative("name");
        string title = string.IsNullOrEmpty(nameProp.stringValue) ? "(未命名)" : nameProp.stringValue;

        bool expanded = IsExpanded(property);
        Rect foldoutRect = new Rect(x, y, w, lineHeight);
        expanded = EditorGUI.Foldout(foldoutRect, expanded, title, true);
        SetExpanded(property, expanded);
        y += lineHeight + spacing;

        if (!expanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        // name（已作为标题显示，这里用缩进再显示一次方便编辑）
        EditorGUI.indentLevel++;
        Rect nameRect = new Rect(x, y, w, lineHeight);
        EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("名称"));
        y += lineHeight + spacing;

        // template (List<TierCfg>) — 走默认，由 TierCfgDrawer 处理
        SerializedProperty templateProp = property.FindPropertyRelative("template");
        float templateH = EditorGUI.GetPropertyHeight(templateProp, true);
        Rect templateRect = new Rect(x, y, w, templateH);
        EditorGUI.PropertyField(templateRect, templateProp, new GUIContent("单位模板"), true);
        y += templateH + spacing;

        // ======== PatrolTemplate (List<string>) 下拉框 ========
        SerializedProperty patrolTemplateProp = property.FindPropertyRelative("PatrolTemplate");

        // 标题
        Rect ptTitleRect = new Rect(x, y, w, lineHeight);
        EditorGUI.LabelField(ptTitleRect, "巡逻队类型", EditorStyles.boldLabel);
        y += lineHeight + spacing;

        // 获取 patrolCfgs 的 name 列表
        SerializedProperty patrolCfgsProp = property.serializedObject.FindProperty("patrolCfgs");
        string[] patrolNames = new string[patrolCfgsProp.arraySize];
        for (int i = 0; i < patrolCfgsProp.arraySize; i++)
        {
            string n = patrolCfgsProp.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            patrolNames[i] = string.IsNullOrEmpty(n) ? $"(未命名 {i})" : n;
        }

        string[] options = new string[patrolNames.Length + 1];
        options[0] = "无";
        for (int i = 0; i < patrolNames.Length; i++)
            options[i + 1] = patrolNames[i];

        for (int i = 0; i < patrolTemplateProp.arraySize; i++)
        {
            SerializedProperty itemProp = patrolTemplateProp.GetArrayElementAtIndex(i);

            int selectedIdx = 0;
            string curVal = itemProp.stringValue;
            for (int j = 0; j < patrolNames.Length; j++)
            {
                if (patrolNames[j] == curVal)
                {
                    selectedIdx = j + 1;
                    break;
                }
            }

            Rect itemRect = new Rect(x + 10, y, w - 70, lineHeight);
            Rect delRect = new Rect(x + w - 54, y, 50, lineHeight);

            int newIdx = EditorGUI.Popup(itemRect, "巡逻队", selectedIdx, options);
            if (newIdx != selectedIdx)
            {
                itemProp.stringValue = newIdx == 0 ? "" : patrolNames[newIdx - 1];
                itemProp.serializedObject.ApplyModifiedProperties();
            }

            if (GUI.Button(delRect, "删除"))
            {
                patrolTemplateProp.DeleteArrayElementAtIndex(i);
                patrolTemplateProp.serializedObject.ApplyModifiedProperties();
                EditorGUI.EndProperty();
                return;
            }

            y += lineHeight + spacing;
        }

        // 添加按钮（右下角）
        Rect addPTRect = new Rect(x + w - 80, y, 80, lineHeight);
        if (GUI.Button(addPTRect, "+ 添加项"))
        {
            patrolTemplateProp.arraySize++;
            patrolTemplateProp.serializedObject.ApplyModifiedProperties();
        }
        y += lineHeight + spacing;

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
#endif
