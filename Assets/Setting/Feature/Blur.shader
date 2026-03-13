Shader "LX/Blur"
//因为越简单越好，所以注释了不必要的功能
{
    Properties
    {
        /*[NoScaleOffset]*/_MainTex("Base (RGB)", 2D) = "white" {}
        /*[NoScaleOffset]*/_AddiveMaskTex("Addive (RGB)", 2D) = "black" {}
        _BlurSize("Blur Size", Range(0,20)) = 1.0
        _AddColor("AddiveColor", Color) = (1,1,1)
        _Monochrome("_Monochrome", Range(0,1)) = 0
    }

        HLSLINCLUDE
        //#include "UnityCG.cginc"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        sampler2D _MainTex;
        float4 _MainTex_ST;

        sampler2D _AddiveMaskTex;
        float4 _AddiveMaskTex_ST;
        float4 _AddColor;
        float _Monochrome;

        //这个是有意义的，不能删
        half4 _MainTex_TexelSize;
        float _BlurSize;
        //float:32位，最高精度
        //half：16位，中等精度
        //fixed:11位，低精度（hsls中好像不能用）
        //在PC基本没有区别，在旧版移动段可能有区别


        struct a2v {
            float4 vertex : POSITION;//好像是说为了跨平台方便最好使用SV_
            //float3 normal : NORMAL;
            //将模型的第一组纹理坐标存储到该变量中
            float3 texcoord : TEXCOORD0;
            half4 color : COLOR;
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            //float3 worldPos : TEXCOORD0;
            //float3 worldNormal : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half4 color : COLOR;
        };

        v2f vert(a2v v) {
            v2f o;
            //o.pos = UnityObjectToClipPos(v.vertex);原版写法
            o.pos = GetVertexPositionInputs(v.vertex.xyz).positionCS;//urp写法
            // 模型坐标顶点转换世界坐标顶点
            //o.worldPos = mul(unity_ObjectToWorld, v.vertex);
            // 模型坐标法线转换世界坐标法线
            //o.worldNormal = UnityObjectToWorldNormal(v.normal);
            // 对顶点纹理坐标进行变换，最终得到uv坐标。
            // 方法原理 o.uv = v.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            //_MainTex_ST 是纹理的属性值，写法是固定的为 纹理名+_ST(就是后面那个4个参数填缩放和偏移的)
            //o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);//但是这里不需要进行处理缩放啥的
            o.uv = v.texcoord;
            o.color = v.color;
            return o;
        }

        half4 frag(v2f i) : SV_Target
        {
            /*
            //fixed3 worldNormal = normalize(i.worldNormal);
            //fixed3 worldLightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
            //half4 texColor = tex2D(_MainTex, i.uv);
            //saturate(alpha)←吧alpha这个变量限定在0-1之间
            //透明度测试，如果小于设定的_Cutoff透明度阈值，则会抛弃当前的片元
            //if (1-texColor.r< 0.3)discard;
            //half3 diffuse = texColor.rgb;// * max(0, dot(worldNormal, worldLightDir));
            //return half4(diffuse,1)
            
            float weight[5] = {0.1621, 0.0983, 0.0219,0.0133,0.0030};
            float mask =0;


                for (int u = -2; u <= 2; u++) {
                    for (int v = -2; v <= 2; v++) {
                        mask += tex2D(_MainTex, i.uv + _BlurSize * float2(u,v) * _MainTex_TexelSize.xy).r * weight[abs(u)+ abs(v)];
                    }
                }

                //mask = smoothstep(0, 1, mask);
                if (mask > 0.7 && mask < 0.9) mask = 0.8;
                else if(mask > 0.15)mask = 0.25;
                else mask = 0;


                */
            
            half4 sum = 0;
            // 使用2×2卷积核(均值模糊)
            /*
            sum += tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(1, 1) * _BlurSize);
            sum += tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(1, -1) * _BlurSize);
            sum += tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(-1, 1) * _BlurSize);
            sum += tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(-1, -1) * _BlurSize);
            sum *= 0.25;
            return sum;*/
            
            //使用高斯模糊
            float weight[5] = { 0.1621, 0.0983, 0.0219,0.0133,0.0030 };

            for (int u = -2; u <= 2; u++) {
                for (int v = -2; v <= 2; v++) {
                    sum += tex2D(_MainTex, i.uv + _BlurSize * float2(u, v) * _MainTex_TexelSize.xy) * weight[abs(u) + abs(v)];
                }
            }
            float mask= tex2D(_AddiveMaskTex, i.uv).r;
            sum=(1-mask)*sum + sum*mask * (_AddColor);
            float gray = dot(sum.rgb, float3(0.299, 0.587, 0.114));

            return (_Monochrome*gray+(1-_Monochrome)*sum)*i.color;


            /*
            float weight[3] = {0.4026,0.2442,0.0545};

            float3 mask = 0;// tex2D(_MainTex,i.uv).rgb * weight[0];
            for (int u = -1; u <= 1; u++) {
                for (int v = -1; v <= 1; v++) {
                    mask += tex2D(_MainTex, i.uv + _BlurSize * float2(u, v) * _MainTex_TexelSize.xy) * weight[abs(u) + abs(v)];
                }
            }*/

            //return half4(mask,1);//返回需要设置透明通道值，只有使用Blend命令打开混合后，这里的设置才有意义，否则这些透明度并不会对片元的透明效果有任何影响
            
        }
    ENDHLSL

    SubShader {

        //Tags { "Queue" = "AlphaTest"  "IgnoreProjector" = "true"    "RenderType" = "TransparentCutout" }//透明度测试（要么透明要么不透明）
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "true" "RenderType" = "Transparent" }//透明度混合

        //Tags{ "RenderType" = "Opaque" } // 设置为不透明
        LOD 200//好像是优先级？

        //ZWrite Off // 深度不写入，透明度混合中都应关闭深度写入
        //Cull Off // 不剔除  Cull Back 剔除背面（背向摄像机的面） Cull Front 剔除前面 （朝向摄像机的面）
        //ZTest Always//深度测试无论如何都通过（全覆盖）
        //ColorMask RGB
        //Lighting Off
        //Fog{Mode Off}
        //AlphaTest GEqual 0.1  //透明度>0.1显示（透明度测试用的）等效于clip(color.a - 0.1f)
        //Blend SrcAlpha OneMinusSrcAlpha   //设置该Pass的混合模式，我们将源颜色（该片元着色器产生的颜色）的混合因子设为SrcAlpha，把目标颜色（已经存在于颜色缓冲中的颜色）的混合因子设为OneMinusSrcAlpha
        
  

        Pass{
            HLSLPROGRAM
                #pragma vertex vert  //定点着色器
                #pragma fragment frag    //片段着色器
                //#pragma target 2.0 从2.0最大到5.0，越高功能越多
            ENDHLSL
        }

    }Fallback "Diffuse"//备选着色器

}
