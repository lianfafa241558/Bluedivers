Shader "Custom/EmittBlend"
{
    Properties
    {
        // 主纹理
        _MainTex ("Main Texture", 2D) = "white" {}
        // 主纹理UV移动速度
        _MainUVSpeed ("Main UV Speed", Vector) = (0, 0, 0, 0)
        // HDR颜色叠加
        [HDR] _Color ("Color", Color) = (1,1,1,1)

        // 自发光贴图（可UV滚动）
        _EmittTex ("Emitt Texture", 2D) = "black" {}
        // 自发光颜色（HDR）
        [HDR] _EmittColor ("Emitt Color", Color) = (1,1,1,1)
        // 自发光强度
        _EmittIntensity ("Emitt Intensity", Range(0,10)) = 1.0
        // 自发光UV移动速度
        _EmittUVSpeed ("Emitt UV Speed", Vector) = (0.1, 0.1, 0, 0)

        [Space(15)]
        [Enum(Off, 0, On, 1)]_ZWriteMode("ZWriteMode", float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10


        [Space(15)]
        [Toggle(_UnityFogEnable)] _UnityFogEnable("_UnityFogEnable", Float) = 1
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

		ZWrite [_ZWriteMode]
		ZTest LEqual
		Offset 0 , 0
		ColorMask RGBA
        Cull Off

        Tags
        {
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            // 引入URP核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma shader_feature_local _UnityFogEnable

            // 顶点输入结构体
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color : COLOR;
            };

            // 顶点输出/片元输入结构体
            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float2 uv2          : TEXCOORD1;
                // 传递时间（用于UV移动）
                float time          : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float4 color : TEXCOORD5;
            };

            // 全局属性声明
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float2 _MainUVSpeed;

            float4 _Color;

            TEXTURE2D(_EmittTex);
            SAMPLER(sampler_EmittTex);
            float4 _EmittTex_ST;
            float4 _EmittColor;
            float _EmittIntensity;
            float2 _EmittUVSpeed;

            // 顶点着色器
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 转换顶点到裁剪空间
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.positionHCS = posInputs.positionCS;
                // 传递UV（支持缩放和平移）
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.uv2 = TRANSFORM_TEX(IN.uv, _EmittTex);
                // 传递时间（使用内置的_Time.y，单位为秒）
                OUT.time = _Time.y;
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            // 片元着色器
            half4 frag(Varyings IN) : SV_Target
            {
                // 1. 采样主纹理（带UV滚动）
                float2 mainUV = IN.uv + _MainUVSpeed * IN.time;
                half4 mainTexColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV) * IN.color;

                // 2. 采样自发光贴图（带UV滚动）
                float2 emittUV = IN.uv2 + _EmittUVSpeed * IN.time;
                half4 emittTex = SAMPLE_TEXTURE2D(_EmittTex, sampler_EmittTex, emittUV);

                // 3. 主色 * 颜色，再叠加自发光（贴图 * HDR颜色 * 强度）
                half3 baseColor = mainTexColor.rgb * _Color.rgb;
                half3 emittColor = emittTex.rgb * _EmittColor.rgb * _EmittIntensity;
                half3 finalColor = baseColor + emittColor;

            #if _UnityFogEnable && (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
                mainTexColor.a *= saturate(1 - IN.fogFactor);
            #endif
                // 4. 返回最终颜色（保留主纹理的Alpha通道）
                return half4(finalColor, mainTexColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
