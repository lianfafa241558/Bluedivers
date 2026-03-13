// For more information, visit -> https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample

// This file is intented for you to edit and experiment with different lighting equation.
// Add or edit whatever code you want here

// #pragma once is a safe guard best practice in almost every .hlsl (need Unity2020 or up), 
// doing this can make sure your .hlsl's user can include this .hlsl anywhere anytime without producing any multi include conflict
#pragma once

half3 ShadeGI(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    // hide 3D feeling by ignoring all detail SH (leaving only the constant SH term)
    // we just want some average envi indirect color only
    half3 averageSH = SampleSH(0);

    // can prevent result becomes completely black if lightprobe was not baked 
    averageSH = max(_IndirectLightMinColor,averageSH);

    // occlusion (maximum 50% darken for indirect to prevent result becomes completely black)
    half indirectOcclusion = lerp(1, surfaceData.occlusion, 0.5);
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

half3 CompositeAllLightResults(half3 indirectResult, half3 mainLightResult, half3 additionalLightSumResult, half3 emissionResult,half3 colourResult, ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    //这里我们防止光线过亮，
    //同时仍要保持浅色的色调
        half3 rawLightSum = max(indirectResult, mainLightResult + additionalLightSumResult); // pick the highest between indirect and direct light
/*
#ifdef _MAIN_LIGHT_SHADOWS
    return lightingData.shadowCoord;
#endif
*/
    return ((1-surfaceData.colourMask)*surfaceData.albedo + colourResult)* rawLightSum/**lightingData.shadowCoord*/ + emissionResult;
}