Shader "Environment/SkyboxLayer"
{
    Properties
    {
        _MainTex("远景纹理", 2D) = "white" {}
        [HDR]_Color("颜色", Color) = (1,1,1,1)
        _Alpha ("_Alpha", Range(0, 1)) = 1  //透明度
        [HDR]_EmissionColor("发光颜色", Color) = (1,1,1,1)

        [Space(15)] 
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10
    }

    SubShader
    {
        // 天空层（星星/月亮/云）队列必须留在透明段：RenderQueueRange.transparent 才能筛到它们，
        // 且材质自定义队列（星星/月亮 3000、云 3500）继续决定它们之间的先后
        Tags 
        {
            "RenderType" = "Background"
            //"Queue" = "Background+50"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100
        ZWrite Off
        Cull Back
        Blend [_SrcBlend] [_DstBlend]
        ZClip Off          // 关键：禁止视锥体自动裁剪远处物体
        //ZTest Always
        //ZTest Greater
        // 该 Pass 不再由 URP 默认的透明 Pass 绘制（LightMode 是自定义标签），
        // 而是在"天空盒之后、全屏雾之前"的 445 由
        // Assets/Scripts/Rendering/SkyboxLayerBeforeFogRendererFeature.cs 单独画一遍：
        // 早于 450 才能吃到全屏雾，晚于天空盒(400) 才不会被天空盒以相同深度整片擦掉。
        // 注意：把该 Renderer Feature 去掉后天空层将完全不显示（没有默认 Pass 兜底）。
        Pass
        {
            Tags { "LightMode" = "SkyboxLayerBeforeFog" }

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
                float4 _Color;
                float _Alpha;
                float4 _EmissionColor;
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
                half4 col = tex2D(_MainTex, input.uv);
                half3 finalRGB = col.rgb * _Color.rgb;
                return half4(finalRGB,_Alpha*col.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
