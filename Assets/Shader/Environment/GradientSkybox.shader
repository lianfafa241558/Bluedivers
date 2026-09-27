Shader "Environment/GradientSkybox"
{
    // 三段渐变天空盒：天顶 / 赤道(地平线) / 地面 三个颜色独立可控。
    // 设计约定（与 FPSGame.DayNightSystem.EnvironmentLightingModule 配套）：
    //   1. 天顶/赤道/地面三色由模块用 skyColor / equatorColor / groundColor 三条渐变每帧写入，
    //      与 RenderSettings.ambient*Color 同源，天空与环境光同步。
    //   2. 昼夜暗度靠 _Exposure（模块走"无 _Lerp 分支"），因此本 shader 故意不提供 _Lerp 属性。
    //   3. _SkyTint 只是给体积云采样用的兼容属性（DrawVolumetricCloud 读它做云的染色），
    //      不参与本 shader 着色，避免天空被二次相乘压暗。
    Properties
    {
        [Header(Sky Colors)]
        _SkyColor("天顶色", Color) = (0.44, 0.62, 0.85, 1)
        _EquatorColor("赤道/地平线色", Color) = (0.78, 0.78, 0.75, 1)
        _GroundColor("地面色", Color) = (0.36, 0.33, 0.28, 1)
        _SkyPower("天顶色过渡 (越大赤道带越宽)", Range(0.05, 8)) = 1
        _GroundPower("地面色过渡 (越大赤道带越宽)", Range(0.05, 8)) = 1

        [Header(Sun)]
        _SunDirection("太阳方向 (运行时由模块写入)", Vector) = (0, 0.3, -1, 0)
        _SunSize("太阳盘大小", Range(0, 0.2)) = 0.02
        _SunSoftness("太阳盘边缘柔化", Range(0.01, 1)) = 0.25
        [HDR] _SunColor("太阳盘颜色", Color) = (1, 0.95, 0.85, 1)
        _SunIntensity("太阳盘强度", Range(0, 10)) = 1
        _HaloStrength("太阳光晕强度", Range(0, 4)) = 0.35
        _HaloPower("太阳光晕收缩", Range(1, 128)) = 12
        _SunHorizonFade("地平线以下淡出速度", Range(0.1, 64)) = 12

        [Header(Misc)]
        _Exposure("曝光 (昼夜/天气由模块写入)", Float) = 1
        _Rotation("旋转 (度)", Range(0, 360)) = 0
        // ⚠ 仅供 DrawVolumetricCloud 采样，不参与着色
        _SkyTint("整体色调 (供体积云采样，不参与着色)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
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

            CBUFFER_START(UnityPerMaterial)
                float4 _SkyColor;
                float4 _EquatorColor;
                float4 _GroundColor;
                float _SkyPower;
                float _GroundPower;
                float4 _SunDirection;
                float _SunSize;
                float _SunSoftness;
                float4 _SunColor;
                float _SunIntensity;
                float _HaloStrength;
                float _HaloPower;
                float _SunHorizonFade;
                float _Exposure;
                float _Rotation;
                float4 _SkyTint;
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
                // 天空盒网格的物体空间方向即世界方向（相机位于原点、不随平移变化）
                output.viewDir = rotated;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDir);

                // 上下两半球都从赤道色出发：天顶方向插到 _SkyColor，地面方向插到 _GroundColor。
                // y = 0（地平线）两侧都恰好等于 _EquatorColor，故用 step 拼接不会出现色缝。
                float skyT = pow(saturate(dir.y), max(_SkyPower, 0.01));
                float groundT = pow(saturate(-dir.y), max(_GroundPower, 0.01));
                float3 upper = lerp(_EquatorColor.rgb, _SkyColor.rgb, skyT);
                float3 lower = lerp(_EquatorColor.rgb, _GroundColor.rgb, groundT);
                float3 col = lerp(lower, upper, step(0.0, dir.y));

                // 太阳盘 + 光晕：方向由模块写入（不依赖 URP 的主光 uniform，避免相机相对空间差异）
                float3 sunDir = normalize(_SunDirection.xyz + float3(0.0, 1e-5, 0.0));
                float sunCos = dot(dir, sunDir);
                float diskRadius = max(_SunSize, 1e-4);
                float disk = saturate((sunCos - (1.0 - diskRadius)) / max(diskRadius * _SunSoftness, 1e-5));
                disk *= disk; // 让盘面更实、边缘更柔
                float halo = pow(saturate(sunCos), max(_HaloPower, 1.0));
                // 太阳盘/光晕只出现在地平线以上；越贴地平线越被雾霾吞掉
                float aboveHorizon = saturate(dir.y * _SunHorizonFade);
                col += _SunColor.rgb * (disk * _SunIntensity + halo * _HaloStrength) * aboveHorizon;

                col *= _Exposure;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
