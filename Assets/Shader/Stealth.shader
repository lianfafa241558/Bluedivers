Shader "LX/Stealth"
{
	Properties{
		_MainTex("Texture", 2D) = "white"{}

		[HDR]_HitColor("_HitColor", Color) = (0,0,0,0)

		_NormalTex("NormalTex", 2D) = "white" {}//x是r，y是g
		_StrengthTex("StrengthTex", 2D) = "white" {}//位移的强度默认就是全1
		_HeatTime("Heat Time", range(0,1)) = 0.1//偏移波动的时间
		_HeatForce("Heat Force", range(0,0.1)) = 0.008//偏移的范围

		[Enum(Off, 0, On, 1)]_ZWriteMode("ZWriteMode", float) = 0
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10

		[Space(15)] 
        [Toggle(_UseFresnel)]_UseFresnel("_UseFresnel", Float) = 0
        _FresnelScale("_FresnelScale", Range(0,1)) = 0.5
        _FresnelDecay("_FresnelDecay", Range(0,30)) = 1

		[Header(Dissolve)]
        [Toggle(_UseAlphaClipping)]_UseAlphaClipping("_UseDissolve", Float) = 0
        _DissolveValue("DissolveValue", Color) = (0,0,0)//实际上float就行，但是为了方便控制
        _EdgeWidth ("Edge Width", Range(0, 0.1)) = 0.05

        _AlphaMap("_AlphaMap", 2D) = "white" {}
        [HDR]_EdgeColor("_EdgeColor", Color) = (0.8,0.8,0.8)
	}

		SubShader{
			Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" }
			//Blend SrcAlpha OneMinusSrcAlpha
			Blend[_SrcBlend][_DstBlend]
			//AlphaTest Greater .01
			Cull Off 
			Lighting Off 
			ZWrite [_ZWriteMode]
			Pass {
				Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma shader_feature_local _UseFresnel
			#pragma shader_feature_local _UseAlphaClipping
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct appdata_t {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord: TEXCOORD0;
#if _UseFresnel
				float3 normal : NORMAL;
#endif
			};

			struct v2f {
				float4 vertex : POSITION;
				float4 uvgrab : TEXCOORD0;
				float2 uvmain : TEXCOORD1;
				float2 texcoord: TEXCOORD2;
#if _UseFresnel||_UseAlphaClipping
				float3 positionWS : TEXCOORD4;
#endif
#if _UseFresnel
				float3 normalWS : TEXCOORD3;
#endif
			};
			float4 _HitColor;

			float _HeatForce;
			float _HeatTime;
			float4 _NormalTex_ST;
			sampler2D _NormalTex;
			sampler2D _StrengthTex;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			SAMPLER(_CameraOpaqueTexture);
#if _UseFresnel
			float _FresnelScale;
			float _FresnelDecay;
#endif

			sampler2D _AlphaMap;
			half3 _EdgeColor;
			half _EdgeWidth;
			half3 _DissolveValue;

			//将half3变为half2
			half2 vertexToHalf2Method1(float3 vertexPos)
			{
				// Use normalized components to ensure uniform distribution
				half2 uv = half2(
					frac(vertexPos.x * 0.14159 + vertexPos.z * 0.26795),
					frac(vertexPos.y * 0.31831 + vertexPos.x * 0.41421)
				);
				return uv;
			}


			v2f vert(appdata_t v)
			{
				v2f o;
				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
				o.vertex = vertexInput.positionCS;
				o.uvgrab = float4(0, 0, 0, 0);
				o.uvmain = TRANSFORM_TEX(v.texcoord, _NormalTex);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
			#if _UseFresnel
				
				
			#endif
			#if _UseFresnel||_UseAlphaClipping
				o.positionWS = TransformObjectToWorld(v.vertex.xyz);
			#endif
			#if _UseFresnel
				o.normalWS = TransformObjectToWorldNormal(v.normal);
			#endif
				return o;
			}

			half4 frag(v2f i) : SV_Target
			{
				half4 strengthTex = tex2D(_StrengthTex,i.uvmain);
				


				//noise effect
				half4 offsetColor1 = tex2D(_NormalTex, i.uvmain + _Time.xz * _HeatTime);
				half4 offsetColor2 = tex2D(_NormalTex, i.uvmain - _Time.yx * _HeatTime);
				half distortX = ((offsetColor1.r + offsetColor2.r) - 1) * _HeatForce * strengthTex.r;
				half distorty = ((offsetColor1.g + offsetColor2.g) - 1) * _HeatForce * strengthTex.r;

				half2 screenUV = (i.vertex.xy / _ScreenParams.xy) + float2(distortX, distorty);

				half4 col = tex2D(_CameraOpaqueTexture, screenUV);

				half3 mainTex = tex2D(_MainTex, i.uvmain).rgb;
				half3 extraCol = mainTex.rgb*saturate(_HitColor.rgb+_DissolveValue.rgb*10);
				
#if _UseFresnel
				Light mainLight = GetMainLight();
				half3 N = i.normalWS;
				half3 L = normalize(_WorldSpaceCameraPos - i.positionWS);
				half NdotL =saturate(dot(N,L));
				if (NdotL > 0) {
					extraCol *=  pow(1 - NdotL,_FresnelDecay)*_FresnelScale;
				}
#endif

				col.rgb+=extraCol;
				col.a = 1.0f;

			#if _UseAlphaClipping

				half dissolve=(_DissolveValue.r*1.2-0.1);
				half v = tex2D(_AlphaMap, vertexToHalf2Method1(i.positionWS)).r;
				clip(v - dissolve+_EdgeWidth);
				half isDissolved = step(dissolve+_EdgeWidth, v);//阶跃方法(小于dissolve时0，大于时1)
				half3 edgeColor = _EdgeColor * smoothstep(dissolve+_EdgeWidth, dissolve, v);
				col.rgb = lerp(col.rgb,edgeColor,1-isDissolved);
			#endif


				return col;
			}
			ENDHLSL
		}
	}
}
