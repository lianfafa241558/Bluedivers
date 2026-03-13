
Shader "LX/FogSimulation" {

        Properties
        {
            _MainTex("Texture", 2D) = "white" {}
            _FogColor("FogColor",color) = (1,1,1,1)
            _FogIntensity("FogIntensity",float) = 1
            _FogDistance("FogDistance",float) = 1
            _Luminosity("_Luminosity ", Range(0.0, 1.0)) = 1.0//黑白化的系数
            [Toggle]_UseLight("_UseLight", Float) = 0
            _LightValue("_LightValue", Float) = 1


            [ToggleUI]_Reverse("Reverse", Float) = 0
        }
            SubShader
            {
                Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
                LOD 200

                Pass
                {

                    Stencil{
                        Ref 1
                        Comp NotEqual
                        Pass Replace
                    }

                    //Tags{"LightMode" = "UniversalForward"}


                    Tags{"LightMode" = "ForwardAdd"}
                    //ZTest Always
                    ZWrite Off
                    Cull Off
                    ZTest Less

                    HLSLPROGRAM
                    #pragma vertex vert
                    #pragma fragment frag

                    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/core.hlsl"
                    //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
                    struct appdata
                    {
                        float4 positionOS : POSITION;
                        float2 uv : TEXCOORD0;
                    };

                    struct v2f
                    {
                        float2 uv : TEXCOORD0;
                        float4 positionCS : SV_POSITION;
                        //float3 positionWS : TEXCOORD2;
                        float4 scrPos  : TEXCOORD1;
                    };
                    
                    TEXTURE2D(_MainTex);
                    SAMPLER(sampler_MainTex);
                    TEXTURE2D(_CameraDepthTexture);
                    SAMPLER(sampler_CameraDepthTexture);

                    float4 _MainTex_ST;
                    float4 _FogColor;
                    float _FogIntensity;
                    float _FogDistance;
                    half _Luminosity;
                    half _UseLight;
                    half _LightValue;


                    half _Reverse;

                    v2f vert(appdata v)
                    {
                        v2f o;
                        o.positionCS = TransformObjectToHClip(v.positionOS);
                        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                        o.scrPos  = ComputeScreenPos(o.positionCS);
                        //o.positionWS = mul(unity_ObjectToWorld, v.positionOS);
                        return o;
                    }
                    
                    half4 frag(v2f i) : SV_Target
                    {


                        float2 ssuv = i.scrPos .xy / i.scrPos .w; //uv 
                        //与中心的差值(disuv越小，放大效果越大)
                        half2 disuv = (ssuv - 0.5);

                        

                        half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ssuv);

                        if (_Reverse) {
                            return half4(0.05 - col.rgb, 1);
                        }

                        half depth = SAMPLE_TEXTURE2D(_CameraDepthTexture,sampler_CameraDepthTexture,ssuv);//深度图采样
                        // LinearEyeDepth是视角空间下的深度值，范围是 [相机近裁剪面， 相机远裁剪面]
                        half ssdepth = LinearEyeDepth(depth,_ZBufferParams);//线性深度[0.2,70]
                       

                        half scale = 0;
                        
                        if (_UseLight) {

                            //_FogDistance * (1 + 5 * power约等于70
                            //后面这个越小雾气越小，power越大雾气越小
                            half power = saturate(1 - pow(6 * length(half2(i.uv.x - 0.5, (i.uv.y - 0.5) / 1.78)), 2));
                            scale = saturate(1 - ssdepth / (_FogDistance * (1 + 5 * power * _LightValue)));
                                //距离补正
                                half brightness = min(ssdepth / _FogDistance*2,3);
                                col = col * (1+ power * brightness);
                            
                        }
                        else {
                            //scale越小越雾强度越高
                            scale = saturate(1 - ssdepth / _FogDistance);
                        }
                        scale = saturate(scale+_Luminosity * 0.8f+1- _FogIntensity);

                        col = col * scale + (1 - scale) * _FogColor;


                        return col;
                    }


                    ENDHLSL
                }
            }
    }
