
Shader "ToonLit/ToonLit_MouthEye"
{
    Properties
    {
        //[Header(High Level Setting)]
        [Toggle]_IsFace("Is Face? (请确定这是面部材质)", Float) = 1

        [Toggle(_MAIN_LIGHT_SHADOWS)]_MAIN_LIGHT_SHADOWS("_MAIN_LIGHT_SHADOWS", Float) = 1
        _RenderRef("_RenderRef",Int) = 0

        [Header(Base Color)]
        [MainTexture]_BaseMap("_BaseMap (Albedo)", 2D) = "white" {}
        [HDR][MainColor]_BaseColor("_BaseColor", Color) = (1,1,1,1)

        [Space(20)]
        [Header(Mouth)]
        [Toggle(_UseMouthMap)]_UseMouthMap("使用嘴部", Float) = 1
        [MainTexture]_MouthMap("嘴部贴图", 2D) = "white" {}
        [IntRange]_Expression("表情序号", Range(0,64)) = 24
        _Column("每行数量", Int) = 8

         _CelShadeMidPoint("阴影切面的系数", Range(-1,1)) = -0.5
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

    }
        CustomEditor "ToonLitMouthEyeShaderGUI"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
