// 文件名: ToonLitMouthEyeShaderGUI.cs
// 放置位置: 任意Editor文件夹下

using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ToonLitMouthEyeShaderGUI : ShaderGUI
{
    private MaterialEditor materialEditor;
    private MaterialProperty[] properties;

    // 存储每个模块的折叠状态
    private bool showHighLevel = false;

  

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        this.materialEditor = materialEditor;
        this.properties = properties;

        // 使用 SerializedObject 的标准模式
        materialEditor.serializedObject.Update();

        // 在此处调用你的绘制函数
        DrawMyCustomGUI(materialEditor, properties);

        // 将改动应用回材质
        materialEditor.serializedObject.ApplyModifiedProperties();
    }
    private void DrawMyCustomGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // 绘制自定义检视器
        DrawHighLevelSettings();
        DrawBaseColor();
        DrawMouth();
    }
    private void DrawHighLevelSettings()
    {
        showHighLevel = EditorGUILayout.BeginFoldoutHeaderGroup(showHighLevel, "高级设置");
        if (showHighLevel)
        {
            //GUILayout.Label("High Level Setting", EditorStyles.boldLabel);
            MaterialProperty isFace = FindProperty("_IsFace", properties);
            materialEditor.ShaderProperty(isFace, "是面部材质");

            MaterialProperty renderRef = FindProperty("_RenderRef", properties);
            materialEditor.ShaderProperty(renderRef, "写入缓冲");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(5);
    }

    private void DrawBaseColor()
    {

        MaterialProperty baseMap = FindProperty("_BaseMap", properties);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties);
        materialEditor.TexturePropertySingleLine(new GUIContent("颜色贴图", "颜色"), baseMap, baseColor);

        EditorGUILayout.Space(5);
    }

    private void DrawMouth()
    {

        MaterialProperty mouthMap = FindProperty("_MouthMap", properties);
        MaterialProperty expression = FindProperty("_Expression", properties);
        MaterialProperty column = FindProperty("_Column", properties);

        materialEditor.TexturePropertySingleLine(new GUIContent("嘴部贴图", "嘴部贴图"), mouthMap, expression);
        column.intValue = EditorGUILayout.IntField("每行数量", (int)column.floatValue);

        EditorGUILayout.Space(5);
    }

 
    private new MaterialProperty FindProperty(string name, MaterialProperty[] properties)
    {
        return ShaderGUI.FindProperty(name, properties);

    }

 
}