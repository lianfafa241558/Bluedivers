// For more information, visit -> https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample

// #pragma once is a safe guard best practice in almost every .hlsl (need Unity2020 or up), 
// doing this can make sure your .hlsl's user can include this .hlsl anywhere anytime without producing any multi include conflict
#pragma once

//我们在SRP/URP的包里已经没有“UnityCG.cginc”了，所以:
//包含以下两个hlsl文件，用通用管道着色就够了。一切都包含在其中。
// Core.hlsl将包含SRP着色器库，所有与材质无关的常量缓冲区(perobject，percamera，perframe)。
//还包括矩阵(matrix)/空间转换函数(space conversion functions)和雾(fog)。
// Lighting.hlsl将包含灯光函数/数据来抽象灯光常数。您应该使用GetMainLight和GetLight函数
//初始化光结构。Lighting.hlsl还包括GI，灯光BDRF函数。还包括阴影。

//所有通用渲染管道着色器都需要。
//它将包括Unity内置着色器变量(照明变量除外)
// (https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html
//它还会包含很多实用函数。
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//如果您正在执行光照着色器，请包含此选项。这包括照明着色器变量，
//照明和阴影功能
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

//SRP或URP着色器库中未定义材质着色器变量。
//这意味着_BaseColor、_BaseMap、_BaseMap_ST以及着色器属性部分中的所有变量
//必须由着色器本身定义。如果您在名为
// UnityPerMaterial，SRP可以缓存帧间的材质属性，显著降低成本
//每个drawcall的。
//在这种情况下，虽然URP的LitInput.hlsl包含了素材的CBUFFER
//上面定义的属性。可以看出，这不是ShaderLibrary的一部分，它专用于
// URP光照着色器。
//所以我们不打算用LitInput.hlsl，我们会自己实现一切。
//# include " Packages/com . unity . render-pipelines . universal/Shaders/litin put . hlsl "

//我们将包含一些实用utility.hlsl文件来帮助我们
#include "NiloOutlineUtil.hlsl"
#include "NiloZOffset.hlsl"
#include "NiloInvLerpRemap.hlsl"

//注意:
// subfix OS 表示 object 空间     (例如 positionOS = position object space)
// subfix WS 表示 world 空间      (例如 positionWS = position world space)
// subfix VS 表示 view 空间       (例如 positionVS = position view space)
// subfix CS 表示 clip 空间       (例如 positionCS = position clip space)


//所有过程将共享此属性结构(定义从Unity应用程序到我们的顶点着色器所需的数据)
struct Attributes
{
    float3 positionOS : POSITION;
    half3 normalOS : NORMAL;
    half4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uvAver : TEXCOORD3;
};

//所有过程将共享此变量结构(定义从顶点着色器到片段着色器所需的数据)
struct Varyings
{
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float4 positionWSAndFogFactor : TEXCOORD3; // xyz: 世界空间, w: vertex fog factor
    half3 normalWS : TEXCOORD4; //法线
    half3 tangentWS : TEXCOORD6; //切线
    float3 bitangentWS : TEXCOORD7; // 副切线
    float4 positionCS : SV_POSITION; //裁剪空间
    half3 subfixVS : TEXCOORD5; //相机空间

};


///////////////////////////////////////////////////////////////////////////////////////
// CBUFFER and Uniforms 
// (你应该把所有 uniforms of all passes 在这个单一的UnityPerMaterial CBUFFER里面！否则SRP批处理是不可能的！)
///////////////////////////////////////////////////////////////////////////////////////

//所有的sampler2D都不需要放在CBUFFER里面
sampler2D _BaseMap;
sampler2D _MouthMap;
sampler2D _AlphaMap;
sampler2D _EmissionMap;
sampler2D _OcclusionMap;
sampler2D _SpecularMap;
    //sampler2D _OutlineZOffsetMaskTex;
sampler2D _BlendingMap;
sampler2D _ColourTex;
sampler2D _ColourMaskTex;


// 把你所有的 uniforms(一般是里面的东西。着色器文件的属性{})，以使SRP批处理程序兼容
// see -> https://blogs.unity3d.com/2019/02/28/srp-batcher-speed-up-your-rendering/
CBUFFER_START(UnityPerMaterial)
    
    // high level settings
float _IsFace;

    // base color
float4 _BaseMap_ST;
half4 _BaseColor;
float4 _MouthMap_ST;
float4 _AlphaMap_ST;
float _Expression;
float _Column;
float _BaseScale;

float _BlendingScale;
float _UseUV1;

float4 _BlendingMap_ST;
    //hit
half3 _HitColor;
half3 _EdgeColor;
half _EdgeWidth;

half _UseMouthMap;
half _UseAlphaUV;
half _UseAlphaClipping;
half3 _DissolveValue;
    // emission
float _UseEmission;
float _EmissionMaskAddite;
half3 _EmissionColor;
half _EmissionMulByBaseColor;
half3 _EmissionMapChannelMask;
half _EmissionScale;
float4 _EmissionMap_ST;

    //specular
half _UseSpecular;
half4 _SpecularColor;
half _SpecularMulByBaseColor;
half _Smoothness;
half _SpecularSoftness;
half _SpecularOffest;
//half _AnisotropyScale;
    // occlusion
float _UseOcclusion;
    //float   _ReverseOcclusionColor;
half _OcclusionStrength;
half4 _OcclusionMapChannelMask;
half _OcclusionRemapStart;
half _OcclusionRemapEnd;

    // lighting
half3 _IndirectLightMinColor;
half _CelShadeMidPoint;
half _CelShadeSoftness;

    // shadow mapping
half _ReceiveShadowMappingAmount;
float _ReceiveShadowMappingPosOffset;
half3 _ShadowMapColor;
half _FogMaxValue;

    // outline
float _UseAverNormal;
float _FixOutlineColor;
float _OutlineWidth;
half3 _OutlineColor;
float _OutlineZOffset;
    //float   _OutlineZOffsetMaskRemapStart;
    //float   _OutlineZOffsetMaskRemapEnd;

    // colour
float _UseColour;
float4 _ColourTex_ST;
float4 _ColourMaskTex_ST;
float _ColourScale;
half4 _ColourColor;


CBUFFER_END

//仅用于applyShadowBiasFixToHClipPos()的特殊uniform，它不是每个材质的uniform，
//所以在我们的UnityPerMaterial CBUFFER之外写也可以
float3 _LightDirection;


struct ToonSurfaceData
{
    half3 albedo;
    half alpha;
    half3 emission;
    half4 tangentAndSpecular;
    half occlusion;
    half3 colourMask;
};
struct ToonLightingData
{
    float4 positionCS;
    half3 normalWS;

    half3 tangentWS;
    float3 bitangentWS; // 副切线
    float3 positionWS;
    half3 viewDirectionWS;
    float4 shadowCoord;
};

///////////////////////////////////////////////////////////////////////////////////////
//顶点共享函数
///////////////////////////////////////////////////////////////////////////////////////

//将平均法线转为本地空间
float3 UnpackNormalRG(float2 packednormal) //从UV中解码出法线
{
    float3 normal;
    normal.xy = packednormal * 2 - 1;
    normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
    return normal;
}

//将位置WS转换为轮廓位置WS
float3 TransformPositionWSToOutlinePositionWS(float3 positionWS, float positionVS_Z, float3 normalWS)
{
    //可以替换成自己的方法！这里我们会写一个简单的世界空间方法，因为教程的原因，它不是最好的方法！
    float width = _OutlineWidth * clamp(length(positionWS - _WorldSpaceCameraPos.xyz) / 10, 1, 3);
    //float width=_OutlineWidth*clamp(positionVS_Z/2,0.5,30);
    float outlineExpandAmount = width * GetOutlineCameraFovAndDistanceFixMultiplier(positionVS_Z);
    return positionWS + normalWS * outlineExpandAmount;
}

//如果未定义“ToonShaderIsOutline ”,则=执行常规MVP转换
//如果定义了“ToonShaderIsOutline ”=进行常规MVP变换+根据法线方向将顶点推出一点
Varyings VertexShaderWork(Attributes input)
{
    Varyings output;

    // VertexPositionInputs包含多个空间中的位置(世界、视图、同质剪辑空间、ndc)
    // Unity编译器会剥离所有不使用的引用(假设你不使用视图空间)。
    //因此，此结构在没有额外成本的情况下具有更大的灵活性。
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS);

    //与VertexPositionInputs类似，VertexNormalInputs将包含法线、切线和双切线
    //在世界空间中。如果不使用，它将被剥离。
    VertexNormalInputs vertexNormalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    float3 positionWS = vertexInput.positionWS;
    
#ifdef ToonShaderIsOutline
    if (_UseAverNormal)
    {
        float3 bitangentOS = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
        float3x3 OtoT = float3x3(input.tangentOS.xyz, bitangentOS, input.normalOS);
        float3 smoothNormalTS = UnpackNormalRG(input.uvAver); // 从UV3转切线空间
        float3 smoothNormalOS = normalize(mul(smoothNormalTS, OtoT)); //从切线转模型空间
        float3 smoothNormalWS = normalize(TransformObjectToWorldNormal(smoothNormalOS)); //从模型空间转世界空间

        positionWS = TransformPositionWSToOutlinePositionWS(vertexInput.positionWS, vertexInput.positionVS.z, smoothNormalWS);
    }
    else
    {
        positionWS = TransformPositionWSToOutlinePositionWS(vertexInput.positionWS, vertexInput.positionVS.z, vertexNormalInput.normalWS);
    }
#endif
    
    output.normalWS = vertexNormalInput.normalWS; //已通过GetVertexNormalInputs(...)进行了归一化处理
    output.tangentWS = vertexNormalInput.tangentWS;
    output.bitangentWS = normalize(cross(output.normalWS, output.tangentWS) * input.tangentOS.w);
    
    // Computes fog factor per-vertex.
    float fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    // TRANSFORM_TEX is the same as the old shader library.
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    if (_UseUV1)
    {
        output.uv1 = TRANSFORM_TEX(input.uv1, _BaseMap);
    }
    // packing positionWS(xyz) & fog(w) into a vector4
        output.positionWSAndFogFactor = float4(positionWS, fogFactor);
    output.positionCS = TransformWorldToHClip(positionWS);
    // 叉积计算副切线,注意要乘以 tangentOS.w（切线空间的符号）

    

    // 将世界坐标转换为相机空间坐标 //好像会超级卡，所以就算了
    //output.subfixVS= mul(UNITY_MATRIX_V, positionWS);
    output.subfixVS = positionWS;
    //output.subfixVS = ObjSpaceViewDir(vertexInput.positionCS);
    
    
#ifdef ToonShaderIsOutline
    
    output.positionCS = NiloGetNewClipPosWithZOffset(output.positionCS, _OutlineZOffset * 1 + 0.03 * _IsFace);

    //控制描边偏移的
    // [Read ZOffset mask texture]
    //我们不能在顶点着色器中使用tex2D()，因为栅格化前ddx & ddy是未知的，
    //所以使用tex2Dlod()和显式mip级别0，将显式mip级别0放在param uv的第4个组件中)
    //float outlineZOffsetMaskTexExplictMipLevel = 0;
    //float outlineZOffsetMask = tex2Dlod(_OutlineZOffsetMaskTex, float4(input.uv,0,outlineZOffsetMaskTexExplictMipLevel)).r; //我们假设它是黑色/白色 texture
     
    // [Remap ZOffset texture value]
    // 翻转纹理读取值，使默认的黑色区域=应用ZOffset，因为通常轮廓遮罩纹理使用此格式（黑色=隐藏轮廓）
    //outlineZOffsetMask = 1-outlineZOffsetMask;
    //outlineZOffsetMask = invLerpClamp(_OutlineZOffsetMaskRemapStart,_OutlineZOffsetMaskRemapEnd,outlineZOffsetMask);// allow user to flip value or remap

    // [Apply ZOffset, Use remapped value as ZOffset mask]
    //output.positionCS = NiloGetNewClipPosWithZOffset(output.positionCS, _OutlineZOffset * outlineZOffsetMask + 0.03 * _IsFace);
#endif

    // ShadowCaster pass needs special process to positionCS, else shadow artifact will appear
    //--------------------------------------------------------------------------------------
#ifdef ToonShaderApplyShadowBiasFix
    // see GetShadowPositionHClip() in URP/Shaders/ShadowCasterPass.hlsl
    // https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl
    // 注意:URP 的 ApplyShadowBias 已内置 normal/depth bias(由 URP Asset 的 Shadow Settings 控制),
    // 不要再手动沿法线外扩顶点,否则会把投射阴影轮廓整体撑大.
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, output.normalWS, _LightDirection));
    
    //防止相机过近导致的镂空
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    output.positionCS = positionCS;
#endif
    //--------------------------------------------------------------------------------------    

    return output;
}

//根据距离(30-40)淡化阴影
float GetDistanceFade(float3 positionWS)
{
    float4 posVS = mul(GetWorldToViewMatrix(), float4(positionWS, 1));
    //return posVS.z;
#if UNITY_REVERSED_Z
    float vz = -posVS.z;
#else
    float vz = posVS.z;
#endif
    // jave.lin : 30.0 : start fade out distance, 40.0 : end fade out distance
    float fade = 1 - smoothstep(30, 40, vz);
    return fade;
}



///////////////////////////////////////////////////////////////////////////////////////
// 共享函数(步骤1:为照明计算准备数据结构)
///////////////////////////////////////////////////////////////////////////////////////
//转相机视角
inline float3 ObjSpaceViewDir(in float4 v)
{
    float3 objSpaceCameraPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1)).xyz;
    return objSpaceCameraPos - v.xyz;
}


half4 HSVToRGB(half3 c)
{
    half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return half4(c.z * lerp(K.xxx, saturate(p - K.xxx), c.y), 1);
}

//将half3变为half2
half2 vertexToHalf2Method1(float3 vertexPos)
{
    //frac在边界处会出现跳变，所以干脆不用了
    half2 uv = half2(
        (vertexPos.x * 0.14159 + vertexPos.z * 0.26795),
        (vertexPos.y * 0.31831 + vertexPos.x * 0.41421)
    );
    return uv;
}

half4 GetFinalBaseColor(Varyings input)
{ //计算基础颜色

    half4 backCol = tex2D(_BaseMap, input.uv);
    half4 col = half4(backCol.rgb, 1);
    if (_UseMouthMap)
    {
        float2 uv2 = input.uv;
        uv2.x *= 0.5;
        uv2.y *= 0.5;
        uv2.x += fmod(_Expression / _MouthMap_ST.x, _Column) / _Column;
        uv2.y = uv2.y + int(_Expression / _Column) / _MouthMap_ST.x / _Column;
        half4 texCol = tex2D(_MouthMap, TRANSFORM_TEX(uv2, _MouthMap)); //用贴图*旁边的设置得到最终uv
        if (input.uv[0] < 0.3 && input.uv[1] < 0.3)
        {
            col.a = texCol.a;
            col.rgb = col.rgb * (1 - texCol.a) + texCol.rgb * texCol.a; //b贴图上面空白的位置置0;
        }
    }
    if (_UseAlphaClipping)
    {
        half dissolve = (_DissolveValue.r * 1.2 - 0.1);
        half v = _UseAlphaUV ? tex2D(_AlphaMap, input.uv).r : tex2D(_AlphaMap, vertexToHalf2Method1(input.subfixVS)).r;
        col.a = step(dissolve, v); //col.a * v;
    }
    if (_BlendingScale)
    {
        float2 baseOffset = _BaseMap_ST.zw; // 获取偏移值
        float2 correctedUV = (_UseUV1 ? input.uv1 : input.uv) - baseOffset; // 反向补偿
        col = col * (1 - _BlendingScale) + col * _BlendingScale * (tex2D(_BlendingMap, correctedUV * _BlendingMap_ST.xy + _BlendingMap_ST.zw) * 2 - 1) * backCol.a;
    }
    
    return col * _BaseColor * _BaseScale;
}
half3 GetFinalEmissionColor(Varyings input)//计算自发光颜色
{
    half3 result = 0;
    result = _HitColor;
    if (_UseEmission)
    {
        if (_EmissionMaskAddite)
        {
            half3 value = tex2D(_EmissionMap, input.uv *_EmissionMap_ST.xy).rgb * _EmissionMapChannelMask;
            result += (value.r + value.g + value.b) * _EmissionColor.rgb * _EmissionScale;
        }
        else
        {
            result += tex2D(_EmissionMap, input.uv*_EmissionMap_ST.xy).rgb * _EmissionColor.rgb  * _EmissionScale;
            //result += tex2D(_EmissionMap, input.uv).rgb * _EmissionMapChannelMask * _EmissionColor.rgb*_EmissionScale;
        }
        
    }
    return result;
}
half GetFinalOcculsion(Varyings input)//计算环境光遮罩？
{
    half result = 1;
    if (_UseOcclusion)
    {
        half4 texValue = tex2D(_OcclusionMap, input.uv);
        //if(_ReverseOcclusionColor)texValue=1-texValue;
        half occlusionValue = dot(texValue, _OcclusionMapChannelMask);
        occlusionValue = lerp(1, occlusionValue, _OcclusionStrength);
        occlusionValue = invLerpClamp(_OcclusionRemapStart, _OcclusionRemapEnd, occlusionValue);
        result = occlusionValue;
    }

    return result;
}
half4 GetFinalSpecular(Varyings input)//计算高光贴图
{
    half4 result = 0;
    result = tex2D(_SpecularMap, input.uv); 
    
    return result;
}

half3 GetFinalColourColor(Varyings input)//计算"色彩"遮罩(效果在光照文件中结算)
{
    half3 result = 0;
    if (_UseColour)
    {
        result = tex2D(_ColourMaskTex, input.uv * _ColourMaskTex_ST.xy).rgb *_ColourColor;
        //result = tex2D(_ColourTex, input.uv).rgb *_ColourColor*tex2D(_ColourMaskTex, input.uv).rgb;
    }
    return result;
}

void DoClipTestToTargetAlphaValue(half alpha) //进行Clip以确定Alpha值(透明度小于灰色的将不被渲染)
{
    if (_UseMouthMap)
    {
        clip(alpha - 0.5);
    }
    else if (_UseAlphaClipping)
    {
        clip(alpha - (_DissolveValue.r * 1.2 - 0.1) + _EdgeWidth);
    }

}



ToonSurfaceData InitializeSurfaceData(Varyings input)
{
    ToonSurfaceData output;
    // albedo & alpha
    float4 baseColorFinal;
    baseColorFinal = GetFinalBaseColor(input);
    output.albedo = baseColorFinal.rgb;
    output.alpha = baseColorFinal.a;
    output.tangentAndSpecular = GetFinalSpecular(input);
    //if (input.uv.x > _DissolveValue) output.alpha = 0;
    

    DoClipTestToTargetAlphaValue(output.alpha); // early exit if possible


    if (_UseAlphaClipping)
    {
        half v = _UseAlphaUV ? tex2D(_AlphaMap, input.uv).r : tex2D(_AlphaMap, vertexToHalf2Method1(input.subfixVS)).r;

        half dissolve = (_DissolveValue.r * 1.2 - 0.1);
        half isDissolved = step(dissolve + _EdgeWidth, v); //阶跃方法(小于dissolve时0，大于时1)

        //half3 edgeColor = _EdgeColor * smoothstep(dissolve+_EdgeWidth, dissolve, v);
        //half3 edgeColor = _EdgeColor * smoothstep(dissolve+_EdgeWidth, dissolve, v);
        half3 edgeColor = _EdgeColor * lerp(dissolve + _EdgeWidth, dissolve, v);

        output.albedo.rgb = lerp(output.albedo.rgb, edgeColor, 1 - isDissolved);
    }

    // emission
    output.emission = GetFinalEmissionColor(input);

    // occlusion
    output.occlusion = GetFinalOcculsion(input);
    //色彩遮罩
    output.colourMask = GetFinalColourColor(input);
     
    return output;
}

ToonLightingData InitializeLightingData(Varyings input)
{
    ToonLightingData lightingData;
    lightingData.positionCS = input.positionCS;
    lightingData.positionWS = input.positionWSAndFogFactor.xyz;

    lightingData.viewDirectionWS = SafeNormalize(GetCameraPositionWS() - lightingData.positionWS);
    lightingData.normalWS = normalize(input.normalWS); //插值法得到的法线不是单位向量，我们需要对其进行归一化处理
    lightingData.tangentWS = input.tangentWS;
    lightingData.bitangentWS = input.bitangentWS;

    return lightingData;
}

///////////////////////////////////////////////////////////////////////////////////////
//分割共享函数(步骤2:计算照明和最终颜色)
///////////////////////////////////////////////////////////////////////////////////////

//所有的照明方程式都写在这里面。hlsl，
//只是通过编辑这个。hlsl可以控制大部分的视觉效果。
#include "ToonLit_LightingEquation.hlsl"

//这个函数不包含照明逻辑，它只是传递照明结果数据
//这个函数完成的工作是“做阴影贴图深度测试位置WS偏移”
half3 ShadeAllLights(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    //间接照明
    half3 indirectResult = ShadeGI(surfaceData, lightingData);

    
    //填充 InputData
    InputData inputData = (InputData) 0;
    inputData.positionWS = lightingData.positionWS;
    inputData.normalWS = lightingData.normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(lightingData.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(lightingData.positionCS);
    
    //////////////////////////////////////////////////////////////////////////////////
    //光线结构由URP提供，用于抽象光线着色器变量。
    //它包含光的
    //  -方向
    //  -颜色
    //  -距离衰减
    //  -阴影衰减
 
    // URP根据光线和平台采取不同的明暗处理方法。
    //永远不要在着色器中引用灯光着色器变量，而是使用
    // -GetMainLight()
    // -GetLight()
    //函数填充这个光结构。
    //////////////////////////////////////////////////////////////////////////////////

    //==============================================================================================
    //主光是最亮的平行光。
    //它在灯光循环之外被着色，并且它有一组特定的变量和着色路径
    //所以我们可以在只有单一方向光的情况下尽可能快
    //您可以选择性地传递一个shadowCoord。如果是这样，将计算阴影衰减。
    Light mainLight = GetMainLight();

    float3 shadowTestPosWS = lightingData.positionWS + mainLight.direction * (_ReceiveShadowMappingPosOffset + _IsFace);
#ifdef _MAIN_LIGHT_SHADOWS
    //由于此更改，现在计算片段着色器中的阴影坐标
    // https://forum.unity.com/threads/shadow-cascades-weird-since-7-2-0.828453/#post-5516425

    //_ ReceiveShadowMappingPosOffset将控制阴影比较位置的偏移量，
    //这样做通常是为了隐藏脸部等阴影敏感区域的丑陋自我阴影
    float4 shadowCoord = TransformWorldToShadowCoord(shadowTestPosWS);
    //mainLight.shadowAttenuation = MainLightRealtimeShadow(shadowCoord);

    mainLight.shadowAttenuation =lerp(1, MainLightRealtimeShadow(shadowCoord),  GetDistanceFade(shadowTestPosWS));
    //lightingData.shadowCoord=shadowCoord;
#endif 

    // Main light
    half3 mainLightResult = ShadeSingleLight(surfaceData, lightingData, mainLight, false);

    //==============================================================================================
    // All additional lights

    half3 additionalLightSumResult = 0;

//#ifdef _ADDITIONAL_LIGHTS
    
    // 4. 附加光 – 直接使用 LIGHT_LOOP_BEGIN，自动处理所有可见灯光
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)

    Light light = GetAdditionalLight(lightIndex, lightingData.positionWS);
    //light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, shadowTestPosWS); //使用偏移位置WS进行阴影测试
    additionalLightSumResult += ShadeSingleLight(surfaceData, lightingData, light, true);
    LIGHT_LOOP_END



    //==============================================================================================
    // 色彩
    half3 colourResult = ShadeColour(surfaceData, lightingData);
    // emission
    half3 emissionResult = ShadeEmission(surfaceData, lightingData);

    return CompositeAllLightResults(indirectResult, mainLightResult, additionalLightSumResult, emissionResult, colourResult, surfaceData, lightingData);
}

half3 ConvertSurfaceColorToOutlineColor(half3 originalSurfaceColor, half2 uv)
{
    half3 outlineColor = half3(1, 1, 1); // 初始化轮廓颜色
    
    //这个贴图默认是黑的，所以要倒置
    if (_FixOutlineColor)
    {
        //half3 mask = 1 - tex2D(_OutlineZOffsetMaskTex, uv).rgb;
        //outlineColor = originalSurfaceColor * (1 - mask.r) + mask.r * saturate(_OutlineColor);
        outlineColor = _OutlineColor;
    }
    else
    {
        outlineColor = originalSurfaceColor * _OutlineColor;
    }
    return outlineColor;
}

half3 ApplyFog(half3 color, Varyings input)
{
    half fogFactor = input.positionWSAndFogFactor.w;
    color = MixFog(color, fogFactor / (1 + (color.r + color.g + color.b) / 3) * _FogMaxValue);

    return color;
}

// only the .shader file will call this function by 
// #pragma fragment ShadeFinalColor
half4 ShadeFinalColor(Varyings input) : SV_TARGET
{
    //////////////////////////////////////////////////////////////////////////////////////////
    // 首先准备照明功能的所有数据
    //////////////////////////////////////////////////////////////////////////////////////////
    
    // 填充ToonSurfaceData结构：
    ToonSurfaceData surfaceData = InitializeSurfaceData(input);

    // 填充 ToonLightingData 结构:
    ToonLightingData lightingData = InitializeLightingData(input);
 
    
    //应用所有照明计算
    half3 color = ShadeAllLights(surfaceData, lightingData);

#ifdef ToonShaderIsOutline
     color = ConvertSurfaceColorToOutlineColor(color, input.uv);
#endif

    color = ApplyFog(color, input);

    

    return half4(color, surfaceData.alpha);
}

//////////////////////////////////////////////////////////////////////////////////////////
// 共享功能(仅用于ShadowCaster通道和DepthOnly通道)
//////////////////////////////////////////////////////////////////////////////////////////
half4 BaseColorAlphaClipTest(Varyings input) : SV_Target
{
    return half4(input.normalWS,1);
    //DoClipTestToTargetAlphaValue(GetFinalBaseColor(input).a);
}

