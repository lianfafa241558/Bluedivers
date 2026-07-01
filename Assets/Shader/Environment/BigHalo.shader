Shader "Environment/BigHalo"
{
    Properties
    {
        _MainTex("远景纹理", 2D) = "white" {}
        [HDR]_BaseColor("颜色", Color) = (1,1,1,1)
        _MoveSpeed("_MoveSpeed", Range(-5,5)) = 0
         //_GlowWidth ("Glow Width", Range(0, 0.1)) = 0.03  // 发光宽度
    }

    SubShader
    {
        Tags 
        {
            "RenderType" = "Background"
            "Queue" = "Background+50"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100
        ZWrite Off
        Cull Back
        Blend SrcAlpha One 
        ZClip Off          // 关键：禁止视锥体自动裁剪远处物体
        //ZTest Always
        //ZTest Greater
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                //float3 normal : NORMAL;
                //float4 tangent : TANGENT; // 切线
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                sampler2D _MainTex;
                float4 _MainTex_ST;
                float4 _BaseColor;
                float _MoveSpeed;
            CBUFFER_END

            //float _GlowWidth;

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 关键：把位置固定在最远深度，不参与视锥体裁剪
                float4 pos = TransformObjectToHClip(input.positionOS.xyz);
                // 顶点沿法线外扩 = 描边轮廓
                //pos.xyz += input.normal * _GlowWidth;
                //pos.z = pos.w; // 强行放到最远深度
                
                output.positionHCS = pos;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                input.uv.y+=(_Time.x * _MoveSpeed);
                half4 col = tex2D(_MainTex, input.uv);


                half3 finalRGB = col.rgb * _BaseColor.rgb;

                return half4(finalRGB,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
