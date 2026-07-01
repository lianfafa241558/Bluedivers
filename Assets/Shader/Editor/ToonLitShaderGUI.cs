using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ToonLitMainShaderGUI : ShaderGUI
{
    private MaterialEditor materialEditor;
    private MaterialProperty[] properties;

    // 存储每个模块的折叠状态
    private bool showHighLevel = false;
    private bool showBaseColor = false;
    private bool showMouth = false;
    private bool showDissolve = false;
    private bool showHit = false;
    private bool showColour = false;
    private bool showEmission = false;
    private bool showSpec = false;
    private bool showOcclusion = false;
    private bool showLighting = false;
    private bool showShadow = false;
    private bool showOutline = false;

    // 需要同步关键字的Toggle属性名与对应的关键字
    private Dictionary<string, string> toggleKeywordMap = new Dictionary<string, string>()
    {
        { "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS" },
        { "_UseMouthMap", "_USEMOUTHMAP" },
        { "_UseAlphaClipping", "_USEALPHACLIPPING" },
        { "_UseAlphaUV", "_USEALPHAUV" },
        { "_UseColour", "_USECOLOUR" },
        { "_UseEmission", "_USEEMISSION" },
        { "_EmissionMaskAddite", "_EMISSIONMASKADDITIVE" },  // 注意拼写与Shader中保持一致
        { "_UseOcclusion", "_USEOCCLUSION" },
        { "_ReverseOcclusionColor", "_REVERSEOCCLUSIONCOLOR" },
        { "_WhiteFocusOutline", "_WHITEFOCOUTLINE" },
        { "_FixOutlineColor", "_FIXOUTLINECOLOR" },
        { "_UseAverNormal", "_USEAVERNORMAL" }
    };

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
        //DrawMouth();
        DrawDissolve();
        DrawHit();
        //DrawColour();
        DrawEmission();
        DrawSpec();
        DrawOcclusion();
        DrawLighting();
        DrawShadowMapping();
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

        showBaseColor = EditorGUILayout.BeginFoldoutHeaderGroup(showBaseColor, "基础");
        if (showBaseColor)
        {
            MaterialProperty baseMap = FindProperty("_BaseMap", properties);
            materialEditor.TextureProperty(baseMap, "颜色贴图");

            MaterialProperty baseColor = FindProperty("_BaseColor", properties);
            materialEditor.ShaderProperty(baseColor, "颜色");

            MaterialProperty blendingScale = FindProperty("_BlendingScale", properties);
            materialEditor.ShaderProperty(blendingScale, "混合程度");
            MaterialProperty useUV1 = FindProperty("_UseUV1", properties);
            materialEditor.ShaderProperty(useUV1, "使用UV1用于混合和溶解");

            MaterialProperty blendingMap = FindProperty("_BlendingMap", properties);
            materialEditor.TextureProperty(blendingMap, "混合纹理");


        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawMouth()
    {

        showMouth = EditorGUILayout.BeginFoldoutHeaderGroup(showMouth, "嘴部");
        if (showMouth)
        {
            EditorGUI.indentLevel++;
            MaterialProperty useMouthMap = FindProperty("_UseMouthMap", properties);
            materialEditor.ShaderProperty(useMouthMap, "使用嘴部");

            MaterialProperty mouthMap = FindProperty("_MouthMap", properties);
            MaterialProperty expression = FindProperty("_Expression", properties);
            MaterialProperty column = FindProperty("_Column", properties);

            materialEditor.TexturePropertySingleLine(new GUIContent("嘴部贴图", "嘴部贴图"), mouthMap, expression);
            column.intValue = EditorGUILayout.IntField("每行数量", (int)column.floatValue);
            EditorGUI.indentLevel--;

        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawDissolve()
    {

        showDissolve = EditorGUILayout.BeginFoldoutHeaderGroup(showDissolve, "溶解");
        if (showDissolve)
        {
            EditorGUI.indentLevel++;
            MaterialProperty useAlphaClipping = FindProperty("_UseAlphaClipping", properties);
            materialEditor.ShaderProperty(useAlphaClipping, "浣跨敤婧惰В");

            MaterialProperty useAlphaUV = FindProperty("_UseAlphaUV", properties);
            materialEditor.ShaderProperty(useAlphaUV, "浣跨敤UV杩涜婧惰В");

            MaterialProperty alphaMap = FindProperty("_AlphaMap", properties);
            MaterialProperty edgeColor = FindProperty("_EdgeColor", properties);
            materialEditor.TexturePropertySingleLine(new GUIContent("婧惰В璐村浘", "婧惰В璐村浘"), alphaMap, edgeColor);

            // _DissolveValue 鏄?Color 绫诲瀷锛屽疄闄呭彧浣跨敤 R 鍒嗛噺锛岀粯鍒朵负 Float Slider
            MaterialProperty dissolveValue = FindProperty("_DissolveValue", properties);
            float dissolve = dissolveValue.colorValue.r;
            EditorGUI.BeginChangeCheck();
            dissolve = EditorGUILayout.Slider("婧惰В绯绘暟", dissolve, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                dissolveValue.colorValue = new Color(dissolve, 0, 0, 0);
            }

            MaterialProperty edgeWidth = FindProperty("_EdgeWidth", properties);
            materialEditor.ShaderProperty(edgeWidth, "杈圭紭瀹藉害");
            EditorGUI.indentLevel--;
            
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawHit()
    {EditorGUI.indentLevel++;
        showHit = EditorGUILayout.BeginFoldoutHeaderGroup(showHit, "击中效果");
        if (showHit)
        {
            EditorGUI.indentLevel++;
            MaterialProperty hitColor = FindProperty("_HitColor", properties);
            materialEditor.ShaderProperty(hitColor, "HitColor");
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawColour()
    {
        showColour = EditorGUILayout.BeginFoldoutHeaderGroup(showColour, "色彩");
        if (showColour)
        {
            EditorGUI.indentLevel++;
            MaterialProperty useColour = FindProperty("_UseColour", properties);
            materialEditor.ShaderProperty(useColour, "使用'色彩'效果");

            MaterialProperty colourTex = FindProperty("_ColourTex", properties);
            MaterialProperty colourScale = FindProperty("_ColourScale", properties);
            materialEditor.TexturePropertySingleLine(new GUIContent("色彩贴图", "色彩贴图"), colourTex, colourScale);

            MaterialProperty colourMaskTex = FindProperty("_ColourMaskTex", properties);
            materialEditor.TexturePropertySingleLine(new GUIContent("色彩遮罩", "色彩遮罩"), colourMaskTex);

            EditorGUI.indentLevel--;
            
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawEmission()
    {
        showEmission = EditorGUILayout.BeginFoldoutHeaderGroup(showEmission, "自发光");
        if (showEmission)
        {
            EditorGUI.indentLevel++;
            MaterialProperty useEmission = FindProperty("_UseEmission", properties);
            materialEditor.ShaderProperty(useEmission, "使用自发光");
            
            MaterialProperty emissionMaskAddite = FindProperty("_EmissionMaskAddite", properties);
            materialEditor.ShaderProperty(emissionMaskAddite, "浣跨敤姣忎釜閫氶亾浣滀负钂欑増");

            if (emissionMaskAddite.floatValue>0.5f)
            {
                MaterialProperty emissionMapChannelMask = FindProperty("_EmissionMapChannelMask", properties);
                materialEditor.ShaderProperty(emissionMapChannelMask, "自发光贴图通道");
            }

            MaterialProperty emissionColor = FindProperty("_EmissionColor", properties);
            MaterialProperty emissionMulByBaseColor = FindProperty("_EmissionMulByBaseColor", properties);
            materialEditor.ShaderProperty(emissionMulByBaseColor, "根据原颜色发光");

            MaterialProperty emissionScale = FindProperty("_EmissionScale", properties);
            materialEditor.ShaderProperty(emissionScale, "发光系数");

            MaterialProperty emissionMap = FindProperty("_EmissionMap", properties);
            materialEditor.TexturePropertySingleLine(new GUIContent("自发光贴图", "自发光贴图"), emissionMap, emissionColor);

            EditorGUI.indentLevel--;
            
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }
    private void DrawSpec()
    {
        showSpec = EditorGUILayout.BeginFoldoutHeaderGroup(showSpec, "镜面反射");
        if (showSpec)
        {
            EditorGUI.indentLevel++;
            MaterialProperty usespec = FindProperty("_UseSpecular", properties);
            materialEditor.ShaderProperty(usespec, "使用镜面反射");

            MaterialProperty specMulByBaseColor = FindProperty("_SpecularMulByBaseColor", properties);
            materialEditor.ShaderProperty(specMulByBaseColor, "根据原颜色反光");

            MaterialProperty specMap = FindProperty("_SpecularMap", properties);
            MaterialProperty specColor = FindProperty("_SpecularColor", properties);
            materialEditor.TexturePropertySingleLine(new GUIContent("高光贴图", "高光贴图"), specMap, specColor);

            MaterialProperty _Smoothness = FindProperty("_Smoothness", properties);
            materialEditor.ShaderProperty(_Smoothness, "平滑度");

            MaterialProperty _SpecularSoftness = FindProperty("_SpecularSoftness", properties);
            materialEditor.ShaderProperty(_SpecularSoftness, "反射柔化");

            MaterialProperty _SpecularOffest = FindProperty("_SpecularOffest", properties);
            materialEditor.ShaderProperty(_SpecularOffest, "反射偏移");

            //MaterialProperty _AnisotropyScale = FindProperty("_AnisotropyScale", properties);
            //materialEditor.ShaderProperty(_AnisotropyScale, "各向异性系数");

            EditorGUI.indentLevel--;

        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }
 
    private void DrawOcclusion()
    {
        showOcclusion = EditorGUILayout.BeginFoldoutHeaderGroup(showOcclusion, "遮挡");
        if (showOcclusion)
        {
            EditorGUI.indentLevel++;
            MaterialProperty useOcclusion = FindProperty("_UseOcclusion", properties);
            materialEditor.ShaderProperty(useOcclusion, "使用遮挡");

            
            //MaterialProperty reverseOcclusionColor = FindProperty("_ReverseOcclusionColor", properties);
            //materialEditor.ShaderProperty(reverseOcclusionColor, "翻转遮挡强度");

            MaterialProperty occlusionStrength = FindProperty("_OcclusionStrength", properties);

            MaterialProperty occlusionMap = FindProperty("_OcclusionMap", properties);
            materialEditor.TexturePropertySingleLine(new GUIContent("遮挡贴图", "_OcclusionMap"), occlusionMap, occlusionStrength);

            MaterialProperty occlusionMapChannelMask = FindProperty("_OcclusionMapChannelMask", properties);
            materialEditor.ShaderProperty(occlusionMapChannelMask, "_OcclusionMapChannelMask");

            MaterialProperty occlusionRemapStart = FindProperty("_OcclusionRemapStart", properties);
            materialEditor.ShaderProperty(occlusionRemapStart, "遮罩起点");

            MaterialProperty occlusionRemapEnd = FindProperty("_OcclusionRemapEnd", properties);
            materialEditor.ShaderProperty(occlusionRemapEnd, "遮罩终点");
            EditorGUI.indentLevel--;
            
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawLighting()
    {
        showLighting = EditorGUILayout.BeginFoldoutHeaderGroup(showLighting, "光照");
        if (showLighting)
        {
            EditorGUI.indentLevel++;
            MaterialProperty indirectLightMinColor = FindProperty("_IndirectLightMinColor", properties);
            materialEditor.ShaderProperty(indirectLightMinColor, "最低颜色");

            MaterialProperty fogMaxValue = FindProperty("_FogMaxValue", properties);
            materialEditor.ShaderProperty(fogMaxValue, "雾气系数");

            MaterialProperty indirectLightMultiplier = FindProperty("_IndirectLightMultiplier", properties);
            materialEditor.ShaderProperty(indirectLightMultiplier, "间接光照系数");

            MaterialProperty directLightMultiplier = FindProperty("_DirectLightMultiplier", properties);
            materialEditor.ShaderProperty(directLightMultiplier, "主光照系数");
            EditorGUI.indentLevel--;

        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);

    }

    private void DrawShadowMapping()
    {
        showShadow = EditorGUILayout.BeginFoldoutHeaderGroup(showShadow, "阴影");
        if (showShadow)
        {
            EditorGUI.indentLevel++;
            //MaterialProperty mainLightShadows = FindProperty("_MAIN_LIGHT_SHADOWS", properties);
            //materialEditor.ShaderProperty(mainLightShadows, "浣跨敤闃村奖");

            MaterialProperty celShadeMidPoint = FindProperty("_CelShadeMidPoint", properties);
            materialEditor.ShaderProperty(celShadeMidPoint, "阴影切面的系数");

            MaterialProperty celShadeSoftness = FindProperty("_CelShadeSoftness", properties);
            materialEditor.ShaderProperty(celShadeSoftness, "阴影切面的平滑程度");

            MaterialProperty mainLightIgnoreCelShade = FindProperty("_MainLightIgnoreCelShade", properties);
            materialEditor.ShaderProperty(mainLightIgnoreCelShade, "主光忽略切面");

            MaterialProperty additionalLightIgnoreCelShade = FindProperty("_AdditionalLightIgnoreCelShade", properties);
            materialEditor.ShaderProperty(additionalLightIgnoreCelShade, "额外光忽略切面");

            MaterialProperty receiveShadowMappingAmount = FindProperty("_ReceiveShadowMappingAmount", properties);
            materialEditor.ShaderProperty(receiveShadowMappingAmount, "_灯光阴影图系数");

            MaterialProperty receiveShadowMappingPosOffset = FindProperty("_ReceiveShadowMappingPosOffset", properties);
            materialEditor.ShaderProperty(receiveShadowMappingPosOffset, "_灯光阴影图偏移");

            MaterialProperty shadowMapColor = FindProperty("_ShadowMapColor", properties);
            materialEditor.ShaderProperty(shadowMapColor, "阴影颜色");
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawOutline()
    {
        showOutline = EditorGUILayout.BeginFoldoutHeaderGroup(showOutline, "描边");
        if (showOutline)
        {
            EditorGUI.indentLevel++;


            MaterialProperty fixOutlineColor = FindProperty("_FixOutlineColor", properties);
            materialEditor.ShaderProperty(fixOutlineColor, "使用固定颜色而非乘数");

            MaterialProperty useAverNormal = FindProperty("_UseAverNormal", properties);
            materialEditor.ShaderProperty(useAverNormal, "使用平均化法线");

            MaterialProperty outlineWidth = FindProperty("_OutlineWidth", properties);
            materialEditor.ShaderProperty(outlineWidth, "描边宽度 (World Space)");

            MaterialProperty outlineColor = FindProperty("_OutlineColor", properties);
            materialEditor.ShaderProperty(outlineColor, "描边颜色");

            MaterialProperty outlineZOffset = FindProperty("_OutlineZOffset", properties);
            materialEditor.ShaderProperty(outlineZOffset, "描边偏移 (View Space)");

            //MaterialProperty outlineZOffsetMaskTex = FindProperty("_OutlineZOffsetMaskTex", properties);
            //materialEditor.TexturePropertySingleLine(new GUIContent("_OutlineZOffsetMask", "_OutlineZOffsetMask (black is apply ZOffset)"), outlineZOffsetMaskTex);

            //MaterialProperty outlineZOffsetMaskRemapStart = FindProperty("_OutlineZOffsetMaskRemapStart", properties);
            //materialEditor.ShaderProperty(outlineZOffsetMaskRemapStart, "_OutlineZOffsetMaskRemapStart");

            //MaterialProperty outlineZOffsetMaskRemapEnd = FindProperty("_OutlineZOffsetMaskRemapEnd", properties);
            //materialEditor.ShaderProperty(outlineZOffsetMaskRemapEnd, "_OutlineZOffsetMaskRemapEnd");
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private new MaterialProperty FindProperty(string name, MaterialProperty[] properties)
    {
        return ShaderGUI.FindProperty(name, properties);
    }

    private void SyncKeywords(Material material)
    {
        foreach (var kvp in toggleKeywordMap)
        {
            string propertyName = kvp.Key;
            string keyword = kvp.Value;
            MaterialProperty prop = FindProperty(propertyName, properties);
            if (prop != null)
            {
                bool enable = prop.floatValue > 0.5f;
                if (enable)
                    material.EnableKeyword(keyword);
                else
                    material.DisableKeyword(keyword);
            }
        }
    }
}