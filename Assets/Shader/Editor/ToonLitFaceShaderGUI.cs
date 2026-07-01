// 文件名: ToonLitFaceShaderGUI.cs
// 放置位置: 任意Editor文件夹下

using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ToonLitFaceShaderGUI : ShaderGUI
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
        DrawOutline();
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

    private void DrawOutline()
    {
        MaterialProperty celShadeMidPoint = FindProperty("_CelShadeMidPoint", properties);
        materialEditor.ShaderProperty(celShadeMidPoint, "阴影切面的系数");
        


        MaterialProperty useAverNormal = FindProperty("_UseAverNormal", properties);
        materialEditor.ShaderProperty(useAverNormal, "使用平均化法线");

        MaterialProperty outlineWidth = FindProperty("_OutlineWidth", properties);
        materialEditor.ShaderProperty(outlineWidth, "描边宽度 (World Space)");

        MaterialProperty outlineColor = FindProperty("_OutlineColor", properties);
        materialEditor.ShaderProperty(outlineColor, "描边颜色");


        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }



    private new MaterialProperty FindProperty(string name, MaterialProperty[] properties)
    {
        return ShaderGUI.FindProperty(name, properties);
    }

 
}