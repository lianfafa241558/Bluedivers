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

            // 全局控制变量（由 SnowController 通过 Shader.SetGlobalFloat 设置，非材质属性）
            float _SnowEnabled;
            float _GlobalSnowAmount;

            // 积雪遮罩（由 SnowController 通过 Shader.SetGlobalTexture 设为纹理阵列）：
            // 1=正常积雪，0=该点无雪（弹坑、地形被破坏处）。
            // 切成 _SnowMaskTiles×_SnowMaskTiles 片存放，弹坑只重传被碰到的那一片；0 表示遮罩未创建
            TEXTURE2D_ARRAY(_SnowMask);
            SAMPLER(sampler_SnowMask);
            // 遮罩映射：xy=地形原点(x,z)，zw=1/地形尺寸，世界坐标 XZ 乘它得到遮罩 UV
            float4 _SnowMaskRect;
            // 遮罩每边切片数
            float _SnowMaskTiles;

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

                // 检测朝上面：法线与世界上方向的点积 = 表面朝上程度
                half upDot = saturate(dot(normalWS, half3(0.0, 1.0, 0.0)));

                // 噪声干扰：按世界坐标采样噪声，扰动阈值使积雪边缘破碎、疏密不均
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, IN.positionWS.xz * _NoiseScale).r;
                half threshold = _SnowThreshold + (noise - 0.5) * _NoiseStrength;

                // 平滑过渡：smoothstep 在阈值上下 _SnowSoftness 范围内做柔和过渡
                half mask = smoothstep(threshold - _SnowSoftness, threshold + _SnowSoftness, upDot);
                mask *= _SnowAmount * saturate(_GlobalSnowAmount);

                // 积雪遮罩：世界坐标 XZ → 遮罩 UV → 定位切片与片内 UV，采样值 0 的点（弹坑等）不出雪。
                // saturate 兜底，UV 越界时不依赖纹理 wrap 模式；
                // _SnowMaskTiles 为 0 表示遮罩尚未初始化（域重载后全局变量会重置），此时按满雪处理
                half snowMaskValue = 1.0;
                if (_SnowMaskTiles > 0.0)
                {
                    float2 snowMaskUV = saturate((IN.positionWS.xz - _SnowMaskRect.xy) * _SnowMaskRect.zw);
                    float2 tileUV = snowMaskUV * _SnowMaskTiles;
                    float2 tileCoord = min(floor(tileUV), _SnowMaskTiles - 1.0);
                    float slice = tileCoord.y * _SnowMaskTiles + tileCoord.x;
                    snowMaskValue = SAMPLE_TEXTURE2D_ARRAY(_SnowMask, sampler_SnowMask, tileUV - tileCoord, slice).r;
                }
                mask *= snowMaskValue;

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
