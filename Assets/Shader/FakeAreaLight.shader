Shader "LX/FakeAreaLight"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _AlphaTex("_AlphaTex", 2D) = "white" {}
        [HDR]_Color("Color", Color) = (1,1,1)
        [HDR]_EmissionColor("_EmissionColor", Color) = (1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10

        _Noise ("Noise", 2D) = "white" {}
        _distortFactorTime("FactorTime",Range(0,100)) = 0.5
        _distortFactor("factor",Range(0,1)) = 0

        [Space(16)]
        _FresnelScale("_FresnelScale", Range(0,1)) = 0.5
        _FresnelDecay("_FresnelDecay", Range(0,30)) = 1

    }
    HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"



        
        
        //缓存减少重复使用(方便合并)
        CBUFFER_START(UnityPerMaterial)//基础共享参数

            float _FresnelScale;
            float _FresnelDecay;
            float _FresnelWave;
            float _FresnelWaveTime;

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _AlphaTex;
            //float4 _MainTex_ST;

            float4 _Color;
            float4 _EmissionColor;
            sampler2D _Noise;
            float4 _Noise_ST;
            half _distortFactorTime;
            half _distortFactor;

        CBUFFER_END


        //大致就是sin函数倍增之后加上取小数的frac函数可以近似得到一种随机数的效果吧。
        float random(float2 uv)
        {
            return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
        }

        struct a2v {
            float4 vertex : POSITION;
            float3 uv : TEXCOORD0;
            float3 uv2 : TEXCOORD1;
            half4 color : COLOR;//传递image的颜色用
            float3 normal : NORMAL;
        };

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float2 uv : TEXCOORD0;
            float2 uv2 : TEXCOORD1;
            half4 color : COLOR;//传递image的颜色用
            float3 positionWS : TEXCOORD2;
            float3 normalWS : TEXCOORD3;
            float fogFactor : TEXCOORD4;
        };

        v2f vert(a2v v)
        {
            v2f o;
            VertexPositionInputs posInputs = GetVertexPositionInputs(v.vertex.xyz);
            o.vertex = posInputs.positionCS;//urp写法
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            o.uv2 = o.uv;
            o.color=v.color;

            o.positionWS = posInputs.positionWS;
            o.normalWS = TransformObjectToWorldNormal(v.normal);
            o.fogFactor =ComputeFogFactor(posInputs.positionCS.z);
            return o;
        }

        half4 frag(v2f i) : SV_Target
        {
            half a= tex2D(_AlphaTex, i.uv/_MainTex_ST.xy-_MainTex_ST.zw).r;
            //屏幕位置扭动
            half dragOffset = sin(_Time.y * _distortFactorTime);
            i.uv2.y+=_Time.y*_distortFactor;
            i.uv.x = i.uv.x +tex2D(_Noise, i.uv2*_Noise_ST.xy+_Noise_ST.zw).r * dragOffset*_distortFactor;
            //接下来用这个偏移后的uv坐标来对纹理进行采样。
            half4 col = tex2D(_MainTex, i.uv) * _Color*i.color*_EmissionColor;


            Light mainLight = GetMainLight();
            half3 N =  normalize(i.normalWS);//法线方向(不是很清楚是否必须标准化
            half3 V = normalize(_WorldSpaceCameraPos - i.positionWS);//L是灯光方向，这里其实是V
            half NdotV  =saturate(abs(dot(N,V)));

            // 标准菲涅尔公式
            half fresnel = pow(1.0 - NdotV, _FresnelDecay);
            col.a=lerp(col.a,col.a * fresnel,_FresnelScale);

            if(_FresnelWave){
                col.a += sin(_Time.y)*_FresnelWave;
            }

        #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
            //col.rgb = MixFog(col.rgb,i.fogFactor);
            col.a *= saturate(1-i.fogFactor);
            //col.rgb = 0;
            //col.a = 0;
        #endif
            col.a*=a;
            return col;
        }

    ENDHLSL

    SubShader {
        LOD 200// 越靠前的subshader的lod值应越大，但是没看懂是干嘛的
        Blend[_SrcBlend][_DstBlend]
        ZWrite Off // 深度不写入，透明度混合中都应关闭深度写入
        Cull Off // 不剔除  Cull Back 剔除背面（背向摄像机的面） Cull Front 剔除前面 （朝向摄像机的面）

        Pass{
            HLSLPROGRAM
                #pragma vertex vert  //定点着色器
                #pragma fragment frag    //片段着色器
                #pragma multi_compile_fog
            ENDHLSL
        }

    }Fallback "Diffuse"//备选着色器
}