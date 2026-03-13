Shader "LX/Warping"
{
	Properties{

		_NormalTex("NormalTex", 2D) = "white" {}//x是r，y是g
		_StrengthTex("StrengthTex", 2D) = "white" {}//位移的强度默认就是全1
		_HeatTime("Heat Time", range(0,1)) = 0.1//偏移波动的时间
		_HeatForce("Heat Force", range(0,0.1)) = 0.008//偏移的范围
		
	}

		SubShader{
			Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" }
			Blend SrcAlpha OneMinusSrcAlpha
			//AlphaTest Greater .01
			Cull Off 
			Lighting Off 
			ZWrite Off
			Pass {
				Tags { "LightMode" = "UniversalForward" }

				HLSLPROGRAM
					#pragma vertex vert
					#pragma fragment frag
					#pragma fragmentoption ARB_precision_hint_fastest

					#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
					#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

					struct appdata_t {
						float4 vertex : POSITION;
						float4 color : COLOR;
						float2 texcoord: TEXCOORD0;
					};

					struct v2f {
						float4 vertex : POSITION;
						float4 uvgrab : TEXCOORD0;
						float2 uvmain : TEXCOORD1;
					};

					float _HeatForce;
					float _HeatTime;
					float4 _NormalTex_ST;
					sampler2D _NormalTex;
					sampler2D _StrengthTex;
					SAMPLER(_CameraOpaqueTexture);

					v2f vert(appdata_t v)
					{
						v2f o;
						VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
						o.vertex = vertexInput.positionCS;
						o.uvmain = TRANSFORM_TEX(v.texcoord, _NormalTex);
						return o;
					}

					half4 frag(v2f i) : SV_Target
					{
						half4 strengthTex = tex2D(_StrengthTex,i.uvmain);

						//noise effect
						half4 offsetColor1 = tex2D(_NormalTex, i.uvmain + _Time.xz * _HeatTime);
						half4 offsetColor2 = tex2D(_NormalTex, i.uvmain - _Time.yx * _HeatTime);
						half distortX = ((offsetColor1.r + offsetColor2.r) - 1) * _HeatForce * strengthTex;
						half distorty = ((offsetColor1.g + offsetColor2.g) - 1) * _HeatForce * strengthTex;

						half2 screenUV = (i.vertex.xy / _ScreenParams.xy) + float2(distortX, distorty);

						half4 col = tex2D(_CameraOpaqueTexture, screenUV);
						col.a = 1.0f;

						return col;
					}
				ENDHLSL
			}
		}
}
