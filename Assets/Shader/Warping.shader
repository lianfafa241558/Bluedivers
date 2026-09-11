Shader "LX/Warping"
{
	Properties{

		_NormalTex("NormalTex", 2D) = "white" {}//x是r，y是g
		_StrengthTex("StrengthTex", 2D) = "white" {}//位移的强度默认就是全1
		_HeatTime("Heat Time", range(0,1)) = 0.1//偏移波动的时间
		_HeatForce("Heat Force", range(0,0.1)) = 0.008//偏移的范围
		_DistortFullDistance("Distort Full Distance", Range(0.1, 200)) = 15//小于该距离(米)保持全强度，更远按 该距离/实际距离 衰减

	}

		SubShader{
			Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" }
			Blend SrcAlpha OneMinusSrcAlpha
			//AlphaTest Greater .01
			Cull Off 
			Lighting Off 
			ZWrite Off
			Pass {
				// 由 WarpingBeforeFogRendererFeature 在 445（雾 Pass 450 之前）手工绘制，
				// 使输出能被全屏雾一起雾化；不再走 URP 默认透明 Pass（否则会被画两遍）
				Tags { "LightMode" = "WarpingEffect" }

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
                        float4 color : COLOR;
						float distortScale : TEXCOORD2;
					};

					float _HeatForce;
					float _HeatTime;
					float _DistortFullDistance;
					float4 _NormalTex_ST;
					sampler2D _NormalTex;
					sampler2D _StrengthTex;
					SAMPLER(_CameraOpaqueTexture);

				v2f vert(appdata_t v)
				{
					v2f o;
					VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
					o.vertex = vertexInput.positionCS;
					o.uvgrab = float4(0, 0, 0, 0);
					o.uvmain = TRANSFORM_TEX(v.texcoord, _NormalTex);
                    o.color=v.color;
					// 位移量是屏幕空间的固定偏移，而远处的面片在屏幕上很小 —— 同样的偏移占比过大，
					// 看起来就会"扭得特别厉害"。这里用透视投影的 w（≈相机空间深度）做归一：
					// 距离 <= _DistortFullDistance 时满强度，更远按 距离反比 衰减
					o.distortScale = saturate(_DistortFullDistance / max(vertexInput.positionCS.w, 0.001));
					return o;
				}

				half4 frag(v2f i) : SV_Target
				{
					half4 strengthTex = tex2D(_StrengthTex,i.uvmain);

					//noise effect
					half4 offsetColor1 = tex2D(_NormalTex, i.uvmain + _Time.xz * _HeatTime);
					half4 offsetColor2 = tex2D(_NormalTex, i.uvmain - _Time.yx * _HeatTime);
					half distortAmount = _HeatForce * strengthTex.r * i.distortScale;
					half distortX = ((offsetColor1.r + offsetColor2.r) - 1) * distortAmount;
					half distorty = ((offsetColor1.g + offsetColor2.g) - 1) * distortAmount;

					half2 screenUV = (i.vertex.xy / _ScreenParams.xy) + float2(distortX, distorty)*i.color.a;

					half4 col = tex2D(_CameraOpaqueTexture, screenUV);
					col.a = 1;

					return col;
				}
				ENDHLSL
			}
		}
}
