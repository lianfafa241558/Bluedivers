Shader "WarningWall"{

	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
		[HDR]_BaseColor("_BaseColor", Color) = (1,1,1,1)
		
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10
		[Enum(Off, 0, On, 1)]_ZWriteMode("ZWriteMode", float) = 0

		_Noise ("Noise", 2D) = "white" {}
        _distortFactorTime("FactorTime",Range(0,100)) = 0.5
        _distortFactor("factor",Range(0,1)) = 0

	}

	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		sampler2D _MainTex;
		half4 _MainTex_ST;
		half4 _BaseColor;

        sampler2D _Noise;
        float4 _Noise_ST;
        half _distortFactorTime;
        half _distortFactor;

		struct a2v {
			half4 vertex : POSITION;//顶点位置
			half2 texcoord : TEXCOORD0;//纹理坐标
			half4 color : COLOR;//传递image的颜色用
		};
		

		struct v2f {
			half4 vertex : SV_POSITION;
			half2 texcoord : TEXCOORD2;
			half4 color         : TEXCOORD1;//传递image的颜色用
			float3 worldPos : TEXCOORD3;
			//half distance	: TEXCOORD4;
			float2 uv2 : TEXCOORD4;
		};

		v2f vert(a2v v)
		{
			v2f o;
			o.vertex = GetVertexPositionInputs(v.vertex.xyz).positionCS;
			o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
			o.uv2 =o.texcoord;
			o.color = v.color;

			// 计算世界坐标
			float4 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));

			o.worldPos = worldPos.xyz; 

			return o;
		}



		half4 frag(v2f i) : SV_Target
		{
			//屏幕位置扭动
            half dragOffset = sin(_Time.y * _distortFactorTime);

            //i.uv2.x+=_Time.y*_distortFactor;
            i.uv2.x+=_Time.y*_distortFactor;
            i.texcoord.y = i.texcoord.y +tex2D(_Noise, i.uv2*_Noise_ST.xy+_Noise_ST.zw).r * dragOffset*_distortFactor;

			half4 col = tex2D(_MainTex,i.texcoord);

			half a=clamp(1-length(i.worldPos - _WorldSpaceCameraPos.xyz)/25,0,1);
			clip(a);
			return  half4(col.rgb,col.a*a*a)*i.color*_BaseColor; 
		}
	ENDHLSL

    SubShader {
        LOD 20// 越靠前的subshader的lod值应越大，但是没看懂是干嘛的
        Blend SrcAlpha [_ZWriteMode]
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


