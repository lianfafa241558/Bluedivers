Shader "LX/MaskTexture"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR]_Color("Color", Color) = (1,1,1)
        _AlphaMask("AlphaMask", 2D) = "white" {}
        _MaskColor("MaskColor", Color) = (1,1,1)
        _MultiplyTex("Multiply Texture", 2D) = "white" {}
        _Cutoff("Mask Cutoff", Range(0, 3)) = 0.001
        
        [Space(15)] 
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 1
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        sampler2D _MainTex;
        float4 _MainTex_ST;
        float4 _Color;

        sampler2D _AlphaMask;
        float4 _AlphaMask_ST;
        float4 _MaskColor;

        sampler2D _MultiplyTex;
        float4 _MultiplyTex_ST;

        float _Cutoff;

        struct a2v {
            float4 vertex : POSITION;
            float3 uv : TEXCOORD0;
            float4 color : COLOR;
        };

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 color : TEXCOORD1;
        };

        v2f vert(a2v v)
        {
            v2f o;
            o.vertex = GetVertexPositionInputs(v.vertex.xyz).positionCS;//urp写法
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            o.color = v.color;
            return o;
        }

        half4 frag(v2f i) : SV_Target
        {
            half4 col = tex2D(_MainTex, i.uv)*_Color*i.color;
            // 只乘 rgb：让叠加贴图当"颜色/图案层"用；若想让它同时控透明度，补一句 col.a *= mulTex.a
            // 注意 uv 是在 _MainTex_ST 之上再套 _MultiplyTex_ST，所以这里的 Tiling 是相对主贴图的倍数，不是独立贴图坐标
            half4 mulTex = tex2D(_MultiplyTex, i.uv * _MultiplyTex_ST.xy + _MultiplyTex_ST.zw);
            col.rgb *= mulTex.rgb;

            half3 mask = (tex2D(_AlphaMask, i.uv * _AlphaMask_ST.rg + _AlphaMask_ST.ba) * _MaskColor).rgb;
            half maskValue = mask.r + mask.g + mask.b;
            // clip 在混合之前丢弃片元：不参与混合也不写深度，比"alpha 置 0 再混合"少一层 overdraw
            // 阈值别写死成 0，蒙版边缘有半黑过渡像素，写死会留下噪点或深色描边
            clip(maskValue - _Cutoff);
            col.a *= maskValue;
            return col; 
        }

    ENDHLSL

    SubShader {
        LOD 200// 越靠前的subshader的lod值应越大，但是没看懂是干嘛的
        Blend [_SrcBlend][_DstBlend]
        ZWrite Off // 深度不写入，透明度混合中都应关闭深度写入
        Cull Back // 不剔除  Cull Back 剔除背面（背向摄像机的面） Cull Front 剔除前面 （朝向摄像机的面）

        Pass{
            HLSLPROGRAM
                #pragma vertex vert  //定点着色器
                #pragma fragment frag    //片段着色器
            ENDHLSL
        }

    }Fallback "Diffuse"//备选着色器
}