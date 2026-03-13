Shader "LX/Holography"//全息投影
{
    Properties
    {

        _MainTex("Base (RGB)", 2D) = "white" {}
        [HDR]_Color("Color", Color) = (1,1,1,1)

        [Space(20)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10
        [Space(20)]
        _NoiseIntensity("_NoiseIntensity", Range(0, 1)) = 1//噪声亮度
        [Space(20)]
        _StripeColor("StripeColor", Color) = (0.8, 0.8, 0.8, 1)//条纹颜色
        _StripeWidth("StripeWidth", Range(1, 10)) = 4

    }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        sampler2D _MainTex;
        half4 _MainTex_ST;
        half4 _Color;

        half _NoiseIntensity;


        half4 _StripeColor;
        half _StripeWidth;


        //大致就是sin函数倍增之后加上取小数的frac函数可以近似得到一种随机数的效果吧。
        float random(float2 uv)
        {
            return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
        }

        struct a2v {
            half4 vertex   : POSITION;
            half4 texcoord : TEXCOORD0;
            half4 color : COLOR;//传递image的颜色用
        };


        struct v2f
        {
            half4 vertex       : SV_POSITION;
            half2 uv            : TEXCOORD0;
            half4 color         : TEXCOORD2;//传递image的颜色用
        };


        v2f vert(a2v v) {
            v2f o;
            o.vertex = GetVertexPositionInputs(v.vertex.xyz).positionCS;//urp写法
            o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
            o.color = v.color;

            return o;
        }

        half4 frag(v2f i) : SV_Target
        {

            half3 color = tex2D(_MainTex, i.uv) * _Color * i.color;
            //half alpha = _Color.a* i.color.a;
            half alpha =1;
            //接下来添加噪声，我们用一个变量控制噪声强度，同时传入时间变量来得到噪声一直变化的效果。
            
            _NoiseIntensity = _NoiseIntensity * 0.15;
            float2 c = i.uv.xy;
            c.x = round(c.x * 100) * 0.01 * _Time.x;
            c.y = round(c.y * 333) * 0.003 * _Time.x;
            float n = random(c);
            half3 result = color * (1 - _NoiseIntensity) + _NoiseIntensity * n;

            //随机条纹
            _StripeWidth = _StripeWidth * 0.02;
            float heigth = fmod(1 - i.uv.y + _Time.y * 0.06 + random(i.uv.y * _Time.y) * 0.001, 1);
            //fmod取模(暗部)
            heigth = fmod(heigth, _StripeWidth);
            if (heigth > _StripeWidth * 0.6) {
                result = result * (1-_StripeColor);
                //alpha = alpha * (1-_StripeColor);
            }

            
            return half4(result, alpha);
        }

            ENDHLSL

            SubShader {
                LOD 200// 越靠前的subshader的lod值应越大，但是没看懂是干嘛的
                Blend[_SrcBlend][_DstBlend]
                ZWrite Off // 深度不写入，透明度混合中都应关闭深度写入
                Cull Off 
                Pass{
                    HLSLPROGRAM
                        #pragma vertex vert 
                        #pragma fragment frag
                    ENDHLSL
            }

        }Fallback "Diffuse"//备选着色器
}