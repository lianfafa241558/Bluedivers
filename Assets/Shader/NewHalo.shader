Shader "LX/NewHalo"{

	Properties{
		[NoScaleOffset]_MainTex("Base (RGB)", 2D) = "white" {}
		[HDR]_BaseColor("_BaseColor", Color) = (1,1,1,1)
		_EmissionScale("_EmissionScale", Range(0,2)) = 1
		_RenderRef("_RenderRef",Int) = 1
	}

	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		sampler2D _MainTex;
		half4 _MainTex_ST;
		half4 _BaseColor;
		half _EmissionScale;

		struct a2v {
			half4 vertex : POSITION;//顶点位置
			half2 texcoord : TEXCOORD0;//纹理坐标
		};

		struct v2f {
			half4 vertex : SV_POSITION;
			half2 texcoord : TEXCOORD2;
		};

		v2f vert(a2v v)
		{
			v2f o;
			o.vertex = GetVertexPositionInputs(v.vertex.xyz).positionCS;
			o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
			return o;
		}



		half4 frag(v2f i) : SV_Target
		{
			half4 col = tex2D(_MainTex, i.texcoord);
			col *= _EmissionScale;

			return col*_BaseColor;
		}
	ENDHLSL

	SubShader{
		Tags{ "RenderType" = "Overlay" "Queue" = "Transparent" "LightMode" = "UniversalForward" }
		LOD 20//这tm不会是是渲染优先级吧
		Cull Off // 不剔除
		ZWrite Off  //深度写入关闭
		Blend SrcAlpha OneMinusSrcAlpha //混合透明度

		Pass {
			Stencil {
				Ref [_RenderRef]  // 设置参考值为1
				Comp always  // 比较函数为always，始终通过
				Pass replace  // 通过测试后替换Stencil缓冲区的值为参考值
			}
			HLSLPROGRAM
				#pragma vertex vert  //定点着色器
				#pragma fragment frag    //片段着色器
			ENDHLSL

		}
	}Fallback "Diffuse"//备选着色器
}


