Shader "LX/Texture2"
{
    Properties
    {
        
        _MainTex("Texture", 2D) = "white" {}
        
        [Toggle(_Transpose)]_Transpose("_Transpose", Int) = 0
        [HDR] _BaseColor("_BaseColor", Color) = (1,1,1,1)

        [HDR] _HitColor("_HitColor", Color) = (0,0,0,0)
        _RenderRef("_RenderRef",Int) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)]_ZTestMode("ZTestMode", Float) = 4
        [Enum(Off, 0, On, 1)]_ZWriteMode("ZWriteMode", float) = 1
        [Enum(Off, 0, Front, 1,Back,2)]_CullMode("_CullMode", float) = 2

        [Space(15)] 
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10
        [Space(15)] 
        [Toggle(_UseLight)]_UseLight("_UseLight", Float) = 0
        [Space(15)] 
        [Toggle(_UseFresnel)]_UseFresnel("_UseFresnel", Float) = 0
        _FresnelScale("_FresnelScale", Range(0,1)) = 0.5
        _FresnelDecay("_FresnelDecay", Range(0,30)) = 1
        
        _FresnelWave("_FresnelWave", Range(0,0.5)) = 0
        _FresnelWaveTime("_FresnelWaveTime", Range(0,5)) = 5
        [Space(15)] 
        [Toggle(_UseMoveST)]_UseMoveST("_UseMoveST", Float) = 0

        [Toggle]_MoveX("_MoveX", Float) = 0
        _MoveSpeed("_MoveSpeed", Float) = 0
        _MoveOtherSpeed("_MoveOtherSpeed",Float) = 0

        _TimeScale("_TimeScale",Float) = 1

        [Space(15)] 
        [Toggle(_MY_FOG_ENABLE)] _MY_FOG_ENABLE("_UnityFogEnable", Float) = 1
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #pragma shader_feature_local _UseLight
        #pragma multi_compile_fog
        #pragma shader_feature_local _MY_FOG_ENABLE
        #pragma shader_feature_local _Transpose
        #pragma shader_feature_local _UseFresnel
        #pragma shader_feature_local _UseMoveST
        #pragma multi_compile_instancing

        
        //缓存减少重复使用(方便合并)
        CBUFFER_START(UnityPerMaterial)//基础共享参数
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _FresnelScale;
            float _FresnelDecay;
            float _FresnelWave;
            float _FresnelWaveTime;

            float _MoveX;
            float _MoveSpeed;
            float _MoveOtherSpeed;

            float _TimeScale;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(Props)//变化实例参数
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _HitColor)
        UNITY_INSTANCING_BUFFER_END(Props)


    //////////////////////////////////////////////////////////////////////////////////
    //光线结构由URP提供，用于抽象光线着色器变量。
    //它包含光的
    //half3   direction; // 方向
    //half3   color; // 颜色&强度
    //half    distanceAttenuation; // 距离衰减
    //half    shadowAttenuation; // 阴影衰减

    // URP根据光线和平台采取不同的明暗处理方法。
    //永远不要在着色器中引用灯光着色器变量，而是使用
    // -GetMainLight()
    // -GetLight()
    //函数填充这个光结构。
    //struct VertexPositionInputs
    //{
    //    float4 positionCS; // 顶点在裁剪空间的位置
    //    float4 positionWS; // 顶点在世界空间的位置
    //    float4 positionVS; // 顶点在视图空间的位置
    //    float3 viewDir;    // 从视图位置到顶点的方向
    //    // 其他可能的属性
    //};
    //////////////////////////////////////////////////////////////////////////////////



        struct a2v {
            float4 vertex : POSITION;
            float3 uv : TEXCOORD0;
            float4 color : COLOR;
            float3 normal : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 color : TEXCOORD1;

            float3 positionWS : TEXCOORD2;
            float3 normalWS : TEXCOORD3;

            float fogFactor : TEXCOORD4;

            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        v2f vert(a2v v)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            // 获取顶点在裁剪空间的位置
            VertexPositionInputs posInputs = GetVertexPositionInputs(v.vertex.xyz);
    
            o.positionCS = posInputs.positionCS;//urp写法
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        #if _Transpose
            o.uv = float2(o.uv.y,o.uv.x); 
        #endif
            o.color = v.color;
            //o.worldPos = mul(unity_ObjectToWorld, v.vertex);//旧版写法
            //o.positionWS = GetVertexPositionInputs(v.vertex.xyz).positionWS;//Urp写法
        #if _UseFresnel
             //o.positionWS = TransformObjectToWorld(v.vertex);
            o.positionWS = posInputs.positionWS;
            //TransformObjectToWorld(v.positionOS);//不知道那版的写法
            o.normalWS = TransformObjectToWorldNormal(v.normal);
        #endif
        #if _MY_FOG_ENABLE
            o.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
        #endif

            return o;
        }

        half4 frag(v2f i) : SV_Target
        { 
            UNITY_SETUP_INSTANCE_ID(i);
            #if _UseMoveST
                if(_MoveX){
                    i.uv.x+=(_Time.x * _MoveSpeed*_MainTex_ST.x/_TimeScale);
                    i.uv.y+=(_Time.x * _MoveOtherSpeed*_MainTex_ST.y/_TimeScale);
                }
                else{
                    i.uv.y+=(_Time.x * _MoveSpeed*_MainTex_ST.y/_TimeScale);
                    i.uv.x+=(_Time.x * _MoveOtherSpeed*_MainTex_ST.x/_TimeScale);
                }
            #endif
            half4 col = tex2D(_MainTex, i.uv);

            col*= (UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor)+UNITY_ACCESS_INSTANCED_PROP(Props, _HitColor))*i.color; 
        #if _UseLight
            Light mainLight = GetMainLight();
            col.rgb *= mainLight.color;
        #endif
            
        #if _UseFresnel
            Light mainLight = GetMainLight();
            half3 N = i.normalWS;
            half3 L = normalize(_WorldSpaceCameraPos - i.positionWS);
            half NdotL =saturate(dot(N,L));


            if (NdotL > 0) {
                col.a +=  pow(1 - NdotL,_FresnelDecay)*_FresnelScale;
                //col.a /=(_BaseColor.r+_BaseColor.g+_BaseColor.b)/3;
            }

                

            if(_FresnelWave){
                col.a += sin(_Time.y)*_FresnelWave;
            }
        #endif
        #if _MY_FOG_ENABLE && (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
            col.rgb = MixFog(col.rgb, i.fogFactor);
            //col.rgb =  i.fogFactor;
        #endif

            return col;
        }

    ENDHLSL

    SubShader {
        LOD 200// 越靠前的subshader的lod值应越大，但是没看懂是干嘛的

        ZWrite [_ZWriteMode] // 深度不写入，透明度混合中都应关闭深度写入
        Cull [_CullMode] // 不剔除  Cull Back 剔除背面（背向摄像机的面） Cull Front 剔除前面 （朝向摄像机的面）
        Blend[_SrcBlend][_DstBlend]
        ZTest[_ZTestMode]
        //Blend SrcAlpha OneMinusSrcAlpha //传统透明
        //Blend One OneMinusSrcAlpha// 倍增透明度
        //Blend One One
        //Blend OneMinusDstColor One // 弱添加
        //Blend DstColor Zero // 乘法(剔除白色)
        //Blend DstColor SrcColor // 2x 乘法

        Pass{
             Stencil{
                Ref[_RenderRef]
                //Comp Always
                //Pass Replace
            }
            HLSLPROGRAM
                #pragma vertex vert  //定点着色器
                #pragma fragment frag    //片段着色器

            ENDHLSL
        }

    }Fallback "Texture"//备选着色器
}