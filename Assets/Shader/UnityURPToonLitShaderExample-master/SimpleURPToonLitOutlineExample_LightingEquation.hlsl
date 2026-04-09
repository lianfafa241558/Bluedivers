// For more information, visit -> https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample

// This file is intented for you to edit and experiment with different lighting equation.
// Add or edit whatever code you want here

// #pragma once is a safe guard best practice in almost every .hlsl (need Unity2020 or up), 
// doing this can make sure your .hlsl's user can include this .hlsl anywhere anytime without producing any multi include conflict
#pragma once


// 方法功能：计算卡通渲染的全局光照（间接光 / 环境光）
half3 ShadeGI(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    // 采样环境球谐光照(SH)，只取基础环境色（忽略复杂细节，保留平均色调）
    // 注释原意：通过忽略所有细节SH，隐藏3D立体感，只保留基础的环境颜色
    half3 averageSH = SampleSH(0);

    // 确保间接光不会全黑（设置最低亮度）
    // 如果没有光照探针烘焙，使用 _IndirectLightMinColor 作为最低颜色
    averageSH = max(_IndirectLightMinColor, averageSH);

    // 计算间接光的遮挡效果
    //  Occlusion（遮挡）只影响50%的亮度，防止画面过暗、完全变黑
    half indirectOcclusion = lerp(1, surfaceData.occlusion, 0.5);

    // 最终返回：环境颜色 * 间接光遮挡
    return averageSH * indirectOcclusion;
}


// 此功能将由所有直射灯使用(directional/point/spot)
half3 ShadeSingleLight(ToonSurfaceData surfaceData, ToonLightingData lightingData, Light light,bool isAdditionalLight)
{
    half3 N = lightingData.normalWS;
    half3 L = light.direction;

    half NoL = dot(N,L);

    //half lightAttenuation = 1;

    //点光源和聚光灯的灯光距离和角度渐变（请参见Lighting.hlsl中的GetAdditionalPerObjectLight（…））
    // Lighting.hlsl -> https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl
    //half distanceAttenuation = min(4,light.distanceAttenuation); //如果点光源/聚光灯离顶点太近，则夹紧以防止光线过亮
    half distanceAttenuation = min(1,isAdditionalLight?sqrt(light.distanceAttenuation):light.distanceAttenuation);
    //N点L
    //最简单的1线cel色调，你总是可以用自己的方法替换这条线！
    half litOrShadowArea = smoothstep(_CelShadeMidPoint-_CelShadeSoftness,_CelShadeMidPoint+_CelShadeSoftness, NoL);

    // occlusion
    litOrShadowArea *= surfaceData.occlusion;

    // 脸忽略celshade，因为使用NoL方法通常很难看
    litOrShadowArea = _IsFace? lerp(0.5,1,litOrShadowArea) : litOrShadowArea;

    // 灯光阴影图
    litOrShadowArea *= lerp(1,light.shadowAttenuation,_ReceiveShadowMappingAmount);

    half3 litOrShadowColor = lerp(_ShadowMapColor,1, litOrShadowArea);

    half3 lightAttenuationRGB = litOrShadowColor * distanceAttenuation;

    //饱和（）light.color以防止过亮
    //额外的光会降低强度，因为它是相加的
    // saturate() light.color to prevent over bright
    // additional light reduce intensity since it is additive
    return light.color * lightAttenuationRGB* (isAdditionalLight ? 0.25 : 1);
    //return saturate(light.color) * lightAttenuationRGB * (isAdditionalLight ? 0.25 : 1);
}

half3 ShadeColour(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    half3 result = 0;
    if(_UseColour)
    {
        //这个写法隐式将lightingData.viewDirectionWS *从float3截断为float2
        result = tex2D(_ColourTex, lightingData.viewDirectionWS.xy * _ColourTex_ST)*surfaceData.colourMask;

    }
    return result;
}

half3 ShadeEmission(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    half3 emissionResult = lerp(surfaceData.emission, surfaceData.emission * surfaceData.albedo, _EmissionMulByBaseColor); // optional mul albedo
    return emissionResult;
}

//间接光，主要光，额外光
half3 CompositeAllLightResults(half3 indirectResult, half3 mainLightResult, half3 additionalLightSumResult, half3 emissionResult,half3 colourResult, ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    //这里我们防止光线过亮，
    //同时仍要保持浅色的色调
        half3 rawLightSum = max(indirectResult, mainLightResult + additionalLightSumResult); //在间接光和直接光之间拾取最高值
/*
#ifdef _MAIN_LIGHT_SHADOWS
    return lightingData.shadowCoord;
#endif
*/
    return ((1-surfaceData.colourMask)*surfaceData.albedo + colourResult)* rawLightSum/**lightingData.shadowCoord*/ + emissionResult;
}