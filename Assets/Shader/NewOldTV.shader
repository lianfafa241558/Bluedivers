Shader "LX/FeatureOldTV"
{
    Properties
    {

        _MainTex("Base (RGB)", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Scale("Scale ", Range(0.0, 1.0)) = 1.0//全局系数
        [Space(20)]
        _Expand("screen ", Range(0.0, 1.0)) = 1.0//屏幕扭曲程度
        _Luminosity("_Luminosity ", Range(0.0, 1.0)) = 1.0//黑白化的系数
        _Light("_Light ", Range(0.0, 10.0)) = 1.0//亮度的系数

        _NoiseIntensity("_NoiseIntensity", Range(0, 1)) = 1//噪声亮度
        [Space(20)]
        _StripeColor("StripeColor", Color) = (0.8, 0.8, 0.8, 1)//条纹颜色
        _StripeWidth("StripeWidth", Range(1, 10)) = 4

        [Space(20)]
        _DragTex("DragTex", 2D) = "white" {}
        _DragStrength("DragStrength", Range(-0.08, 0.08)) = 0.05

        [Space(20)]
        [Toggle(_UseFlowLight)]_UseFlowLight("_UseFlowLight", Float) = 0
        _FlowLightSpeed("FlowLightSpeed", Range(0.001, 5)) = 1
        _FlowLightTex("FlowLightTex", 2D) = "white" {}

        [Space(20)]
        [Toggle(_UseBar)]_UseBar("_UseBar", Float) = 0
        _Top1("Top1", Range(0, 1)) = 0
        _Down1("Down1", Range(0, 1)) = 0
        _Top2("Top2", Range(0, 1)) = 0
        _Down2("Down2", Range(0, 1)) = 0


    }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        sampler2D _MainTex;
        half4 _MainTex_ST;
        half4 _Color;

        half _Scale;
        half _Expand;
        half _NoiseIntensity;
        half _Luminosity;
        half _Light;


        half4 _StripeColor;
        half _StripeWidth;
        sampler2D _DragTex;
        half _DragStrength;

        bool _UseFlowLight;
        half _FlowLightSpeed;
        sampler2D _FlowLightTex;

        bool _UseBar;
        half _Top1;
        half _Down1;
        half _Top2;
        half _Down2;

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
            half2 uv2           : TEXCOORD3;
            half4 color         : TEXCOORD2;//传递image的颜色用
        };


        v2f vert(a2v v) {
            v2f o;
            o.vertex = GetVertexPositionInputs(v.vertex.xyz).positionCS;//urp写法
            o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
            o.color = v.color;
            //我们采用的方法是先计算该uv坐标离(0.5,0.5)的距离，这个(0.5,0.5)的uv坐标其实可以看成屏幕的中心点。
            //计算出距离之后根据这个距离的大小来对uv进行偏移，这其中用到了一个参数_Expand 为偏移的距离。
            float d2 = dot(o.uv - half2(0.5, 0.5), o.uv - half2(0.5, 0.5));
            o.uv2 = (o.uv - half2(0.5, 0.5)) * (_Expand + d2 * (1 - _Expand)) + half2(0.5, 0.5);
            return o;
        }

        half4 frag(v2f i) : SV_Target
        {

            //屏幕位置扭动
            half dragOffset = (_Time.y % _Expand * _Scale);
            if (dragOffset > 0 && dragOffset < 1) {
                i.uv2.x = i.uv2.x + random(i.uv2.y * _Time.y) * _DragStrength * _Scale * tex2D(_DragTex, i.uv2).r * dragOffset;
            }
            //接下来用这个偏移后的uv坐标来对纹理进行采样。
            half3 color = tex2D(_MainTex, i.uv2) * _Color * i.color *(1-(1-_Light)* _Scale);
            half alpha = _Color.a* i.color.a;
           
            
            //黑白化
            half lumScale = _Luminosity * _Scale;
            float luminosity = (0.299 * color.r + 0.587 * color.g + 0.114 * color.b)* lumScale;

            color = half3(color.r * (1- lumScale)+ luminosity,color.g * (1 - lumScale) + luminosity,color.b * (1 - lumScale) + luminosity);

            //接下来添加噪声，我们用一个变量控制噪声强度，同时传入时间变量来得到噪声一直变化的效果。

            _NoiseIntensity = _NoiseIntensity * 0.15* _Scale;
            //float n = random(i.uv2.xy * _Time.x);
            float2 c = i.uv2.xy;
            c.x = round(c.x * 100) * 0.01 * _Time.x;
            c.y = round(c.y * 333) * 0.003 * _Time.x;
            float n = random(c);
            half3 result = color * (1 - _NoiseIntensity) + _NoiseIntensity * n;

            //随机条纹
            _StripeWidth = _StripeWidth * 0.02;
            float heigth = fmod(1 - i.uv.y + _Time.y * 0.06 + random(i.uv.y * _Time.y) * 0.005, 1);
            //fmod取模(暗部)
            heigth = fmod(heigth, _StripeWidth);
            if (heigth > _StripeWidth * 0.6) {
                result = result * (1-_StripeColor* _Scale);
            }

            //#if那个是要求游戏中不变的
            if (_UseBar) {
                //写在WndManager里面了，这个实在没办法靠纯shader实现
                //随机的大规模白色条
                if (i.uv.y > _Down1 && i.uv.y < _Top1) {
                    result = result * (1 + 4 * _Scale);
                }
                //随机的大规模白色条
                else if (i.uv.y > _Down2 && i.uv.y < _Top2) {
                    result = result * (1 + 4 * _Scale);
                }
            }

            if (_UseFlowLight) {
                //每次波动的时间↓_FlowLightSpeed
                half flowLightOffset = (_Time.y * _FlowLightSpeed % 3);
                if (flowLightOffset < 1)
                {
                    //+1-flowLightOffset是往上
                    float4 flowLightColor = tex2D(_FlowLightTex, float2(i.uv.x, i.uv.y /* + 1*/ + flowLightOffset));
                    result.rgb = result.rgb + flowLightColor.rgb * _Scale;//流光
                }
            }
            return half4(result, alpha);
        }

            ENDHLSL

            SubShader {
                LOD 200// 越靠前的subshader的lod值应越大，但是没看懂是干嘛的

                ZWrite Off // 深度不写入，透明度混合中都应关闭深度写入
                Cull Back // 不剔除  Cull Back 剔除背面（背向摄像机的面） Cull Front 剔除前面 （朝向摄像机的面）
                //Fog{ Mode Off }//没用，这里的东西是先用相机捕获的
                Pass{
                    HLSLPROGRAM
                        #pragma vertex vert  //定点着色器
                        #pragma fragment frag    //片段着色器
                    ENDHLSL
            }

        }Fallback "Diffuse"//备选着色器
}