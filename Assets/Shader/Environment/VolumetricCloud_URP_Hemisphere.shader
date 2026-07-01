Shader "WalkingFat/VolumetricCloud_Hemisphere_Fixed"
{
    Properties
    {
        [MainTexture] _NoiseTex ("Noise Texture", 2D) = "white" {}
        [MainColor] _MainColor ("Main Color", Color) = (1,1,1,1)
        
        _CloudDensity ("Cloud Density", Range(0, 2)) = 0.8
        _Cutoff ("Cutoff", Range(0, 0.5)) = 0.05
        _NoiseScale ("Noise Scale", Range(0.1, 5)) = 1.5
        _CloudSpeed ("Cloud Speed", Range(0, 1)) = 0.1
        
        _TopColor ("Top Color", Color) = (1,1,1,1)
        _BottomColor ("Bottom Color", Color) = (0.8,0.8,0.9,1)
        
        _LightIntensity ("Light Intensity", Range(0, 2)) = 1
        _AmbientIntensity ("Ambient Intensity", Range(0, 2)) = 0.5
    }
    SubShader
    {
        Tags { 
            "Queue" = "Transparent" 
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZClip Off 
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };
            
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            
            // 声明天空盒纹理
            TEXTURE2D(_GlossyEnvironmentTexture);
            SAMPLER(sampler_GlossyEnvironmentTexture);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _TopColor;
                float4 _BottomColor;
                float _CloudDensity;
                float _Cutoff;
                float _NoiseScale;
                float _CloudSpeed;
                float _LightIntensity;
                float _AmbientIntensity;
            CBUFFER_END
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.normal = normalize(TransformObjectToWorldNormal(v.normal));
                o.viewDir = GetWorldSpaceNormalizeViewDir(o.worldPos);
                return o;
            }
            
            half4 frag(v2f i) : SV_Target
            {
                // 使用世界坐标XZ平面采样
                float2 uv = i.worldPos.xz * 0.005 * _NoiseScale;
                float time = _Time.y * _CloudSpeed;
                uv += time;
                
                half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv);
                float density = noise.r;
                
                // 垂直渐变
                float verticalFactor = saturate(i.normal.y);
                verticalFactor = pow(verticalFactor, 1.2);
                
                // 最终密度
                float finalDensity = density * verticalFactor * _CloudDensity - _Cutoff;
                
                if (finalDensity < 0.01)
                    discard;
                
                // 获取主光源方向
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 lightColor = mainLight.color;
                
                // 简单的漫反射光照
                float NdotL = saturate(dot(i.normal, lightDir));
                float diffuse = NdotL * _LightIntensity;
                
                // 采样天空盒颜色作为环境光
                float3 reflectDir = reflect(-i.viewDir, i.normal);
                float4 skyColor = SAMPLE_TEXTURE2D(_GlossyEnvironmentTexture, sampler_GlossyEnvironmentTexture, reflectDir);
                float3 ambient = skyColor.rgb * _AmbientIntensity;
                
                // 计算光照颜色
                half3 lightResult = lightColor * diffuse + ambient;
                
                // 颜色渐变
                half3 color = lerp(_BottomColor.rgb, _TopColor.rgb, verticalFactor);
                color *= _MainColor.rgb;
                color *= (0.5 + lightResult * 0.5);  // 混合光照
                
                float alpha = saturate(finalDensity * 1.5);
                
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}