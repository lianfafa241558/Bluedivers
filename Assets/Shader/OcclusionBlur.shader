Shader "Custom/OcclusionBlur"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1, 1, 1, 0.3)
        _BlurRadius ("Blur Radius", Range(0, 0.05)) = 0.01
        [IntRange]_BlurCount ("_BlurPower", Range(1, 4)) = 1
    }

    SubShader
    {
        // UI 渲染必须设置这些标签
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float4 screenPos : TEXCOORD0;
            float2 uv : TEXCOORD1;
            float4 color : TEXCOORD2;
        };

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _BlurRadius;
            int _BlurCount;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "UIBlur"
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // 获取屏幕坐标用于采样 OpaqueTexture
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 归一化屏幕坐标
                float2 uv = input.screenPos.xy / input.screenPos.w;

                half3 blurredColor = 0;

                
                int sampleCount = 0;
                for (int u = -_BlurCount; u <= _BlurCount; u++) {
                    for (int v = -_BlurCount; v <= _BlurCount; v++) {
                        blurredColor += SampleSceneColor(uv + _BlurRadius * float2(u,v));
                        sampleCount++;
                    }
                }

                blurredColor /= sampleCount;

                // 混合 UI 自身的颜色和模糊后的背景色
                half4 finalColor = half4(blurredColor * _Color.rgb*input.color.rgb, _Color.a*input.color.a);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}