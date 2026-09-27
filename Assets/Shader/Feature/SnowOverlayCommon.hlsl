// 积雪覆盖的共享实现：
//  - SnowOverlay.shader（SnowRendererFeature 用雪材质"重画一遍"的那些物体）用它算积雪覆盖度；
//  - 地形细节（草/花/荆棘）不是 Renderer，吃不到那次重画，只能在自己的材质里叠雪 ——
//    也就是 ToonLit_Shared.hlsl 里 _SNOW_GRASS 关键字分支调用的 ApplyGrassSnow()。
// 两边共用同一套全局量，保证"雪量/雪色/遮罩"始终一致。
//
// ⚠ 这里只声明全局 uniform（由 C# 侧 Shader.SetGlobal* 写入，见 SnowController / SnowRendererFeature），
//   有意不写进 Properties / UnityPerMaterial CBUFFER：
//   ① 材质上的同名属性会遮住 Shader.SetGlobal* 的值；
//   ② 本文件被所有 include ToonLit_Shared.hlsl 的 shader 一起编译，往 CBUFFER 里加字段会波及它们。
#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// 全局开关与雪量（SnowController.SetEnabled / SetGlobalAmount；Volume 生效时由 SnowRendererFeature 每帧写入）
float _SnowEnabled;
float _GlobalSnowAmount;

// 积雪遮罩（纹理阵列 + 世界 XZ 映射）：1=正常积雪，0=该点无雪（弹坑、地形被破坏处）
TEXTURE2D_ARRAY(_SnowMask);
SAMPLER(sampler_SnowMask);
float4 _SnowMaskRect;   // xy=地形原点(x,z)，zw=1/地形尺寸
float _SnowMaskTiles;   // 每边切片数；0 = 遮罩未创建（域重载后全局量会重置）→ 按满雪处理

// 雪的外观参数（跟随 SnowVolume；无 Volume 时用 SnowController 里的默认值）
float4 _SnowOverlayColor;         // 雪色
float _SnowOverlayThreshold;      // 朝上阈值
float _SnowOverlaySoftness;       // 边缘柔和度
float _SnowOverlayNoiseStrength;  // 噪声强度

// 草地积雪量倍率（草不走积雪 Pass，这里统一调它的强度；0 = 草不吃雪）
float _SnowGrassAmount;

// 草地专用常量：调草的雪感直接改这里。
// ⚠ 草片又薄又斜（实测 Pandazole 草网格法线平均 |ny| 只有 0.08~0.35），"法线朝上"判定在草上基本不成立，
//   所以给朝上程度一个下限：保证"雪天草一定变白"，同时遮罩/开关仍然能把它清成 0（弹坑里不长雪）。
#define SNOW_GRASS_NORMAL_FLOOR 0.55
#define SNOW_GRASS_NOISE_SCALE 0.12

// 便宜的 2D 值噪声（给草用：草材质上没有噪声贴图，不值得为它多绑一张）
half SnowHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

half SnowValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    half a = SnowHash(i);
    half b = SnowHash(i + float2(1.0, 0.0));
    half c = SnowHash(i + float2(0.0, 1.0));
    half d = SnowHash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

/// <summary>
/// 表面覆盖系数（0~1）：朝上程度（含噪声扰动阈值、可选下限）× 积雪遮罩。
/// 不含全局开关/雪量：由调用方按自己的语义乘上去
/// （SnowOverlay 乘材质 _SnowAmount 与 _GlobalSnowAmount；草乘 _SnowEnabled×_GlobalSnowAmount×_SnowGrassAmount）。
/// </summary>
/// <param name="positionWS">世界坐标（取 XZ 查积雪遮罩）</param>
/// <param name="normalWS">世界法线（判"朝上程度"）</param>
/// <param name="threshold">积雪阈值（朝上程度高于它才出雪）</param>
/// <param name="softness">边缘柔和度（阈值上下做 smoothstep 过渡）</param>
/// <param name="noiseStrength">噪声强度（扰动阈值，让积雪边缘破碎）</param>
/// <param name="noise">噪声采样值 0~1（调用方采样后传入；不想要阈值扰动就传 0 并把 noiseStrength 传 0）</param>
/// <param name="normalFloor">朝上程度下限（0=严格按法线；草传 SNOW_GRASS_NORMAL_FLOOR）</param>
half SnowSurfaceCoverage(float3 positionWS, half3 normalWS, half threshold, half softness,
                         half noiseStrength, half noise, half normalFloor)
{
    half upDot = saturate(dot(normalize(normalWS), half3(0.0, 1.0, 0.0)));

    // 噪声扰动阈值：积雪边缘破碎、疏密不均
    half noisyThreshold = threshold + (noise - 0.5h) * noiseStrength;
    half coverage = smoothstep(noisyThreshold - softness, noisyThreshold + softness, upDot);
    coverage = max(coverage, saturate(normalFloor));

    // 积雪遮罩：世界 XZ → 遮罩 UV → 定位切片与片内 UV，采样值 0 的点（弹坑等）不出雪。
    // saturate 兜底，UV 越界时不依赖纹理 wrap 模式
    if (_SnowMaskTiles > 0.0f)
    {
        float2 maskUV = saturate((positionWS.xz - _SnowMaskRect.xy) * _SnowMaskRect.zw);
        float2 tileUV = maskUV * _SnowMaskTiles;
        float2 tileCoord = min(floor(tileUV), _SnowMaskTiles - 1.0);
        float slice = tileCoord.y * _SnowMaskTiles + tileCoord.x;
        coverage *= SAMPLE_TEXTURE2D_ARRAY(_SnowMask, sampler_SnowMask, tileUV - tileCoord, slice).r;
    }

    return coverage;
}

/// <summary>
/// 草地（Terrain 细节）积雪：把已算好的受光颜色向雪色混合。
/// 为什么草要自己算：Terrain 的细节不走 SRP 的渲染器列表（按 detailPrototypes + 每层密度图内部实例化绘制），
/// SnowRendererFeature 的 DrawRenderers + overrideMaterial 与层遮罩都落不到它身上，
/// 因此只能在草自己的片元里叠雪（材质关键字 _SNOW_GRASS，草材质 PandaMat2 已启用）。
/// </summary>
/// <param name="litColor">已完成卡通光照的颜色（ShadeAllLights 的结果）</param>
/// <param name="normalWS">世界法线</param>
/// <param name="positionWS">世界坐标</param>
half3 ApplyGrassSnow(half3 litColor, half3 normalWS, float3 positionWS)
{
    // 与积雪 Pass 同源的三个开关：雪天 × 全局雪量（Volume） × 草自身倍率
    half gate = saturate(_SnowEnabled) * saturate(_GlobalSnowAmount) * saturate(_SnowGrassAmount);
    if (gate <= 0.0005h)
    {
        return litColor;   // 非雪天：直接返回，连噪声/遮罩都不采
    }

    half noise = SnowValueNoise(positionWS.xz * SNOW_GRASS_NOISE_SCALE);
    // noiseStrength 传 0：草的朝上程度不可靠，阈值扰动在草上没意义，噪声改成直接乘在覆盖度上
    half coverage = SnowSurfaceCoverage(positionWS, normalWS, (half)_SnowOverlayThreshold,
                                        (half)_SnowOverlaySoftness, 0.0h, 0.0h, SNOW_GRASS_NORMAL_FLOOR);
    half noiseMul = 1.0h - saturate(_SnowOverlayNoiseStrength) * (1.0h - noise);
    coverage = saturate(coverage * noiseMul * gate);

    // 用朝上程度给雪色一点明暗，避免整片死白（草片法线不可靠，这里只是加一点变化）
    half shade = 0.75h + 0.25h * saturate((half)normalWS.y * 0.5h + 0.5h);
    half3 snowColor = _SnowOverlayColor.rgb * shade;
    return lerp(litColor, snowColor, coverage);
}
