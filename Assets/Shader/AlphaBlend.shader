Shader "Custom/AlphaBlend"
{
    Properties
    {
        // 主纹理
        _MainTex ("Main Texture", 2D) = "white" {}
        // HDR颜色叠加
        [HDR] _Color ("Color", Color) = (1,1,1,1)

        // Alpha混合贴图1
        _AlphaTex1 ("Alpha Texture 1", 2D) = "white" {}
        // Alpha1的亮度叠加值
        _Alpha1Add ("Alpha1 Add", Range(0,0.1) ) = 0.0
        // Alpha1的乘法系数
        _Alpha1Mult ("Alpha1 Mult", Range(0,3) ) = 1.0
        // Alpha1的UV移动速度
        _Alpha1UVSpeed ("Alpha1 Speed", Vector) = (0.1, 0.1, 0, 0)

        // Alpha混合贴图2
        _AlphaTex2 ("Alpha Texture 2", 2D) = "white" {}
        // Alpha2的亮度叠加值
        _Alpha2Add ("Alpha2  Add", Range(0,0.1) ) = 0.0
        // Alpha2的乘法系数
        _Alpha2Mult ("Alpha2 Mult", Range(0,3) ) = 1.0
        // Alpha2的UV移动速度
        _Alpha2UVSpeed ("Alpha2 Speed", Vector) = (0.1, 0.1, 0, 0)

        [Space(15)] 
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10

        [Space(15)] 
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Blend[_SrcBlend][_DstBlend]
        //Blend SrcAlpha One

		ZWrite Off
		ZTest LEqual
		Offset 0 , 0
		ColorMask RGBA
        Cull Off

        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // 引入URP核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 顶点输入结构体
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            // 顶点输出/片元输入结构体
            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float2 uv2           : TEXCOORD1;
                float2 uv3           : TEXCOORD2;
                // 传递时间（用于UV移动）
                float time          : TEXCOORD4;
            };

            // 全局属性声明
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float4 _Color;
     

            TEXTURE2D(_AlphaTex1);
            SAMPLER(sampler_AlphaTex1);
            float4 _AlphaTex1_ST;
            float _Alpha1Add;
            float _Alpha1Mult;
            float2 _Alpha1UVSpeed;

            TEXTURE2D(_AlphaTex2);
            SAMPLER(sampler_AlphaTex2);
            float4 _AlphaTex2_ST;
            float _Alpha2Add;
            float _Alpha2Mult;
            float2 _Alpha2UVSpeed;


            // 顶点着色器
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 转换顶点到裁剪空间
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // 传递UV（支持缩放和平移）
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.uv2 = TRANSFORM_TEX(IN.uv, _AlphaTex1);
                OUT.uv3 = TRANSFORM_TEX(IN.uv, _AlphaTex2);
                // 传递时间（使用内置的_Time.y，单位为秒）
                OUT.time = _Time.y;
                return OUT;
            }

            // 片元着色器
            half4 frag(Varyings IN) : SV_Target
            {
                // 1. 采样主纹理
                half4 mainTexColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 2. 计算Alpha1的滚动UV并采样
                float2 alpha1UV = IN.uv2 + _Alpha1UVSpeed * IN.time;
                half4 alpha1Tex = SAMPLE_TEXTURE2D(_AlphaTex1, sampler_AlphaTex1, alpha1UV);
                // 处理Alpha1：alpha值 * 系数 + 亮度叠加
                half alpha1Value = alpha1Tex.r * _Alpha1Mult + _Alpha1Add;

                // 限制值在0-1范围内（防止溢出）
                //alpha1Value = saturate(alpha1Value);

                // 3. 计算Alpha2的滚动UV并采样
                float2 alpha2UV = IN.uv3 + _Alpha2UVSpeed * IN.time;
                half4 alpha2Tex = SAMPLE_TEXTURE2D(_AlphaTex2, sampler_AlphaTex2, alpha2UV);
                // 处理Alpha2：alpha值 * 系数 + 亮度叠加
                half alpha2Value = alpha2Tex.r * _Alpha2Mult + _Alpha2Add;
                //alpha2Value = saturate(alpha2Value);

                // 4. 混合两张Alpha贴图的结果
                half finalAlpha = alpha1Value*alpha2Value;

                // 5. 叠加HDR颜色（主颜色 * HDR颜色 * Alpha混合结果）
                half3 finalColor = mainTexColor.rgb * _Color.rgb * finalAlpha;

                // 6. 返回最终颜色（保留主纹理的Alpha通道）
                return half4(finalColor, mainTexColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}