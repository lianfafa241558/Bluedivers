// see README here: 
// github.com/ColinLeung-NiloCat/UnityURPUnlitScreenSpaceDecalShader

Shader "Decal/SimpleDecal"
{
    Properties
    {
        [Header(Basic)]
        [MainTexture]_MainTex("Texture", 2D) = "white" {}
        [MainColor][HDR]_Color("_Color", Color) = (1,1,1,1)
        [HDR]_EmittierColor("_EmittierColor", Color) = (1,1,1,1)

        [Header(Blending)]
        [Enum(UnityEngine.Rendering.BlendMode)]_DecalSrcBlend("_DecalSrcBlend", Int) = 5 // 5 = SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)]_DecalDstBlend("_DecalDstBlend", Int) = 10 // 10 = OneMinusSrcAlpha

        // 加法式混合专用开关（如 _DecalSrcBlend=SrcAlpha / _DecalDstBlend=DstAlpha：背景被 dst*dstAlpha
        // 原样保留，贴花是"往上加一层"）。这种混合下雾只能【衰减】贴花自身颜色，不能把雾色混进来：
        // 背景在不透明阶段已经算过雾了，再混一份雾色 = 整块足迹多出一份雾色（贴图全黑处也变雾色方块）。
        [Toggle(_DecalAdditiveFog)] _DecalAdditiveFog("加法混合(雾只衰减)", Float) = 0

        [Header(Alpha remap(extra alpha control))]
        _AlphaRemap("_AlphaRemap", vector) = (1,0,0,0)
        [Toggle(_UseMaskMap)] _UseMaskMap("_UseMaskMap", Float) = 0

        [Header(Stencil Masking)]
        _StencilRef("_StencilRef", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)]_StencilComp("_StencilComp", Float) = 0 //0 = disable
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("_Cull", Float) = 1 //1 = Front
        //[Enum(UnityEngine.Rendering.CompareFunction)]_ZTest("_ZTest", Float) = 4 //4 = LEqual

        //[Toggle(_UseFog)] _UseFog("Use Unity Fog", Float) = 1

    }

    SubShader
    {
        // 为了避免渲染顺序问题，队列必须 >= 2501，这样才能进入透明队列、 
        // 在透明队列中，Unity 将始终从后向前绘制
         // https://github.com/ColinLeung-NiloCat/UnityURPUnlitScreenSpaceDecalShader/issues/6#issuecomment-615940985

         // https://docs.unity3d.com/Manual/SL-SubShaderTags.html
         // 2500 以下的队列（“几何图形+500”）被视为 “不透明 ”队列，会优化对象的绘制顺序以获得最佳性能。
         // 更高的渲染队列被视为 “透明对象”，并按距离对对象进行排序、 
         // 从最远的对象开始渲染，以最近的对象结束。
         // 在所有不透明物体和透明物体之间绘制天空盒。
         // 队列“=”透明-499 “表示 ”队列“=”2501“，几乎等同于 ”在透明对象之前绘制"。

         //“DisableBatching ”表示禁用 “动态批处理”，而不是 “srp 批处理”。

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

            #pragma shader_feature_local _UseMaskMap

            // 加法混合材质（见 Properties 上方说明）开启后，雾只做衰减、不叠雾色
            #pragma shader_feature_local _DecalAdditiveFog

            struct appdata
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 viewRayOS : TEXCOORD1; // xyz: viewRayOS, w: extra copy of positionVS.z 
                float3 cameraPosOS : TEXCOORD2;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            CBUFFER_START(UnityPerMaterial)               
                float4 _MainTex_ST;
                half4 _Color;
                half4 _EmittierColor;
                half4 _AlphaRemap;
            CBUFFER_END

            v2f vert(appdata input)
            {
                v2f o;
                o.color = input.color;
                // 顶点位置输入（VertexPositionInputs）包含多个空间（世界、视图、同质剪辑空间、ndc）中的位置。
                // Unity 编译器会删除所有未使用的引用（例如不使用视图空间）。
                // 因此，使用该结构可以在不增加额外成本的情况下提高灵活性。
                VertexPositionInputs vertexPositionInput = GetVertexPositionInputs(input.positionOS);
                o.positionCS = vertexPositionInput.positionCS;

                // 注意：这里【不能】按普通物体那样用顶点 o.positionCS.z 算雾因子（详见 frag 里 MixFog 处的说明）：
                // 贴花是用一个 cube 罩住目标区域、且 Cull Front 渲染到的是 cube 的【背面】，
                // 顶点插值出的雾因子对应用的是远端 cube 面的距离，比真正的被贴表面远得多 → 雾会明显偏浓。

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
                o.cameraPosOS = mul(ViewToObjectMatrix, float4(0,0,0,1)).xyz; //硬代码0或1可以实现许多编译器优化

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


                // convert unity cube's [-0.5,0.5] vertex pos range to [0,1] uv. Only works if you use a unity cube in mesh filter!
                float2 decalSpaceUV = decalSpaceScenePos.xy + 0.5;


                //丢弃“超出立方体体积”的像素
                float shouldClip = 0;
                //如果ZWrite处于关闭状态，则clip（）在移动设备上足够快，因为它不会写入DepthBuffer，因此GPU管道不会停滞（由ARM工作人员确认）。
                clip(0.5 - abs(decalSpaceScenePos) - shouldClip);

                // sample the decal texture
                float2 uv = decalSpaceUV.xy * _MainTex_ST.xy + _MainTex_ST.zw;//Texture tiling & offset

                half4 col = tex2D(_MainTex, uv);
#if _UseMaskMap
                     col=col.r*_AlphaRemap.x+col.g*_AlphaRemap.y+col.b*_AlphaRemap.z;
#endif
                col *= _Color*i.color*_EmittierColor;// tint color

                col.a = saturate(col.a);

                // unity的雾气效果：雾因子必须按【真实被贴表面】的深度算，不能用 cube 顶点算（原因见 vert）。
                // sceneDepthVS 是从 _CameraDepthTexture 重建出来的场景表面视空间深度，也就是贴花覆盖的那个
                // 像素的真实距离——和背景/地形算出来的雾完全一致，且省掉一次矩阵乘法。
                // 换算方式对齐 URP 官方片元雾（ShaderVariablesFunctions.hlsl 的 InitializeInputDataFog）：
                // 视空间深度以相机处为 0，重映射到近平面（减去 _ProjectionParams.y）。
                // 未开启雾关键字时 ComputeFogFactorZ0ToFar 返回 0、MixFog 原样返回，无需额外判断
                float fogFactor = ComputeFogFactorZ0ToFar(max(sceneDepthVS - _ProjectionParams.y, 0.0));
#if _DecalAdditiveFog
                // 加法式混合（例：PylonPower 的 _DecalSrcBlend=SrcAlpha / _DecalDstBlend=DstAlpha）
                // 下背景被 dst*dstAlpha 原样保留，而 MixFog 会把雾色混进输出 —— 等于在整块足迹上
                // 【额外多加一份雾色】：贴图全黑的地方本来什么都不加，开雾后却变成一整块雾色方块
                // （PylonPower 的贴图 pylon_alpha1_orange 四角纯黑但 alpha 恒为 1，正好整块方形都中招）。
                // 加法层对雾只能衰减：MixFog(c,f) - MixFog(0,f) 恒等于 c * 无雾强度，任何雾模式都成立。
                col.rgb = MixFog(col.rgb, fogFactor) - MixFog(half3(0, 0, 0), fogFactor);
#else
                col.rgb = MixFog(col.rgb, fogFactor);
#endif

                // 预乘 Alpha：确保透明区域完全不影响背景（保留背景雾效）
                col.rgb *= col.a;

                return col;
            }
            ENDHLSL
        }
    }
}
