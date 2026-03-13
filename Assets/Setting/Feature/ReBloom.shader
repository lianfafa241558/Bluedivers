Shader "Feature/ReBloom"
{
    Properties
    {

        [HDR] _MainTex("Base (RGB)", 2D) = "white" {}
        [HDR]_Color("Color", Color) = (1,1,1,1)

        //[Space(20)]
        //_StripeColor("StripeColor", Color) = (0.8, 0.8, 0.8, 1)//条纹颜色
        //_StripeWidth("StripeWidth", Range(1, 10)) = 4


    }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        sampler2D _MainTex;
        half4 _MainTex_ST;
        half4 _Color;


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

          //half4 hdrColor = SAMPLE_TEXTURE2D(_MainTex, _MainTex_ST,i.uv);
            //return hdrColor;  

            // 提取亮度（HDR范围）
            //float luminance = 0.2126 * hdrColor.r + 0.7152 * hdrColor.g + 0.0722 * hdrColor.b;
     

        /*
            half3 color = SAMPLE_TEXTURE2D(_Source, sampler_Source, i.uv).rgb; 
            //half3 color = tex2D(_MainTex, i.uv);
            if (color.r > 1.0 || color.g > 1.0 || color.b > 1.0)
                {

                    // 如果大于1，则将其设置为原本颜色
                    return half4(color, 1); // 或者根据需要进行其他处理
                }
                
                */
            
           // float brightness = 0.2126 * color.r + 0.7152 * color.g + 0.0722 * color.b * _Color * i.color;
            /*
            if(brightness<0.5){
                //discard;
                color=half3(0,0,0);
                return half4(color, 1);
            }*/
            return half4(0,0,0, 1);
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