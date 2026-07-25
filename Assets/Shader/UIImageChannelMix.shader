Shader "UI/ImageChannelMix"
{
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}

        // Stencil 参数，用于响应父级 Mask 组件
        [Space(15)]
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100

        // Stencil：响应 Unity UI Mask 组件
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Cull Off
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 贴图
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            // 伽马转线性：pow(col, 2.2)
            half3 GammaToLinear(half3 col)
            {
                return pow(col, 2.2);
            }

            // 线性转伽马：pow(col, 1.0/2.2)
            half3 LinearToGamma(half3 col)
            {
                return pow(col, 1.0 / 2.2);
            }

            // 顶点输入：COLOR 语义接收 UI Image 的颜色
            struct a2v
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
            };

            v2f vert(a2v v)
            {
                v2f o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = posInputs.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // 采样贴图（URP 自动做了 sRGB→线性，先用 LinearToGamma 回到原始伽马值）
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                texColor.rgb = LinearToGamma(texColor.rgb);

                // R 通道 * Image 输入的颜色
                // G 通道 * 白色(1,1,1)
                // B 通道不管（置 0）
                // A 通道直接用贴图的 A 通道
                half3 rChannel = texColor.r * i.color.rgb;
                half3 gChannel = texColor.g * half3(1.0, 1.0, 1.0);

                half3 finalColor = rChannel + gChannel;
                half finalAlpha = texColor.a * i.color.a;

                // 输出前转回线性空间
                finalColor = GammaToLinear(finalColor);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
