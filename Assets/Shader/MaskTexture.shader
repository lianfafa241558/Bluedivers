Shader "LX/MaskTexture"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR]_Color("Color", Color) = (1,1,1)
        _AlphaMask("AlphaMask", 2D) = "white" {}
        _MaskColor("MaskColor", Color) = (1,1,1)
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        sampler2D _MainTex;
        float4 _MainTex_ST;
        float4 _Color;

        sampler2D _AlphaMask;
        float4 _AlphaMask_ST;
        float4 _MaskColor;

        struct a2v {
            float4 vertex : POSITION;
            float3 uv : TEXCOORD0;
        };

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vert(a2v v)
        {
            v2f o;
            o.vertex = GetVertexPositionInputs(v.vertex.xyz).positionCS;//urp写法
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            return o;
        }

        half4 frag(v2f i) : SV_Target
        {
            half4 col = tex2D(_MainTex, i.uv)*_Color;
            half3 mask = (tex2D(_AlphaMask, i.uv * _AlphaMask_ST.rg + _AlphaMask_ST.ba) * _MaskColor).rgb;
            col.a *= mask.r + mask.g + mask.b;
            //col.a = 0;
            //col.g = 0;
            //col.b = 0;
            return col; 
        }

    ENDHLSL

    SubShader {
        LOD 200// 越靠前的subshader的lod值应越大，但是没看懂是干嘛的
        Blend SrcAlpha One
        ZWrite Off // 深度不写入，透明度混合中都应关闭深度写入
        Cull Off // 不剔除  Cull Back 剔除背面（背向摄像机的面） Cull Front 剔除前面 （朝向摄像机的面）

        Pass{
            HLSLPROGRAM
                #pragma vertex vert  //定点着色器
                #pragma fragment frag    //片段着色器
            ENDHLSL
        }

    }Fallback "Diffuse"//备选着色器
}