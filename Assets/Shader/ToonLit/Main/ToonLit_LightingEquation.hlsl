// For more information, visit -> https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample

// This file is intented for you to edit and experiment with different lighting equation.
// Add or edit whatever code you want here

// #pragma once is a safe guard best practice in almost every .hlsl (need Unity2020 or up), 
// doing this can make sure your .hlsl's user can include this .hlsl anywhere anytime without producing any multi include conflict
#pragma once


// 方法功能：计算卡通渲染的全局光照（间接光 / 环境光）
half3 ShadeGI(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    half3 ambientColor;

#ifdef ToonShaderIsOutline
    // Outline pass：直接使用 Unity 内置的环境光颜色 uniform（由引擎自动填充）
    // 与 ForwardLit 保持一致，同样应用 _IndirectLightMinColor 保底
    ambientColor = unity_AmbientSky.rgb;
#else
    // 普通 ForwardLit pass：采样环境球谐光照(SH)，只取基础环境色
    ambientColor = SampleSH(0);
#endif

    // 确保间接光不会全黑（设置最低亮度）
    // 如果没有光照探针烘焙，使用 _IndirectLightMinColor 作为最低颜色
    ambientColor = max(_IndirectLightMinColor, ambientColor);

    // 计算间接光的遮挡效果
    //  Occlusion（遮挡）只影响50%的亮度，防止画面过暗、完全变黑
    half indirectOcclusion = lerp(1, surfaceData.occlusion, 0.5);

    // 最终返回：环境颜色 * 间接光遮挡
    return ambientColor * indirectOcclusion;
}


// 此功能将由所有直射灯使用(directional/point/spot)
half3 ShadeSingleLight(ToonSurfaceData surfaceData, ToonLightingData lightingData, Light light, bool isAdditionalLight)
{
    half3 N = lightingData.normalWS;
    half3 L = light.direction;

    half NoL = dot(N, L);

    //half lightAttenuation = 1;

    //点光源和聚光灯的灯光距离和角度渐变（请参见Lighting.hlsl中的GetAdditionalPerObjectLight（…））
    // Lighting.hlsl -> https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl
    //half distanceAttenuation = min(4,light.distanceAttenuation); //如果点光源/聚光灯离顶点太近，则夹紧以防止光线过亮
    half distanceAttenuation = min(1, isAdditionalLight ? sqrt(light.distanceAttenuation) : light.distanceAttenuation);
    //half distanceAttenuation = min(1, light.distanceAttenuation);
    //N点L
    //最简单的1线cel色调，你总是可以用自己的方法替换这条线！
    half litOrShadowArea = smoothstep(_CelShadeMidPoint - _CelShadeSoftness, _CelShadeMidPoint + _CelShadeSoftness, NoL);

    // occlusion
    litOrShadowArea *= surfaceData.occlusion;

    // 脸忽略celshade，因为使用NoL方法通常很难看
    litOrShadowArea = _IsFace ? lerp(0.5, 1, litOrShadowArea) : litOrShadowArea;

    // 灯光阴影图
    litOrShadowArea *= lerp(1, light.shadowAttenuation, _ReceiveShadowMappingAmount);

    half3 litOrShadowColor = lerp(_ShadowMapColor, 1, litOrShadowArea);

    half3 lightAttenuationRGB = litOrShadowColor * distanceAttenuation;

    //饱和（）light.color以防止过亮
    //额外的光会降低强度，因为它是相加的
    // saturate() light.color to prevent over bright
    // additional light reduce intensity since it is additive
    return light.color * lightAttenuationRGB;
    //return light.color * lightAttenuationRGB * (isAdditionalLight ? 0.25 : 1);
    //return saturate(light.color) * lightAttenuationRGB * (isAdditionalLight ? 0.25 : 1);
}

half3 ShadeColour(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    half3 result = 0;
    /*
    if (_UseColour)
    {

        float2 uv = lightingData.viewDirectionWS.xy * (_ColourTex_ST.xy + _ColourTex_ST.zw);
        float3 texColor = tex2D(_ColourTex, uv).rgb;
        result = texColor * surfaceData.colourMask;
    }*/
    return result;
}

half3 ShadeEmission(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    half3 emissionResult = lerp(surfaceData.emission, surfaceData.emission * surfaceData.albedo, _EmissionMulByBaseColor); // optional mul albedo
    return emissionResult;
}

//间接光，主要光，额外光
half3 CompositeAllLightResults(half3 indirectResult, half3 mainLightResult, half3 additionalLightSumResult, half3 emissionResult, half3 colourResult, ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    //这里我们防止光线过亮，
    //同时仍要保持浅色的色调
    half3 rawLightSum = max(indirectResult, mainLightResult + additionalLightSumResult); //在间接光和直接光之间拾取最高值
/*
#ifdef _MAIN_LIGHT_SHADOWS
    return lightingData.shadowCoord;
#endif
*/
    
    half3 specular = 0;
    /*
    if(_UseSpecular)specular= mainLightResult.rgb * surfaceData.specular * (1-smoothstep(0.5-_SpecularSoftness,0.5+_SpecularSoftness,pow(1-saturate(dot(R,V)),_Smoothness)));
    */
#ifdef ToonShaderIsOutline
    // Outline: 在间接光和直接光之间取最大值，避免两者叠加导致对环境光过于敏感
    half3 combinedLight = max(indirectResult, mainLightResult + additionalLightSumResult);
    return ((1 - surfaceData.colourMask) * surfaceData.albedo + colourResult) * combinedLight;
#else
    if (_UseSpecular)
    {

    
        //高光反射部分
        Light mainLight = GetMainLight();
        half3 specularResult = lerp(1, surfaceData.albedo, _SpecularMulByBaseColor) * mainLightResult.rgb * surfaceData.tangentAndSpecular.a * _SpecularColor.rgb;

        // 猜测1:双通道方向偏移 + 法线强度
        // 构建 TBN 矩阵（切线空间 → 世界空间）
        
        half3x3 TBN = half3x3(
        lightingData.tangentWS,
        lightingData.bitangentWS,
        lightingData.normalWS
        );
        // 从贴图获取切线空间法线
        half3 tangentNormal;
        tangentNormal.xy = (surfaceData.tangentAndSpecular.rg - 0.5) * 1;
        tangentNormal.z = sqrt(1 - saturate(dot(tangentNormal.xy, tangentNormal.xy)));
        // 转换到世界空间
        half3 finalNormal = normalize(mul(tangentNormal, TBN));
        
        
        //猜测3:切线空间细节法线偏移（基于纹理采样的切线空间偏移）
        /*
        half2 detailOffset = (surfaceData.tangentAndSpecular.rg - 0.5) * 1;
        half3 finalNormal = normalize(lightingData.normalWS +
            lightingData.tangentWS * detailOffset.x +
            lightingData.bitangentWS * detailOffset.y); 
        */
        
        float3 R = reflect(-mainLight.direction, finalNormal + _SpecularOffest * lightingData.tangentWS);
        float3 V = lightingData.viewDirectionWS;
        //float3 R = reflect(-mainLight.direction, lightingData.normalWS + _SpecularOffest * lightingData.tangentWS);
        //float3 V = lightingData.viewDirectionWS;
        R = normalize(R);
        V = normalize(V);
        
        specular = specularResult * (smoothstep(0.5 - _SpecularSoftness, 0.5 + _SpecularSoftness, pow(saturate(dot(R, V)), _Smoothness)));
        
        /*
        // 主高光（原始）
        float NdotV = dot(lightingData.normalWS, lightingData.viewDirectionWS);
        float primarySpec = pow(saturate(NdotV), _Smoothness);
    
        // 次级高光（偏移）
        float3 shiftedNormal = normalize(lightingData.normalWS + _SpecularOffest);
        float shiftedNdotV = dot(shiftedNormal, lightingData.viewDirectionWS);
        float secondarySpec = pow(saturate(shiftedNdotV), _Smoothness);
    
        // 混合两个高光
        float finalSpec = lerp(primarySpec, secondarySpec, 0.5);
    
        specular = specularResult * (smoothstep(0.5 - _SpecularSoftness,
                                            0.5 + _SpecularSoftness,
                                            finalSpec));
        */
        
    }
    return ((1 - surfaceData.colourMask) * surfaceData.albedo + colourResult) * rawLightSum /**lightingData.shadowCoord*/ + emissionResult + specular;

#endif

}