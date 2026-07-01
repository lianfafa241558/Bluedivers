Shader "LX/SimpleFogAdditive"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10
        [Toggle(_UseLight)] _UseLight("Use Main Light", Float) = 0
        [Toggle(_UseFog)] _UseFog("Use Unity Fog", Float) = 1
    }

    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        #pragma shader_feature_local _UseLight
        #pragma shader_feature_local _UseFog
        #pragma multi_compile_fog
        #pragma multi_compile_instancing

        // 材质共享属性（不变）
        CBUFFER_START(UnityPerMaterial)
            sampler2D _MainTex;
            float4 _MainTex_ST;
        CBUFFER_END

        // 实例化属性（每个实例可不同）
        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
        UNITY_INSTANCING_BUFFER_END(Props)

        struct a2v
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 color : TEXCOORD1;
            float fogFactor : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        v2f vert(a2v v)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);

            // 获取裁剪空间位置（URP 标准方法）
            VertexPositionInputs posInputs = GetVertexPositionInputs(v.vertex.xyz);
            o.positionCS = posInputs.positionCS;

            // 纹理坐标变换
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            o.color = v.color;

            // 仅在启用雾效且当前渲染管线支持雾模式时计算雾因子
        #if _UseFog && (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
            o.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
        #else
            o.fogFactor = 0.0;
        #endif

            return o;
        }

        half4 frag(v2f i) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(i);

            half4 col = tex2D(_MainTex, i.uv);
            col *= UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor) * i.color;

        #if _UseLight
            Light mainLight = GetMainLight();
            col.rgb *= mainLight.color;
        #endif

        #if _UseFog && (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
            col.rgb = MixFog(col.rgb, i.fogFactor);
            //col.a = MixFog(col.a, i.fogFactor); 
            col.a*= i.fogFactor;
        #endif

            return col;
        }
    ENDHLSL

    SubShader
    {
         Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }

        LOD 200
        ZWrite Off
        Cull Back
        Blend SrcAlpha [_DstBlend]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }

    Fallback "Texture"
}