Shader "WalkingFat/VolumetricCloud_URP_Fixed"
{
    Properties
    {
        [MainTexture][HideInInspector] _NoiseTex ("Noise Texture (unused)", 2D) = "white" {}
        [HideInInspector] _midYValue ("Mid Y Value", Float) = 0
        [HideInInspector] _cloudHeight ("Cloud Height", Float) = 5

        // 兼容旧脚本的属性（DrawVolumetricCloud 每帧写入，贴图本体不再使用）
        [NoScaleOffset][HideInInspector] _CloudDayTex ("Cloud Day Tex", 2D) = "white" {}
        [NoScaleOffset][HideInInspector] _CloudNightTex ("Cloud Night Tex", 2D) = "white" {}
        [HideInInspector] _CloudLerp ("Cloud Lerp (0夜 1昼)", Float) = 0
        [HideInInspector] _CloudTintColor ("Cloud Tint", Color) = (1,1,1,1)
        [HideInInspector] _CloudExposure ("Cloud Exposure", Float) = 1
        [HideInInspector] _CloudCoverage ("Cloud Coverage (天气倍率)", Float) = 1
        [HideInInspector] _SunDirection ("Sun Direction", Vector) = (0, 1, 0, 0)

        // 可手调的云外观参数
        _CloudScale ("云朵尺度(越小云越大)", Range(0.02, 1)) = 0.12
        _CoverageBase ("基础云量", Range(0, 1)) = 0.5
        _Density ("密度", Range(0.2, 3)) = 1.4
        _EdgeSoftness ("边缘柔和度", Range(0.02, 0.5)) = 0.2
        _WindSpeed ("风速", Float) = 0.02
        _DayCloudColor ("白天云色", Color) = (1, 0.98, 0.95, 1)
        _NightCloudColor ("夜晚云色", Color) = (0.1, 0.12, 0.18, 1)
        _ShadowStrength ("背光面阴影强度", Range(0, 1)) = 0.45

        [Space(15)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("DstBlend", Float) = 10
    }
    SubShader
    {
        Tags {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend[_SrcBlend][_DstBlend]
            Cull Off
            ZWrite Off
            ZClip Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float _midYValue, _cloudHeight;
                float _CloudScale, _CoverageBase, _Density, _EdgeSoftness, _WindSpeed;
                float4 _DayCloudColor, _NightCloudColor;
                float _ShadowStrength;
            CBUFFER_END

            // 与脚本每帧写入的动态属性（不入 CBUFFER，避免破坏合批兼容的属性布局假设）
            float _CloudLerp;
            float4 _CloudTintColor;
            float _CloudExposure;
            float _CloudCoverage;
            float4 _SunDirection;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 posWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                o.pos = vertexInput.positionCS;
                o.posWS = vertexInput.positionWS;
                return o;
            }

            // ---- 程序化噪声 ----
            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x),
                    lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int k = 0; k < 5; k++)
                {
                    v += a * ValueNoise(p);
                    p = p * 2.03 + float2(19.7, 7.3);
                    a *= 0.5;
                }
                return v;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // 云场用世界坐标 XZ 驱动，堆叠的所有层采样同一片云体，体积感连贯
                float2 wind = _WindSpeed * _Time.y * float2(1.0, 0.6);
                float2 p = i.posWS.xz * _CloudScale + wind;

                // 大形状 + 细节的混合
                float shape = Fbm(p);
                float detail = Fbm(p * 3.1 + _Time.y * 0.03);
                float field = shape * 0.72 + detail * 0.28;

                // 覆盖率合成：手动基础云量 × 天气倍率（暴雪/暴雨时 >1，云量顶满）
                float coverage = saturate(_CoverageBase * _CloudCoverage);
                float threshold = lerp(0.95, 0.28, coverage);
                // 大尺度云团聚分布：让云成团成带，而不是均匀撒满天空
                float clusters = Fbm(p * 0.22 + 137.31);
                threshold += (clusters - 0.5) * 0.35;
                float cloud = smoothstep(threshold, threshold + _EdgeSoftness + 0.05, field);

                // 伪光照：沿真实太阳/月亮方向的地面投影偏移再采样一次，密度差形成朝向光源的明暗起伏
                float2 sunOffset = normalize(_SunDirection.xz + 1e-4) * 0.18;
                float lit = Fbm(p + sunOffset);
                float shade = saturate((field - lit) * 5.0 + 0.75);

                // 昼夜云色：由脚本写入的 _CloudLerp（0夜 1昼）与天空盒 Tint/曝光驱动
                half3 baseCol = lerp(_NightCloudColor.rgb, _DayCloudColor.rgb, saturate(_CloudLerp));
                baseCol *= _CloudTintColor.rgb * _CloudExposure;
                half3 col = baseCol * lerp(1.0 - _ShadowStrength, 1.12, shade);

                // 垂直方向淡出：云层顶/底边缘软化，与云层厚度(_cloudHeight)匹配
                float hFade = 1.0 - saturate(abs(i.posWS.y - _midYValue) / max(_cloudHeight * 0.5, 0.01));
                hFade = smoothstep(0.0, 0.35, hFade);

                float alpha = cloud * saturate(_Density) * hFade;
                if (alpha < 0.01)
                    discard;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
