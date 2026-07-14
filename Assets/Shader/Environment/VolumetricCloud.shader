Shader "WalkingFat/VolumetricCloud_URP_Fixed"
{
    Properties
    {
        [MainTexture] _NoiseTex ("Noise Texture", 2D) = "white" {}

        [HideInInspector]
        _midYValue ("Mid Y Value", float) = 0
        [HideInInspector]
        _cloudHeight ("Cloud Height", float) = 5


        _NoiseScale2 ("Noise Scale 2", range (0.1, 2.0)) = 1.32
        _CloudSize ("Cloud Size", range (0.01, 3.0)) = 0.5

        _MorphSpeed ("Morph Speed", float) = 0.3
        _MorphScale ("Morph Scale", float) = 0.5
        _MorphStrength ("Morph Strength", range(0, 1)) = 0.4

        [Space(15)] 
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10
    }
    SubShader
    {
        Tags { 
            "RenderType"="Transparent" 
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

            TEXTURE2D(_CloudDayTex);
            SAMPLER(sampler_CloudDayTex);
            TEXTURE2D(_CloudNightTex);
            SAMPLER(sampler_CloudNightTex);
            float _CloudLerp;
            float4 _CloudTintColor;
            float _CloudExposure;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 uv3 : TEXCOORD3;
                float3 posWS : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                //float4 _MainColor;
                float _midYValue, _cloudHeight;
                float _NoiseScale2, _CloudSize;
                float _MorphSpeed, _MorphScale, _MorphStrength;
            CBUFFER_END
            
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                o.pos = vertexInput.positionCS;
                o.posWS = vertexInput.positionWS;
                o.uv = TRANSFORM_TEX(v.uv, _NoiseTex);
                
                float2 baseUv = o.uv * _CloudSize;
                float2 morphScroll = _MorphSpeed * float2(0.73, 0.91) * _Time.y;
                float2 morphUv = o.uv * _MorphScale + morphScroll;
                o.uv1 = baseUv;
                o.uv2 = baseUv * _NoiseScale2 + morphUv * 0.5;
                o.uv3 = morphUv;
                
                return o;
            }
            
            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 noise1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv1);
                half4 noise2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv2);
                half4 morphNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv3);
                float morphOffset = (morphNoise.r - 0.5) * _MorphStrength;
                float density = noise1.r * noise2.r + morphOffset;
                
                float yOffset = abs(i.posWS.y - _midYValue);
                float vFalloff = yOffset / max(_cloudHeight * 0.5, 0.01);
                vFalloff = saturate(vFalloff);
                float finalDensity = density * (1 - vFalloff);
                
                if (finalDensity < 0.01)
                    discard;
                
                half4 dayCol = SAMPLE_TEXTURE2D(_CloudDayTex, sampler_CloudDayTex, i.uv);
                half4 nightCol = SAMPLE_TEXTURE2D(_CloudNightTex, sampler_CloudNightTex, i.uv);
                half3 skyColor = lerp(nightCol.rgb, dayCol.rgb, _CloudLerp);
                skyColor *= _CloudTintColor.rgb * _CloudExposure;
                
                float alpha = saturate(finalDensity * 2);
                return half4(skyColor, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
