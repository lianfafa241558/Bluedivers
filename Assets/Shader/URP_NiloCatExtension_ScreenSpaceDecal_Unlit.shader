// see README here: 
// github.com/ColinLeung-NiloCat/UnityURPUnlitScreenSpaceDecalShader

Shader "Screen Space Decal"
{
    Properties
    {

        [Header(Basic)]
        [MainTexture]_MainTex("Texture", 2D) = "white" {}
        [MainColor][HDR]_Color("_Color (default = 1,1,1,1)", Color) = (1,1,1,1)

        [Header(Blending)]
        // https://docs.unity3d.com/ScriptReference/Rendering.BlendMode.html
        [Enum(UnityEngine.Rendering.BlendMode)]_DecalSrcBlend("_DecalSrcBlend", Int) = 5 // 5 = SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)]_DecalDstBlend("_DecalDstBlend", Int) = 10 // 10 = OneMinusSrcAlpha

        [Header(Alpha remap(extra alpha control))]
        _AlphaRemap("_AlphaRemap", vector) = (1,0,0,0)

        [Toggle(_UseMaskMap)] _UseMaskMap("_UseMaskMap", Float) = 0

        //防止侧面拉伸（将投影方向与场景法线进行比较，必要时丢弃）
        [Header(Prevent Side Stretching(Compare projection direction with scene normal and Discard if needed))]
        [Toggle(_ProjectionAngleDiscardEnable)] _ProjectionAngleDiscardEnable("_ProjectionAngleDiscardEnable", float) = 0//投影角度丢弃（指背面
        _ProjectionAngleDiscardThreshold("_ProjectionAngleDiscardThreshold (default = 0)", range(-1,1)) = 0

        //让a通道*rgb
        [Header(Mul alpha to rgb)]
        [Toggle]_MulAlphaToRGB("_MulAlphaToRGB", Float) = 0

        //忽略纹理的环绕设置
        [Header(Ignore texture wrap mode setting)]
        [Toggle(_FracUVEnable)] _FracUVEnable("_FracUVEnable", Float) = 0


        //====================================== below = usually can ignore in normal use case =====================================================================
        [Header(Stencil Masking)]
        // https://docs.unity3d.com/ScriptReference/Rendering.CompareFunction.html
        _StencilRef("_StencilRef", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)]_StencilComp("_StencilComp", Float) = 0 //0 = disable
        [Enum(UnityEngine.Rendering.CompareFunction)]_ZTest("_ZTest", Float) = 0 //0 = disable
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("_Cull", Float) = 1 //1 = Front
        [Toggle(_UnityFogEnable)] _UnityFogEnable("_UnityFogEnable", Float) = 1
        //支持正射相机
        [Header(Support Orthographic camera)]
        [Toggle(_SupportOrthographicCamera)] _SupportOrthographicCamera("_SupportOrthographicCamera (default = off)", Float) = 0
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
            ZTest[_ZTest]

            ZWrite off
            Blend[_DecalSrcBlend][_DecalDstBlend]

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            // make fog work
            #pragma multi_compile_fog

            // due to using ddx() & ddy()
            #pragma target 3.0

            #pragma shader_feature_local_fragment _ProjectionAngleDiscardEnable
            #pragma shader_feature_local _UnityFogEnable
            #pragma shader_feature_local_fragment _FracUVEnable
            #pragma shader_feature_local_fragment _SupportOrthographicCamera

            // Required by all Universal Render Pipeline shaders.
            // It will include Unity built-in shader variables (except the lighting variables)
            // (https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html
            // It will also include many utilitary functions. 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 viewRayOS : TEXCOORD1; // xyz: viewRayOS, w: extra copy of positionVS.z 
                float4 cameraPosOSAndFogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            CBUFFER_START(UnityPerMaterial)//基础共享参数          
                float4 _MainTex_ST;
                float _ProjectionAngleDiscardThreshold;
                half _MulAlphaToRGB;
                /*
                half4 _Color;
                half4 _AlphaRemap;
                */
                float _UseMaskMap;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)//变化参数
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AlphaRemap)

            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert(appdata input)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                // VertexPositionInputs contains position in multiple spaces (world, view, homogeneous clip space, ndc)
                // Unity compiler will strip all unused references (say you don't use view space).
                // Therefore there is more flexibility at no additional cost with this struct.
                // 顶点位置输入（VertexPositionInputs）包含多个空间（世界、视图、同质剪辑空间、ndc）中的位置。
                // Unity 编译器会删除所有未使用的引用（例如不使用视图空间）。
                // 因此，使用该结构可以在不增加额外成本的情况下提高灵活性。
                VertexPositionInputs vertexPositionInput = GetVertexPositionInputs(input.positionOS);

                o.positionCS = vertexPositionInput.positionCS;

                // regular unity fog
#if _UnityFogEnable
                o.cameraPosOSAndFogFactor.a = ComputeFogFactor(o.positionCS.z);
#else
                o.cameraPosOSAndFogFactor.a = 0;
#endif

                // prepare depth texture's screen space UV
                // 准备深度纹理的屏幕空间 UV
                o.screenPos = ComputeScreenPos(o.positionCS);

                // get "camera to vertex" ray in View space
                // 获取视图空间中 “摄像机到顶点 ”的射线
                float3 viewRay = vertexPositionInput.positionVS;

                // [important note]
                //=========================================================
                // "viewRay z division" must do in the fragment shader, not vertex shader! (due to rasteriazation varying interpolation's perspective correction)
                // We skip the "viewRay z division" in vertex shader for now, and store the division value into varying o.viewRayOS.w first, 
                // we will do the division later when we enter fragment shader
                // viewRay /= viewRay.z; //skip the "viewRay z division" in vertex shader for now
                o.viewRayOS.w = viewRay.z;//store the division value to varying o.viewRayOS.w
                //=========================================================

                // unity's camera space is right hand coord(negativeZ pointing into screen), we want positive z ray in fragment shader, so negate it
                viewRay *= -1;

                // it is ok to write very expensive code in decal's vertex shader, 
                // it is just a unity cube(4*6 vertices) per decal only, won't affect GPU performance at all.
                float4x4 ViewToObjectMatrix = mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V);

                // transform everything to object space(decal space) in vertex shader first, so we can skip all matrix mul() in fragment shader
                o.viewRayOS.xyz = mul((float3x3)ViewToObjectMatrix, viewRay);
                o.cameraPosOSAndFogFactor.xyz = mul(ViewToObjectMatrix, float4(0,0,0,1)).xyz; // hard code 0 or 1 can enable many compiler optimization

                return o;
            }

            // copied from URP12.1.2's ShaderVariablesFunctions.hlsl
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
                UNITY_SETUP_INSTANCE_ID(i);
                // [important note]
                //========================================================================
                // now do "viewRay z division" that we skipped in vertex shader earlier.
                i.viewRayOS.xyz /= i.viewRayOS.w;
                //========================================================================

                float2 screenSpaceUV = i.screenPos.xy / i.screenPos.w;
                float sceneRawDepth = tex2D(_CameraDepthTexture, screenSpaceUV).r;

                float3 decalSpaceScenePos;

#if _SupportOrthographicCamera
                // we have to support both orthographic and perspective camera projection
                // static uniform branch depends on unity_OrthoParams.w
                // (should we use UNITY_BRANCH here?) decided NO because https://forum.unity.com/threads/correct-use-of-unity_branch.476804/
                if(unity_OrthoParams.w)
                {
                    float sceneDepthVS = LinearDepthToEyeDepth(sceneRawDepth);

                    //***Used a few lines from Asset: Lux URP Essentials by forst***
                    // Edit: The copied Lux URP stopped working at some point, and no one even knew why it worked in the first place 
                    //----------------------------------------------------------------------------
				    float2 viewRayEndPosVS_xy = float2(unity_OrthoParams.xy * (i.screenPos.xy - 0.5) * 2 /* to clip space */);  // Ortho near/far plane xy pos 
				    float4 vposOrtho = float4(viewRayEndPosVS_xy, -sceneDepthVS, 1);                                            // Constructing a view space pos
				    float3 wposOrtho = mul(UNITY_MATRIX_I_V, vposOrtho).xyz;                                                 // Trans. view space to world space
                    //----------------------------------------------------------------------------

                    // transform world to object space(decal space)
                    decalSpaceScenePos = mul(GetWorldToObjectMatrix(), float4(wposOrtho, 1)).xyz;
                }
                else
                {
#endif
                    // if perspective camera, LinearEyeDepth will handle everything for user
                    // remember we can't use LinearEyeDepth for orthographic camera!
                    float sceneDepthVS = LinearEyeDepth(sceneRawDepth,_ZBufferParams);

                    // scene depth in any space = rayStartPos + rayDir * rayLength
                    // here all data in ObjectSpace(OS) or DecalSpace
                    // be careful, viewRayOS is not a unit vector, so don't normalize it, it is a direction vector which view space z's length is 1
                    decalSpaceScenePos = i.cameraPosOSAndFogFactor.xyz + i.viewRayOS.xyz * sceneDepthVS;
                    
#if _SupportOrthographicCamera
                }
#endif

                // convert unity cube's [-0.5,0.5] vertex pos range to [0,1] uv. Only works if you use a unity cube in mesh filter!
                float2 decalSpaceUV = decalSpaceScenePos.xy + 0.5;

                // discard logic
                //===================================================
                // discard "out of cube volume" pixels
                float shouldClip = 0;
#if _ProjectionAngleDiscardEnable
                // also discard "scene normal not facing decal projector direction" pixels
                float3 decalSpaceHardNormal = normalize(cross(ddx(decalSpaceScenePos), ddy(decalSpaceScenePos)));//reconstruct scene hard normal using scene pos ddx&ddy

                // compare scene hard normal with decal projector's dir, decalSpaceHardNormal.z equals dot(decalForwardDir,sceneHardNormalDir)
                shouldClip = decalSpaceHardNormal.z > _ProjectionAngleDiscardThreshold ? 0 : 1;
#endif
                // call discard
                // if ZWrite is Off, clip() is fast enough on mobile, because it won't write the DepthBuffer, so no GPU pipeline stall(confirmed by ARM staff).
                clip(0.5 - abs(decalSpaceScenePos) - shouldClip);
                //===================================================

                // sample the decal texture
                float2 uv = decalSpaceUV.xy * _MainTex_ST.xy + _MainTex_ST.zw;//Texture tiling & offset
#if _FracUVEnable
                uv = frac(uv);// add frac to ignore texture wrap setting
#endif
                half4 col = tex2D(_MainTex, uv);
                if(_UseMaskMap){
                    half4 am= UNITY_ACCESS_INSTANCED_PROP(Props, _AlphaRemap);
                     col=col.r*am.x+col.g*am.y+col.b*am.z;
                }

                col *=  UNITY_ACCESS_INSTANCED_PROP(Props, _Color);// tint color
                //col.a = saturate(col.a * _AlphaRemap.x + _AlphaRemap.y);// alpha remap MAD
                col.a = saturate(col.a);
                col.rgb *= lerp(1, col.a,_MulAlphaToRGB);// extra multiply alpha to RGB

#if _UnityFogEnable
                // Mix the pixel color with fogColor. You can optionaly use MixFogColor to override the fogColor
                // with a custom one.
                col.rgb = MixFog(col.rgb, i.cameraPosOSAndFogFactor.a);
#endif
                return col;
            }
            ENDHLSL
        }
    }
}
