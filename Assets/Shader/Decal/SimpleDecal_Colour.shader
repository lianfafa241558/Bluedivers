// see README here:
// github.com/ColinLeung-NiloCat/UnityURPUnlitScreenSpaceDecalShader
//
// 精简自 SimpleDecal：去掉 _EmittierColor/_AlphaRemap/_UseMaskMap，保留 _MainTex/_Color 与"色彩"核心（移植自 ToonLit_Colour）：
// 1. 用贴花 UV 采样 _MainTex * _Color 得到基础印花；_ColourMaskTex * _ColourColor 得到色彩遮罩（对应 ToonLit GetFinalColourColor）
// 2. 用"像素->相机"方向作为 UV 采样 _ColourTex，得到随视角变化的颜色（对应 ToonLit ShadeColour）
// 注意：ToonLit 原版用 viewDirectionWS.xy（角色沿 Z 轴观察，xy 是屏幕平面两个变化分量）；
// 贴花需要任意视角都正常，单一平面投影（.xy 或 .xz）会在视线与被丢弃轴对齐时退化成条纹，
// 故采用**三平面采样**：zy/xz/xy 三组分量各采一次，用视角分量绝对值做权重混合——
// 某组分量退化时其权重恰好趋近 0，条纹被隐藏，立体感在任何视角下都成立。
// 合成：遮罩区域用视角颜色替换主纹理颜色，_ColourScale 控制替换程度；透明度 = max(主纹理alpha, 遮罩亮度*_ColourColor.a)

Shader "Decal/SimpleDecal_Colour"
{
    Properties
    {
        [Header(Basic)]
        [MainTexture]_MainTex("Texture", 2D) = "white" {}
        [Toggle] _UseMainAlpha("_UseMainAlpha", Float) = 1

        //[NoScaleOffset]
        _MainMaskTex("_MainMaskTex(颜色遮罩)", 2D) = "white" {}
        [MainColor][HDR]_Color("_Color", Color) = (1,1,1,1)


        [Header(Blending)]
        [Enum(UnityEngine.Rendering.BlendMode)]_DecalSrcBlend("_DecalSrcBlend", Int) = 5 // 5 = SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)]_DecalDstBlend("_DecalDstBlend", Int) = 10 // 10 = OneMinusSrcAlpha

        [Header(Stencil Masking)]
        _StencilRef("_StencilRef", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)]_StencilComp("_StencilComp", Float) = 0 //0 = disable
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("_Cull", Float) = 1 //1 = Front

        [Header(_Colour)]
        [HDR]_ColourColor("色彩颜色", Color) = (1,1,1,1)
        _ColourTex("_ColourTex(视角颜色)", 2D) = "white" {}
        //[NoScaleOffset]
        _ColourMaskTex("_ColourMaskTex(色彩遮罩)", 2D) = "white" {}

         [Space(15)] 
        [Toggle(_UnityFogEnable)] _UnityFogEnable("_UnityFogEnable", Float) = 1
    }

    SubShader
    {
        // 为了避免渲染顺序问题，队列必须 >= 2501，这样才能进入透明队列、
        // 在透明队列中，Unity 将始终从后向前绘制
        Tags { "RenderType" = "Overlay" "Queue" = "Transparent-499" "DisableBatching" = "True" }

        Pass
        {
            Stencil
            {
                Ref[_StencilRef]
                Comp[_StencilComp]
            }

            Cull[_Cull]
            ZTest Off

            ZWrite off
            Blend[_DecalSrcBlend][_DecalDstBlend]

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // make fog work
            #pragma multi_compile_fog

            // due to using ddx() & ddy()
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma shader_feature_local _UnityFogEnable
            struct appdata
            {
                float3 positionOS : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 viewRayOS : TEXCOORD1; // xyz: viewRayOS, w: extra copy of positionVS.z
                float3 cameraPosOS : TEXCOORD2;
            };

            sampler2D _CameraDepthTexture;
            sampler2D _MainTex;
            sampler2D _MainMaskTex;
            sampler2D _ColourTex;
            sampler2D _ColourMaskTex;

            CBUFFER_START(UnityPerMaterial)
                // basic
                float4 _MainTex_ST;
                float4 _MainMaskTex_ST;
                half4 _Color;
                half _UseMainAlpha;
                // colour
                half4 _ColourColor;
                float4 _ColourTex_ST;
                float4 _ColourMaskTex_ST;

            CBUFFER_END

            v2f vert(appdata input)
            {

                v2f o;
                // 顶点位置输入（VertexPositionInputs）包含多个空间（世界、视图、同质剪辑空间、ndc）中的位置。
                // Unity 编译器会删除所有未使用的引用（例如不使用视图空间）。
                // 因此，使用该结构可以在不增加额外成本的情况下提高灵活性。
                VertexPositionInputs vertexPositionInput = GetVertexPositionInputs(input.positionOS);
                o.positionCS = vertexPositionInput.positionCS;


                // 准备深度纹理的屏幕空间 UV
                o.screenPos = ComputeScreenPos(o.positionCS);

                // 获取视图空间中 “摄像机到顶点 ”的射线
                float3 viewRay = vertexPositionInput.positionVS;

                //“viewRay z分割”必须在片段着色器中执行，而不是顶点着色器！（由于光栅化变化插值的透视校正）
                //我们暂时跳过顶点着色器中的“viewRay z分割”，先将分割值存储到不同的o.viewRayOS.w中，
                //稍后我们将在进入片段着色器时进行分割
                o.viewRayOS.w = viewRay.z;
                //unity的摄影机空间是右手坐标（负z指向屏幕），我们希望片段着色器中的z射线为正，因此将其取反
                viewRay *= -1;

                //在贴花的顶点着色器中编写非常昂贵的代码是可以的，
                //每个贴花只有一个统一立方体（4*6个顶点），根本不会影响GPU性能。
                float4x4 ViewToObjectMatrix = mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V);

                //首先在顶点着色器中将所有内容转换为对象空间（贴花空间），这样我们就可以跳过片段着色器中的所有matrix mul（）
                o.viewRayOS.xyz = mul((float3x3)ViewToObjectMatrix, viewRay);
                //相机位置变换到贴花空间，作为 frag 中深度重建射线的起点
                o.cameraPosOS = mul(ViewToObjectMatrix, float4(0,0,0,1)).xyz; //硬代码0或1可以实现许多编译器优化

                // 注意：这里【不能】按普通物体那样用顶点 o.positionCS.z 算雾因子！
                // 本 Shader 用一个 cube 罩住目标区域（TheMarker.prefab 里该 cube 缩放 199），
                // 而 Cull Front 渲染到的是 cube 的【背面】——比真正的贴花表面远得多（可差上百米），
                // 顶点插值出的雾因子会按"远端 cube 面"的距离算雾，导致雾明显偏浓。
                // 雾因子改为在 frag 中按重建出的场景深度计算，见下方。

                return o;
            }

            //复制自URP12.1.2的着色器变量函数.hlsl
            #if SHADER_LIBRARY_VERSION_MAJOR < 12
            float LinearDepthToEyeDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return _ProjectionParams.z - (_ProjectionParams.z - _ProjectionParams.y) * rawDepth;
                #else
                    return _ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y) * rawDepth;
                #endif
            }
            #endif

            half4 frag(v2f i) : SV_Target
            {
                
                // [important note]
                //========================================================================
                //现在执行我们之前在顶点着色器中跳过的“viewRay z分割”。
                i.viewRayOS.xyz /= i.viewRayOS.w;
                //========================================================================

                float2 screenSpaceUV = i.screenPos.xy / i.screenPos.w;
                float sceneRawDepth = tex2D(_CameraDepthTexture, screenSpaceUV).r;

                float3 decalSpaceScenePos;

                //如果是透视相机，LinearEyeDepth将为用户处理一切
                //记住，我们不能将LinearEyeDepth用于正交相机！
                float sceneDepthVS = LinearEyeDepth(sceneRawDepth,_ZBufferParams);

                //任意空间中的场景深度=rayStartPos+rayDir*rayLength
                //这里是ObjectSpace（OS）或DecalSpace中的所有数据
                //请注意，viewRayOS不是一个单位向量，所以不要对其进行归一化，它是一个方向向量，视图空间z的长度为1
                decalSpaceScenePos = i.cameraPosOS + i.viewRayOS.xyz * sceneDepthVS;


                //丢弃“超出立方体体积”的像素
                float shouldClip = 0;
                //如果ZWrite处于关闭状态，则clip（）在移动设备上足够快，因为它不会写入DepthBuffer，因此GPU管道不会停滞（由ARM工作人员确认）。
                clip(0.5 - abs(decalSpaceScenePos) - shouldClip);


                 // 0. 遮罩：用贴花 UV 采样色彩遮罩并乘上色彩颜色（同 ToonLit GetFinalColourColor，
                //    遮罩白=受色彩影响，黑=不受影响）
                // convert unity cube's [-0.5,0.5] vertex pos range to [0,1] uv. Only works if you use a unity cube in mesh filter!
                float2 decalSpaceUV = decalSpaceScenePos.xy + 0.5;


                // 1. 基础印花：主纹理 * 染色
                float2 mainUV = decalSpaceUV * _MainTex_ST.xy + _MainTex_ST.zw;
                half4 baseCol = tex2D(_MainTex, mainUV) * _Color;
                if(_UseMainAlpha==0)baseCol.a=_Color.a;
                half4 mainMask= tex2D(_MainMaskTex, decalSpaceUV * _MainMaskTex_ST.xy + _MainMaskTex_ST.zw);

                baseCol.a*=mainMask.r*mainMask.a;

                half colourMask = tex2D(_ColourMaskTex, decalSpaceUV * _ColourMaskTex_ST.xy + _ColourMaskTex_ST.zw).r;

                // 2. 视角颜色：重建投影点的世界坐标，用"像素->相机"方向做 UV 采样
                //    注意 DisableBatching=True，对象矩阵是单物体矩阵，重建结果正确
                float3 positionWS = mul(GetObjectToWorldMatrix(), float4(decalSpaceScenePos, 1)).xyz;
                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - positionWS);

                // 三平面采样：三组分量各采一次，按视角分量绝对值加权混合。
                // 幂次（4）越高，过渡越锐利、越接近"只取主导平面"；纹理大小仍用 _ColourTex 的 Tiling 调节
                float3 blendWeights = pow(abs(viewDirWS), 4);
                blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z);

                float2 st = _ColourTex_ST.xy;
                float2 off = _ColourTex_ST.zw;
                half3 colourX = tex2D(_ColourTex, viewDirWS.zy * st + off).rgb; // 侧视（x 主导）
                half3 colourY = tex2D(_ColourTex, viewDirWS.xz * st + off).rgb; // 俯视（y 主导）
                half3 colourZ = tex2D(_ColourTex, viewDirWS.xy * st + off).rgb; // 正视（z 主导）
                half3 viewColour = colourX * blendWeights.x + colourY * blendWeights.y + colourZ * blendWeights.z;

                half4 col;
                // 合成：遮罩区域用视角颜色替换主纹理颜色，_ColourScale 控制替换程度
                // （_ColourScale=0 纯印花，=1 遮罩区域完全变成视角颜色）
                half4 withColour = half4(viewColour * _ColourColor.rgb,colourMask*_ColourColor.a);

                // 预乘 Alpha：确保透明区域完全不影响背景（保留背景雾效）
                baseCol.rgb *= baseCol.a;
                withColour.rgb *= withColour.a;
                col = baseCol+withColour;


                #if _UnityFogEnable && (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
                // unity的雾气效果：雾因子必须按【真实被贴表面】的深度算，不能用 cube 顶点算（原因见 vert）。
                // sceneDepthVS 正是从 _CameraDepthTexture 重建出来的场景表面视空间深度，
                // 也就是贴花所覆盖的那个像素的真实距离——和背景/地形算出来的雾完全一致，且省掉一次矩阵乘法。
                // 换算方式对齐 URP 官方片元雾（ShaderVariablesFunctions.hlsl 的 InitializeInputDataFog）：
                // 视空间深度以相机处为 0，重映射到近平面（减去 _ProjectionParams.y）
                float fogFactor = ComputeFogFactorZ0ToFar(max(sceneDepthVS - _ProjectionParams.y, 0.0));
                col.rgb = MixFog(col.rgb, fogFactor);
                #endif



                return col;
            }
            ENDHLSL
        }
    }
}
