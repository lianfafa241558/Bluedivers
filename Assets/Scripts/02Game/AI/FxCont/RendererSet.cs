using System.Collections.Generic;
using UnityEngine;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FPSGame.AI {

    public struct RendererIndexData {
        public Renderer Renderer;
        public int MaterialIndex;

        public RendererIndexData(Renderer renderer, int index) {
            Renderer = renderer;
            MaterialIndex = index;
        }
    }
    [System.Serializable]
    public class RendererSet {
        public MaterialPropertyBlock mpb;

        public MPBTypeEnum type;
        public OccasionTypeEnum occasion;
        public Material material;
        public string colorName;
        //切换时
        [ColorUsage(true, true)]
        public Color defaultColor;

        public OccasionTypeEnum switchOccasion;
        [ColorUsage(true, true)]//alpha和hdr
        public Color switchColor;
        private OccasionTypeEnum lastOccasion;

        //触发时

        [GradientUsage(true)]
        public Gradient gradient;
        [InspectorName("持续时间")]
        public float duration = 0.1f;

        public float lastTriggerTime { get;set; }= float.NegativeInfinity;
        List<(Renderer,int)> renderers=new();

        // 颜色属性名运行时缓存（用 bool 标记解析状态，勿用默认值当哨兵）
        [System.NonSerialized]
        private int colorId;
        [System.NonSerialized]
        private bool colorIdResolved;

        private int GetColorId() {
            if (!colorIdResolved) {
                string name = string.IsNullOrEmpty(colorName)
                    ? (type == MPBTypeEnum.Switch ? "_EmissionColor" : "_HitColor")
                    : colorName;
                colorId = Shader.PropertyToID(name);
                colorIdResolved = true;
            }
            return colorId;
        }


        public enum MPBTypeEnum {
            Trigger,
            Switch,
        }

        public void Add(Renderer renderer, int materialIndex) {
            renderers.Add((renderer,materialIndex));
            if(!mpb.IsValid())mpb = new MaterialPropertyBlock();
        }

        public void Trigger(OccasionTypeEnum occasion) {
            switch (type) {
                case MPBTypeEnum.Trigger:
                    if(this.occasion== occasion) {
                        lastTriggerTime = Time.time;
                    }
                    break;
                case MPBTypeEnum.Switch:
                    if (this.occasion == occasion) {
                        if (mpb.IsValid()) {
                            lastOccasion = occasion;
                            lastTriggerTime = Time.time;
                        }
                    }
                    else if (switchOccasion == occasion && mpb.IsValid()) {
                        lastOccasion = occasion;
                        lastTriggerTime = Time.time;
                    }
                    break;
            }
        }
        public void Update() {
            if (!mpb.IsValid()) return;
            switch (type) {
                case MPBTypeEnum.Trigger:
                    if ((Time.time - lastTriggerTime) <= duration) {
                        Color currentColor = gradient.Evaluate((Time.time - lastTriggerTime) / duration);
                        mpb.SetColor(GetColorId(), currentColor);
                        for (int i = 0; i < renderers.Count; ++i) {
                            renderers[i].Item1.SetPropertyBlock(mpb, renderers[i].Item2);
                        }
                        mpb.Clear();
                    }
                    break;
                case MPBTypeEnum.Switch:
                    if ((Time.time - lastTriggerTime) <= 2) {
                        var a = lastOccasion == switchOccasion ? defaultColor : switchColor;
                        var b = lastOccasion != switchOccasion ? defaultColor : switchColor;
                        Color currentColor = Color.Lerp(a,b,(Time.time - lastTriggerTime)/2);
                        mpb.SetColor(GetColorId(), currentColor);
                        for (int i = 0; i < renderers.Count; ++i) {
                            renderers[i].Item1.SetPropertyBlock(mpb, renderers[i].Item2);
                        }
                        mpb.Clear();
                    }
                    break;
            }

        }

    }

#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(RendererSet))]
    public class RendererSetEditor : PropertyDrawer {
        private const float lineHeight = 20f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            var typeProp = property.FindPropertyRelative("type");
            var typeValue = (RendererSet.MPBTypeEnum)typeProp.enumValueIndex;

            // 绘制折叠箭头和标签
            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, lineHeight),
                property.isExpanded, label);

            if (!property.isExpanded) {
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
            switch (typeValue) {
                case RendererSet.MPBTypeEnum.Switch:
                    DrawSwitchProperties(position, property, ref y);
                    break;

                case RendererSet.MPBTypeEnum.Trigger:
                    DrawTriggerProperties(position, property, ref y);
                    break;
            }

            EditorGUIUtility.labelWidth = LabelWidth;
            EditorGUI.EndProperty();
        }

        private void DrawSwitchProperties(Rect position, SerializedProperty property, ref float y) {

            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, lineHeight),
                property.FindPropertyRelative("material"));
            y += lineHeight*1.1f;
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, lineHeight),
                property.FindPropertyRelative("colorName"));
            y += lineHeight*1.5f;

            float LabelWidth = EditorGUIUtility.labelWidth;

            EditorGUI.PropertyField(
                new Rect(position.x + 0, y, (position.width ) / 2 - 0, lineHeight),
                property.FindPropertyRelative("occasion"));
            EditorGUI.PropertyField(
                new Rect(position.x + 0 + (position.width ) / 2 + 20, y, (position.width ) / 2 - 20, lineHeight),
                property.FindPropertyRelative("defaultColor"));
            y += lineHeight;
            EditorGUI.PropertyField(
                new Rect(position.x + 0, y, (position.width  ) / 2 - 0, lineHeight),
                property.FindPropertyRelative("switchOccasion"));
            EditorGUI.PropertyField(
                new Rect(position.x + 0 + (position.width ) / 2 + 20, y, (position.width ) / 2 - 20, lineHeight),
                property.FindPropertyRelative("switchColor"));
            y += lineHeight;



        }

        private void DrawTriggerProperties(Rect position, SerializedProperty property, ref float y) {
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

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            if (!property.isExpanded)
                return lineHeight;

            var typeProp = property.FindPropertyRelative("type");
            int lineCount = 3; // 基础属性(occasion+material+type)

            if (typeProp.enumValueIndex == (int)RendererSet.MPBTypeEnum.Switch)
                lineCount += 3; // switch模式属性
            else
                lineCount += 3; // trigger模式属性

            // 如果是数组中的元素则增加额外间距
            if (property.propertyPath.Contains(".Array.data["))
                lineCount += 1;

            return lineCount * lineHeight;
        }
    }
#endif


}