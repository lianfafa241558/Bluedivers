Shader "Skybox/DayNightPanoramic"
{
 Properties
    {
        _DayTex("Day Panoramic (HDRI)", 2D) = "white" {}
        _NightTex("Night Panoramic (HDRI)", 2D) = "black" {}

        [HDR] _TintColor("Tint Color", Color) = (1,1,1,1)
        _Exposure("Exposure", Float) = 1.0
        _Rotation("Rotation", Range(0, 360)) = 0.0

        [KeywordEnum(LatitudeLongitudeLayout, None)] _Mapping("Mapping", Float) = 0
        [KeywordEnum(Default, MirrorOnX, ThreeSixtyDegree)] _ImageType("Image Type", Float) = 2
        [KeywordEnum(None, SideBySide, OverUnder)] _Layout("3D Layout", Float) = 0

        _Lerp("Day-Night Lerp", Range(0,1.0)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
            "RenderPipeline"="UniversalPipeline"
        }
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            TEXTURE2D(_DayTex);
            SAMPLER(sampler_DayTex);
            TEXTURE2D(_NightTex);
            SAMPLER(sampler_NightTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DayTex_ST;
                float4 _NightTex_ST;
                float4 _TintColor;
                float _Exposure;
                float _Rotation;
                float _Mapping;
                float _ImageType;
                float _Layout;
                float _Lerp;
            CBUFFER_END

            float3 RotateAroundY(float3 vertex, float degrees)
            {
                float alpha = degrees * PI / 180.0;
                float s, c;
                sincos(alpha, s, c);
                return float3(vertex.x * c + vertex.z * s, vertex.y, -vertex.x * s + vertex.z * c);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 rotated = RotateAroundY(input.positionOS.xyz, _Rotation);
                output.positionHCS = TransformObjectToHClip(rotated);
                output.viewDir = rotated;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDir);
                float2 uv;

                // 官方 HDRI 正确算法
                float phi = atan2(dir.x, dir.z);
                float theta = acos(dir.y);
                uv.x = 0.5 + phi / (PI * 2.0);
                uv.y = theta / PI;
                uv.y = 1-uv.y;

                if (_ImageType == 1) // Mirror
                    uv.x = 1 - uv.x;

                if (_ImageType == 2) // 360 Degrees
                    uv.x = fmod(uv.x * 2.0, 1.0);

                if (_Layout == 1)
                    uv.x *= 0.5;
                else if (_Layout == 2)
                    uv.y *= 0.5;

                half4 day = SAMPLE_TEXTURE2D(_DayTex, sampler_DayTex, uv);
                half4 night = SAMPLE_TEXTURE2D(_NightTex, sampler_NightTex, uv);
                half4 col = lerp(night, day, _Lerp);

                col.rgb *= _TintColor.rgb * _Exposure;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}