
Shader "ToonLit/ToonLit_Scene"
{
    Properties
    {
        //[Header(High Level Setting)]
        [Toggle]_IsFace("Is Face? (请确定这是面部材质)", Float) = 0

        [Toggle(_MAIN_LIGHT_SHADOWS)]_MAIN_LIGHT_SHADOWS("_MAIN_LIGHT_SHADOWS", Float) = 1
        _RenderRef("_RenderRef",Int) = 0
       

        //所有属性都将尝试遵循URP光照着色器的命名约定
        //因此，将URP光照材质的着色器切换到此卡通光照着色器将保留大多数原始属性(如果在此着色器中定义)

        //有关URP光照着色器的命名约定，请参见URP的光照着色器
        [Header(Base Color)]
        [MainTexture]_BaseMap("_BaseMap (Albedo)", 2D) = "white" {}
        [HDR][MainColor]_BaseColor("_BaseColor", Color) = (1,1,1,1)
        _BaseScale("颜色系数", Range(0,2)) = 1

        //混合纹理
        _BlendingScale("混合程度", Range(0,1)) = 0
        _BlendingMap("混合纹理", 2D) = "white" {}
        [Toggle(_UseUV1)]_UseUV1("使用UV1作为混合", Float) = 0

   
        //[Header(Dissolve)]
        [Toggle(_UseAlphaClipping)]_UseAlphaClipping("使用溶解", Float) = 0
        [Toggle(_UseAlphaUV)]_UseAlphaUV("使用UV进行溶解", Float) = 0
        _AlphaMap("溶解贴图", 2D) = "white" {}
        _DissolveValue("溶解系数", Color) = (0,0,0)//实际上float就行，但是为了方便控制
        _EdgeWidth ("边缘宽度", Range(0, 0.1)) = 0.05
        [HDR]_EdgeColor("边缘颜色", Color) = (0.8,0.8,0.8)
        //_Cutoff("_Cutoff (Alpha Cutoff)", Range(0.0, 1.0)) = 0.5

        //[Toggle]_UseColour("使用‘色彩’效果", Float) = 0


        //[Header(Emission)]
        [Toggle]_UseEmission("使用自发光", Float) = 0
        [Toggle]_EmissionMaskAddite("使用每个通道作为蒙版", Float) = 0
        [HDR] _EmissionColor("自发光颜色", Color) = (0,0,0)
        _EmissionMulByBaseColor("根据原颜色发光", Range(0,1.4)) = 0
        _EmissionScale("发光系数", Range(0,2)) = 1
        [NoScaleOffset]_EmissionMap("自发光贴图", 2D) = "white" {}
        _EmissionMapChannelMask("自发光贴图通道", Vector) = (1,1,1,0)

        //[Header(Specular)]
        //[Toggle]_UseSpecular("_UseSpecular", Float) = 0
       
        //[Header(Occlusion)]
        [Toggle]_UseOcclusion("_UseOcclusion (on/off Occlusion completely)", Float) = 0
        //[Toggle]_ReverseOcclusionColor("_ReverseOcclusionColor", Float) = 0
        _OcclusionStrength("_OcclusionStrength", Range(0.0, 1.0)) = 1.0
        [NoScaleOffset]_OcclusionMap("_OcclusionMap", 2D) = "white" {}
        _OcclusionMapChannelMask("_OcclusionMapChannelMask", Vector) = (1,0,0,0)
        //可以直接翻转这个实现
        _OcclusionRemapStart("_OcclusionRemapStart", Range(0,1)) = 0
        _OcclusionRemapEnd("_OcclusionRemapEnd", Range(0,1)) = 1

        //[Header(Lighting)]
        [HideInInspector]_IndirectLightMinColor("最低颜色", Color) = (0.1,0.1,0.1,1) //如果光探头没有烘烤，可以防止完全变黑
        [HideInInspector]_IndirectLightMultiplier("间接光照系数", Range(0,1)) = 1
        [HideInInspector]_DirectLightMultiplier("主光照系数", Range(0,1)) = 1
        [HideInInspector]_CelShadeMidPoint("阴影切面的系数", Range(-1,1)) = -0.5
        [HideInInspector]_CelShadeSoftness("阴影切面的平滑程度", Range(0,1)) = 0.05
        [HideInInspector]_MainLightIgnoreCelShade("主光忽略切面", Range(0,1)) = 0
        [HideInInspector]_AdditionalLightIgnoreCelShade("额外光忽略切面", Range(0,1)) = 0.9

        //[Header(Shadow mapping)]
        _ReceiveShadowMappingAmount("_ReceiveShadowMappingAmount", Range(0,1)) = 0.65
        [HideInInspector]_ReceiveShadowMappingPosOffset("_ReceiveShadowMappingPosOffset", Float) = 0
        _ShadowMapColor("阴影颜色", Color) = (0.8,0.8,0.8)
        _FogMaxValue("雾气系数", Range(0,1)) = 1


        //[Header(Outline)]

        [HideInInspector][ToggleUI]_FixOutlineColor("使用固定颜色而非乘数", Float) = 0
        [ToggleUI]_UseAverNormal("使用平均化法线", Float) = 0
        _OutlineWidth("描边宽度 (World Space)", Range(0,20)) = 4
        _OutlineColor("描边颜色", Color) = (0.5,0.5,0.5,1)
        [HideInInspector]_OutlineZOffset("描边偏移 (View Space)", Range(0,1)) = 0.0001
        //[NoScaleOffset]_OutlineZOffsetMaskTex("_OutlineZOffsetMask (black is apply ZOffset)", 2D) = "black" {}
        //_OutlineZOffsetMaskRemapStart("_OutlineZOffsetMaskRemapStart", Range(0,1)) = 0
        //_OutlineZOffsetMaskRemapEnd("_OutlineZOffsetMaskRemapEnd", Range(0,1)) = 1
    }
    SubShader
    {       
        Tags 
        {
            // SRP在Subshader中引入了新的“RenderPipeline”标签。这允许您创建着色器
            //可以匹配多个渲染管道。如果未设置RenderPipeline标记，它将匹配
            //任何呈现管道。如果您希望您的子shader只在URP运行，请将标记设置为
            //"通用管道"

            //这里需要“UniversalPipeline”标记，因为我们只希望该着色器在URP运行。
            //如果图形设置中未设置通用渲染管道，此子着色器将失败。

            //可以在下面添加一个子着色器，或者回退到标准内置着色器来实现这一点
            //材质使用通用渲染管道和内置Unity管道

            //标签值是“UniversalPipeline”，不是“UniversalRenderPipeline”，小心！
            // https://github.com/Unity-Technologies/Graphics/pull/1431/
            "RenderPipeline" = "UniversalPipeline"

            // explict SubShader tag to avoid confusion
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Lit"
            "Queue"="Geometry"
        }
        
        //我们可以从所有传递到这个HLSLINCLUDE部分中提取重复的hlsl代码。重复代码越少=错误越少
        HLSLINCLUDE

        //所有过程都需要这个关键字
        //关键字会导致变体，直接使用静态了
        //#pragma shader_feature_local_fragment _UseAlphaClipping
        //#pragma shader_feature_local_fragment _UseMouthMap
        //#pragma shader_feature_local_fragment _SpecMap
        //#pragma shader_feature _AdditionalLights
        ENDHLSL

        //注意:
        // subfix OS 表示 object 空间     (例如 positionOS = position object space)
        // subfix WS 表示 world 空间      (例如 positionWS = position world space)
        // subfix VS 表示 view 空间       (例如 positionVS = position view space)
        // subfix CS 表示 clip 空间       (例如 positionCS = position clip space)





        // [#0 Pass - ForwardLit]
        //一次着色GI，所有灯光，发射和雾。
        //与内置管道转发渲染器相比，URP转发渲染器将
        //使用较少的drawcalls和较少的overdraw渲染多个灯光的场景。
        Pass
        {      
            Name "ForwardLit"

            // 标记上 Stencil，给 XRay 效果时防止重复绘制
            Stencil
            {
                Ref [_RenderRef]
                Comp Always
                Pass Replace
            }

            Tags
            {
            //“light mode”与UniversalRenderPipeline.cs中设置的“ShaderPassName”匹配。
            // SRPDefaultUnlit和不带LightMode标记的过程也由通用渲染管道进行渲染

            //“light mode”标签必须是“UniversalForward”才能在URP渲染照亮的对象。
                "LightMode" = "UniversalForward"
            }

            // explict render state to avoid confusion
            // you can expose these render state to material inspector if needed (see URP's Lit.shader)
            Cull Back
            ZTest LEqual
            ZWrite On
            Blend One Zero

            HLSLPROGRAM

            // -
            //通用渲染管道关键字(您可以随时从URP的Lit.shader中复制此部分)
            //在执行自定义着色器时，您最常希望复制并粘贴这些# pragmas
            //这些multi_compile变量根据以下情况从构建中剥离:
            // 1)在构建时在GraphicsSettings中分配的URP资产中的设置
            //例如，如果您禁用了资源中的附加灯光，那么all _ADDITIONA_LIGHTS变量
            //将从生成中剥离
            // 2)剥离无效组合。例如，具有主光线阴影级联的变体
            //但not _MAIN_LIGHT_SHADOWS无效，因此被剥离。


            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            //#pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_instancing
            // ---------------------------------------------------------------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fog
            // ---------------------------------------------------------------------------------------------

            #pragma vertex VertexShaderWork
            #pragma fragment ShadeFinalColor

            //因为这个传递只是一个向前传递，所以不需要任何特殊的#define
            //(没有特殊的#define)

            //在此内编写的所有着色器逻辑。hlsl，记住在编写#include之前编写所有#define
            #include "ToonLit_Shared.hlsl"

            ENDHLSL
        }
        
        // [#1通道-轮廓]
        //与上面的“ForwardLit”过程相同，但是
        //-顶点位置基于法线方向被推出一点
        //-颜色也是有色的
        //-剔除前部而不是剔除后部，因为剔除前部是所有额外路径轮廓方法的必备条件
        Pass 
        {
            Name "Outline"
            Stencil
            {
                Ref[_RenderRef]
                Comp Always
                Pass Replace
            }
            Tags 
            {
                "LightMode" = "Outline"//现在使用urpFeature来手动调用，现在是一个Srp友好的写法
            }

            Cull Front // 剔除前部是额外通过轮廓法的必要条件

            HLSLPROGRAM

            //直接从“ForwardLit”传递中复制所有关键字
            // ---------------------------------------------------------------------------------------------
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            //#pragma multi_compile_fragment _ _SHADOWS_SOFT
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            //#pragma multi_compile_fragment _ _SHADOWS_SOFT
            // ---------------------------------------------------------------------------------------------
            #pragma multi_compile_fog
            // ---------------------------------------------------------------------------------------------

            #pragma vertex VertexShaderWork
            #pragma fragment ShadeFinalColor

            //因为这是一个大纲传递，所以定义“ToonShaderIsOutline”将大纲相关代码注入到VertexShaderWork()和ShadeFinalColor()中
            #define ToonShaderIsOutline

            //在此内编写的所有着色器逻辑。hlsl，记住在编写#include之前编写所有#define
            #include "ToonLit_Shared.hlsl"

            ENDHLSL
        }
 
        // ShadowCaster pass. 用于渲染URP的阴影贴图
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            //更明确的呈现状态以避免混淆
            ZWrite On //这一关的唯一目标就是写深度！
            ZTest LEqual //如果可能，在早期Z阶段提前退出
            ColorMask 0 //我们不关心颜色，我们只想写深度，ColorMask 0会节省一些写带宽
            Cull Back // support Cull[_Cull]需要在片段着色器中使用VFACE来“翻转顶点法线”,这可能超出了简单教程着色器的范围

            HLSLPROGRAM

            //我们在这次传递中需要的唯一关键字= _UseAlphaClipping，它已经在HLSLINCLUDE块中定义了
            //(因此不需要在此过程中编写任何multi_compile或shader_feature)

            #pragma vertex VertexShaderWork
            #pragma fragment BaseColorAlphaClipTest //我们只需要做Clip()，不需要着色

            //因为它是ShadowCaster过程，所以定义“ToonShaderApplyShadowBiasFix”以将“移除阴影贴图工件”代码注入VertexShaderWork()
            #define ToonShaderApplyShadowBiasFix

            //在此内编写的所有着色器逻辑。hlsl，记住在编写#include之前编写所有#define
            #include "ToonLit_Shared.hlsl"

            ENDHLSL
        }

        // DepthNormalsOnly pass。用于渲染URP的屏外"深度+法线"prepass
        //(DepthNormalOnlyPass 按 LightMode="DepthNormals"/"DepthNormalsOnly" 匹配，
        // 相机需要法线缓冲(SSAO/贴花/软粒子等)时才会跑，跑的时候顺带把深度也写了)。
        Pass
        {
            Name "DepthNormalsOnly"
            Tags{"LightMode" = "DepthNormalsOnly"}

            // more explict render state to avoid confusion
            ZWrite On // the only goal of this pass is to write depth!
            ZTest LEqual // early exit at Early-Z stage if possible            
            //ColorMask 0 // we don't care about color, we just want to write depth, ColorMask 0 will save some write bandwidth
            Cull Back // support Cull[_Cull] requires "flip vertex normal" using VFACE in fragment shader, which is maybe beyond the scope of a simple tutorial shader

            HLSLPROGRAM

            //我们在这次传递中需要的唯一关键字= _UseAlphaClipping，它已经在HLSLINCLUDE块中定义了
            //(因此不需要在此过程中编写任何multi_compile或shader_feature)

            #pragma vertex VertexShaderWork
            #pragma fragment BaseColorAlphaClipTest //我们只需要做Clip()，不需要着色

            //因为它是ShadowCaster过程，所以定义“ToonShaderApplyShadowBiasFix”以将“移除阴影贴图工件”代码注入VertexShaderWork()
            #define ToonShaderIsOutline

            //在此内编写的所有着色器逻辑。hlsl，记住在编写#include之前编写所有#define
            #include "ToonLit_Shared.hlsl"

            ENDHLSL
        }

        // DepthOnly pass。URP 在"只需要深度纹理、不需要法线"的那一帧用这个 pass
        // (DepthOnlyPass 按 LightMode="DepthOnly" 匹配，可以在URP包中搜索DepthOnlyPass.cs)。
        // 缺它的后果：相机只勾了 Depth Texture、又没有任何需要法线的功能(SSAO/贴花/软粒子)时，
        // 本材质不会写进 _CameraDepthTexture，依赖深度纹理的效果会漏掉这些物体。
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ZTest LEqual
            ColorMask 0 //只写深度，不写颜色
            Cull Back

            HLSLPROGRAM

            #pragma vertex VertexShaderWork
            #pragma fragment BaseColorAlphaClipTest //我们只需要做Clip()，不需要着色

            // 与 DepthNormalsOnly pass 保持一致：深度同样按描边外扩后的几何写入，
            // 这样 _CameraDepthTexture 的内容不会因为相机是否跑法线 prepass 而不同。
            // 若不希望外扩，把这个宏和 DepthNormalsOnly 里的那个一起删掉即可。
            #define ToonShaderIsOutline

            //在此内编写的所有着色器逻辑。hlsl，记住在编写#include之前编写所有#define
            #include "ToonLit_Shared.hlsl"

            ENDHLSL
        }

    }
    //CustomEditor "ToonLitMainShaderGUI"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
