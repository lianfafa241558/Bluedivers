Shader "UI/SnowflakeScreen_URP"
{
    Properties
    {
        [HideInInspector][PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.85
        _Speed ("Speed", Float) = 10.0
        _Density ("Density", Float) = 200.0

        // UI 默认 Stencil 属性
        [HideInInspector]_StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector]_Stencil ("Stencil ID", Float) = 0
        [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector]_ColorMask ("Color Mask", Float) = 15

        // RectMask2D 的 _ClipRect 不写进 Properties：它由 CanvasRenderer 每帧注入，
        // 一旦声明成材质属性就会被 UnityPerMaterial 的固定值覆盖，裁剪失效
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UISnowflake"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            // 没有这个变体，父级 RectMask2D 对本 shader 完全无效
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 必须用 CBUFFER 包裹，兼容 SRP Batcher
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float  _NoiseIntensity;
                float  _Speed;
                float  _Density;
            CBUFFER_END

            // 必须留在 CBUFFER 外：值是 CanvasRenderer 逐帧注入的，进 cbuffer 就一直是默认值。
            // 空间 = Graphic 局部空间（与 POSITION.xy 同空间）
            #ifdef UNITY_UI_CLIP_RECT
            float4 _ClipRect;
            #endif

            // 等价于内置 UnityUI.cginc 的 UnityGet2DClipping；内联，避免 URP 与 CG 头文件混用
            half Unity2DClipping(float2 position, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, position) * step(position, clipRect.zw);
                return inside.x * inside.y;
            }

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 clipPos    : TEXCOORD1;   // 局部空间坐标，供 _ClipRect 比对
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 用局部空间坐标而非世界空间：_ClipRect 是 RectMask2D 按 Graphic 局部矩形给的
                OUT.clipPos = IN.positionOS.xy;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

          // 二维哈希，输入 float2，输出 [0,1) 的伪随机
           float hash21(float2 p)
          {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
          }

           half4 frag(Varyings IN) : SV_Target
          {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 col = texColor * IN.color;

                // RectMask2D 裁剪（URP 无 UnityGet2DClipping，用上面内联的）
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= Unity2DClipping(IN.clipPos, _ClipRect);
                #endif

                // === 核心改动 ===
                // 1. 用屏幕像素坐标做空间种子，保证每个像素独立
                float2 pixelPos = IN.positionCS.xy;

                // 2. 颗粒分块：Density 越大，块越小颗粒越细
                float2 cell = floor(pixelPos / max(_Density * 0.5, 1.0));

                // 3. 时间种子：每帧乘以很大系数，frac 后完全不连续 -> 闪烁而非平移
                float timeSeed = frac(_Time.x * _Speed);

                // 4. 合并成随机种子
                float noiseValue = hash21(cell + timeSeed * 137.0);

                // 5. 可选：让噪点有一点明暗分布，更接近老电视
                noiseValue = pow(noiseValue, 0.9);

                // 6. 混合
                half3 snowColor = half3(noiseValue, noiseValue, noiseValue);
                half3 finalColor = lerp(col.rgb, snowColor, _NoiseIntensity);

                return half4(finalColor, col.a);
          }
            ENDHLSL
        }
    }

    Fallback "UI/Default"
}