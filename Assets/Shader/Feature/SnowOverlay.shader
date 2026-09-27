// 积雪覆盖 Shader：配合 SnowRendererFeature 使用，把雪able 层物体重画一遍并 Alpha 混合叠加雪色
// 只在朝上的表面（法线朝上，头顶/肩膀/平台面等）根据阈值+柔和度+噪声叠出雪色，实现"顶上一层雪皮"
Shader "Custom/SnowOverlay"
{
    Properties
    {
        _SnowColor ("雪的颜色", Color) = (0.92, 0.95, 1.0, 1.0)
        _SnowThreshold ("积雪阈值", Range(0.0, 1.0)) = 0.5
        _SnowSoftness ("边缘柔和度", Range(0.001, 1.0)) = 0.25
        _SnowAmount ("全局积雪量", Range(0.0, 1.0)) = 1.0
        [NoScaleOffset] _NoiseMap ("噪声纹理", 2D) = "white" {}
        _NoiseScale ("噪声缩放", Float) = 0.1
        _NoiseStrength ("噪声强度", Range(0.0, 1.0)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SnowOverlay"

            // 与已画好的物体表面做 Alpha 混合，把雪色"叠"上去
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Unity 全局雾（Lighting 面板 Fog）：关闭时 FOG_* 关键字不启用，MixFog 为空操作
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // 积雪的全局量（_SnowEnabled/_GlobalSnowAmount/_SnowMask/_SnowMaskRect/_SnowMaskTiles）
            // 与覆盖度公式、草地积雪函数都在这份共享文件里（草材质也 include 它，保证两边一致）
            #include "Assets/Shader/Feature/SnowOverlayCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _SnowColor;
                half  _SnowThreshold;
                half  _SnowSoftness;
                half  _SnowAmount;
                float _NoiseScale;
                half  _NoiseStrength;
            CBUFFER_END

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            // 全局控制变量（_SnowEnabled/_GlobalSnowAmount）、积雪遮罩（_SnowMask/_SnowMaskRect/_SnowMaskTiles）
            // 与 SnowSurfaceCoverage() 都在 SnowOverlayCommon.hlsl 里声明/实现

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                // 顶点阶段算好雾因子（与项目其它自定义 Shader 一致），片元里 MixFog 使用
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);

                // 噪声干扰：按世界坐标采样噪声，扰动阈值使积雪边缘破碎、疏密不均
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, IN.positionWS.xz * _NoiseScale).r;

                // 覆盖度（朝上程度 × 噪声扰动阈值 × 积雪遮罩，遮罩内 0 的点即弹坑等处不出雪）
                // —— 与草地积雪共用 SnowOverlayCommon.hlsl 里的同一套公式
                half mask = SnowSurfaceCoverage(IN.positionWS, normalWS, _SnowThreshold, _SnowSoftness,
                                                _NoiseStrength, noise, 0.0h);
                mask *= _SnowAmount * saturate(_GlobalSnowAmount);

                // 简单光照：主光半兰伯特 + 固定环境补偿，保证雪面有明暗且夜晚不至于全黑
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half shade = ndl * 0.5 + 0.5;
                half3 snowCol = _SnowColor.rgb * mainLight.color * shade + _SnowColor.rgb * 0.25;

                // 与 Unity 全局雾混合：远处雪色向雾色过渡，跟下层已雾化的不透明表面保持同一衰减，
                // 不会在远景糊上一层突兀的雪色。雾关闭时 MixFog 原样返回
                snowCol = MixFog(snowCol, IN.fogFactor);

                return half4(snowCol, mask);
            }
            ENDHLSL
        }
    }
}
