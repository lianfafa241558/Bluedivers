
Shader "ToonLit/ToonLit_Colour"
{
    Properties
    {
        //[Header(High Level Setting)]
        //[Toggle]_IsFace("Is Face? (请确定这是面部材质)", Float) = 1

        [Toggle(_MAIN_LIGHT_SHADOWS)]_MAIN_LIGHT_SHADOWS("_MAIN_LIGHT_SHADOWS", Float) = 1
        _RenderRef("_RenderRef",Int) = 0

        [Header(Base Color)]
        [MainTexture]_BaseMap("_BaseMap (Albedo)", 2D) = "white" {}
        [HDR][MainColor]_BaseColor("_BaseColor", Color) = (1,1,1,1)
        _BaseScale("颜色系数", Range(0,2)) = 1

        //混合纹理
        _BlendingScale("混合程度", Range(0,1)) = 0
        _BlendingMap("混合纹理", 2D) = "white" {}
        [Toggle(_UseUV1)]_UseUV1("使用UV1作为混合", Float) = 0

        [Header(Emission)]
        [Toggle]_UseEmission("使用自发光", Float) = 0
        [Toggle]_EmissionMaskAddite("使用每个通道作为蒙版", Float) = 0
        [HDR] _EmissionColor("自发光颜色", Color) = (0,0,0)
        _EmissionMulByBaseColor("根据原颜色发光", Range(0,1)) = 0
        _EmissionScale("发光系数", Range(0,2)) = 1
        _EmissionMap("自发光贴图", 2D) = "white" {}
        _EmissionMapChannelMask("自发光贴图通道", Vector) = (1,1,1,0)

         [Header(Shade)]
        _CelShadeSoftness("阴影切面的平滑程度", Range(0,1)) = 0.05

        [Header(_Colour)]
         [Toggle]_UseColour("使用色彩", Float) = 0
        
        _ColourScale("色彩系数", Range(0,1)) = 0
        _ColourTex("_ColourTex", 2D) = "white" {}
        [HDR] _ColourColor("色彩颜色", Color) = (0,0,0)
        _ColourMaskTex("_ColourMaskTex", 2D) = "white" {}

        [ToggleUI]_FixOutlineColor("使用固定颜色而非乘数", Float) = 0
        [ToggleUI]_UseAverNormal("使用平均化法线", Float) = 0
        _OutlineWidth("描边宽度 (World Space)", Range(0,20)) = 4
        _OutlineColor("描边颜色", Color) = (0.5,0.5,0.5,1)

    }
    SubShader
    {       
        Tags 
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Lit"
            "Queue"="Geometry"
        }

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
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZTest LEqual
            ZWrite On
            Blend One Zero

            HLSLPROGRAM



            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS
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
            #pragma multi_compile _MAIN_LIGHT_SHADOWS
            //#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
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

        // DepthOnly pass。用于渲染URP的屏外深度prepass(可以在URP包中搜索DepthOnlyPass.cs)
        //例如，当深度纹理打开时，我们需要为此卡通着色器执行此屏幕外深度预处理。
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
    }
        //CustomEditor "ToonLitFaceShaderGUI"
        FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
