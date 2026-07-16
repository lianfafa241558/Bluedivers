using System;
using System.Collections.Generic;
using System.Linq;
using PEMaths;
using Unity.BaseTool;
using UnityEngine;
using Utils;
using Tool = Utils.Tool;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.FPS.Game
{
    [Serializable]
    public class WeaponCfg
    {
        [SerializeField]
        private List<KVP<WeaponAttrType, float>> _fakeAttrs = new();

        private Dictionary<WeaponAttrType, WeaponAttribute> attrs;

        public WeaponAttribute this[WeaponAttrType type] {
            get
            {
                if (attrs == null) Init();
                if(attrs.TryGetValue(type,out var re))
                {
                    return re;
                }
                return default;
            }
        }

        public void SetPara(WeaponAttrType type, float value)
        {

            var index = 0;
            if (index>-1)
            {
                _fakeAttrs[index].Value=value;
            }
            else
            {
                _fakeAttrs.Add(new(type,value));
            }
          
        }

        public WeaponAttribute Add(WeaponAttrType type, float value)
        {
            var item = WeaponAttributeFactory.Create(type, new(value));
            attrs.Add(type, item);
            return item;
        }

        public void Init()
        {
            attrs = new();
            for (int i =0;i< _fakeAttrs.Count; ++i)
            {
                var item = WeaponAttributeFactory.Create(_fakeAttrs[i].Key, new(_fakeAttrs[i].Value));
                attrs.Add(_fakeAttrs[i].Key, item);
                //attrs.Add(_fakeAttrs[i].Key, WeaponAttributeFactory.Create(_fakeAttrs[i].Key, new(_fakeAttrs[i].Value)));
            }
        }



        public void ModifierValue(WeaponAttrType type, ModifierType modifier, PEInt value)
        {
            if(attrs.TryGetValue(type,out var re))
            {
                re.AddModifier(modifier, value);
            }
            else
            {
                attrs.Add(type, WeaponAttributeFactory.Create(type,0));
                re.AddModifier(modifier, value);
            }
        }

        #region 初始化

        private List<WeaponAttrType> TypePara(WeaponShootType type, WeaponFlag flag)
        {
            var re = new List<WeaponAttrType>(){ 
                WeaponAttrType.Ammo, WeaponAttrType.Magazine, WeaponAttrType.ShootInterval,
                WeaponAttrType.BulletsSpreadAngle,WeaponAttrType.ReloadTime
            };
            switch (type)
            {
                case WeaponShootType.Charge:
                    re.AddRange(new List<WeaponAttrType>() { WeaponAttrType.ChargeLowestStage, WeaponAttrType.ChargeHigheststage ,});
                    break;
                case WeaponShootType.Laser:
                    re.AddRange(new List<WeaponAttrType>() { WeaponAttrType.LaserWaitTime });
                    break;
                case WeaponShootType.Lock:
                    re.AddRange(new List<WeaponAttrType>() { 
                        WeaponAttrType.LockDistance,WeaponAttrType.LockRange,WeaponAttrType.LockLayers,
                        WeaponAttrType.LockPerCount,WeaponAttrType.LockInterval
                    });
                    break;
            }
            Tool.ForEachFlag(flag, (e)=> {
                switch (e)
                {
                    case WeaponFlag.AutomaticReload:
                        re.AddRange(new List<WeaponAttrType>() { WeaponAttrType.AutoReloadTime,WeaponAttrType.AutoReloadSpeed });
                        break;
                }
            });



            return re;
        }

        public void Reset(WeaponShootType type, WeaponFlag flag)
        {
            var re= TypePara(type, flag);
            for(int i = _fakeAttrs.Count-1; i >=0; --i)
            {
                if (!re.Contains(_fakeAttrs[i].Key))
                {
                    _fakeAttrs.RemoveAt(i);
                }
            }
            foreach(var item in re)
            {
                //if (!_fakeAttrs.Contains(item))
                //{
                //    _fakeAttrs.Add(new(item,0));
                //}
            }
            
        }
        #endregion
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(WeaponCfg))]
    public class WeaponCfgDrawer : PropertyDrawer
    {

        // 在类中添加字段来跟踪隐藏状态
        private bool hideNotFoundItems = true;
        private static readonly WeaponAttrType[] AllValues = Enum.GetValues(typeof(WeaponAttrType)).Cast<WeaponAttrType>().ToArray();


        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            // 开始绘制
            EditorGUI.BeginProperty(position, label, property);

            // 修改标题行绘制代码
            // 绘制标题
            Rect currentRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // 创建标题区域和按钮区域
            Rect labelRect2 = new Rect(currentRect.x, currentRect.y, currentRect.width - 200, EditorGUIUtility.singleLineHeight);
            Rect buttonRect1 = new Rect(currentRect.x + currentRect.width - 95, currentRect.y, 80, EditorGUIUtility.singleLineHeight);
            Rect buttonRect2 = new Rect(currentRect.x + currentRect.width - 190, currentRect.y, 90, EditorGUIUtility.singleLineHeight);
            Rect countRect = new Rect(currentRect.x + 100, currentRect.y, 100, EditorGUIUtility.singleLineHeight);

            // 绘制标题标签
            EditorGUI.LabelField(labelRect2, label, EditorStyles.boldLabel);

            // 绘制计数
            // 获取 DisplayDic 的内部存储结构
            SerializedProperty arrProp = property.FindPropertyRelative("_fakeAttrs");
            EditorGUI.LabelField(countRect, "长度: " + arrProp.arraySize);

            // 绘制"清空字典"按钮
            if (GUI.Button(buttonRect1, "清空"))
            {
                if (arrProp != null)
                {
                    arrProp.ClearArray();
                    arrProp.serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(arrProp.serializedObject.targetObject);
                }
            }

            // 绘制"隐藏未找到项"切换按钮
            hideNotFoundItems = GUI.Toggle(buttonRect2, hideNotFoundItems, "隐藏未开启属性", "Button");

            currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;


            // 获取所有枚举值
            WeaponAttrType[] enumValues = (WeaponAttrType[])System.Enum.GetValues(typeof(WeaponAttrType));

            // 遍历枚举值，绘制对应的属性
            foreach (WeaponAttrType attrType in enumValues)
            {
                // 在 DisplayDic 中查找对应的键值对
                bool found = false;

                for (int i = 0; i < arrProp.arraySize; i++)
                {
                    SerializedProperty kvpProp = arrProp.GetArrayElementAtIndex(i);
                    SerializedProperty keyProp = kvpProp.FindPropertyRelative("Key");

                    if (keyProp != null && keyProp.enumValueIndex == Array.IndexOf(AllValues, attrType))
                    {
                        SerializedProperty valueProp = kvpProp.FindPropertyRelative("Value");

                        // 绘制属性
                        GUIContent attrLabel = new GUIContent(GetEnumString(attrType));

                        // 计算属性字段和删除按钮的区域
                        Rect fieldRect = new Rect(currentRect.x, currentRect.y, currentRect.width * 0.7f, EditorGUIUtility.singleLineHeight);
                        Rect deleteRect = new Rect(currentRect.x + currentRect.width * 0.7f + 5, currentRect.y, currentRect.width * 0.3f - 5, EditorGUIUtility.singleLineHeight);

                        // 绘制属性字段
                        valueProp.floatValue = EditorGUI.FloatField(fieldRect, attrLabel, valueProp.floatValue);


                        // 绘制删除按钮
                        if (GUI.Button(deleteRect, "删除"))
                        {
                            // 从数组中移除该项
                            if (arrProp != null && i < arrProp.arraySize)
                            {
                                // 删除指定索引的元素
                                arrProp.DeleteArrayElementAtIndex(i);
                                arrProp.serializedObject.ApplyModifiedProperties();
                                EditorUtility.SetDirty(arrProp.serializedObject.targetObject);
                            }
                        }

                        found = true;
                        break;
                        
                    }
                }

                // 如果 DisplayDic 中没有该键，则显示添加按钮
                if (!found&& !hideNotFoundItems)
                {
                    // 在同一行内并排显示标签和按钮
                    Rect labelRect = new Rect(currentRect.x, currentRect.y, currentRect.width * 0.7f, EditorGUIUtility.singleLineHeight);
                    Rect buttonRect = new Rect(currentRect.x + currentRect.width * 0.7f + 5, currentRect.y, currentRect.width * 0.3f - 5, EditorGUIUtility.singleLineHeight);

                    // 显示枚举名称（禁用状态）
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.LabelField(labelRect, GetEnumString(attrType));
                    EditorGUI.EndDisabledGroup();

                    // 显示添加按钮
                    if (GUI.Button(buttonRect, "添加"))
                    {
                        // 在数组末尾添加新的 KVP 元素
                        int index = arrProp.arraySize;
                        arrProp.arraySize++;

                        // 设置新键值对
                        SerializedProperty newKvp = arrProp.GetArrayElementAtIndex(index);
                        SerializedProperty newKey = newKvp.FindPropertyRelative("Key");
                        SerializedProperty newValue = newKvp.FindPropertyRelative("Value");

                        if (newKey != null)
                        {
                            newKey.enumValueIndex = Array.IndexOf(AllValues, attrType);
                        }

                        // 标记属性已修改
                        arrProp.serializedObject.ApplyModifiedProperties();

                        // 强制刷新 Inspector
                        EditorUtility.SetDirty(arrProp.serializedObject.targetObject);
                    }
                }

                if (found || !hideNotFoundItems) currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // 计算总高度：标题 + 每个枚举项的高度
            WeaponAttrType[] enumValues = (WeaponAttrType[])System.Enum.GetValues(typeof(WeaponAttrType));
            int itemCount = enumValues.Length;
            if (hideNotFoundItems)
            {
                itemCount = property.FindPropertyRelative("_fakeAttrs").arraySize;
            }
            var lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            return lineHeight * (itemCount + 1);
        }

        public string GetEnumString(System.Enum value)
        {
            var fieldInfo = value.GetType().GetField(value.ToString());
            var attribute = fieldInfo.GetCustomAttributes(typeof(InspectorNameAttribute), false);
            return attribute.Length > 0 ? ((InspectorNameAttribute)attribute[0]).displayName : value.ToString();

        }
    }
#endif
}