Shader "GlowTexture2"{

	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
		[HDR]_BaseColor("_BaseColor", Color) = (1,1,1,1)
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10
		[Enum(Off, 0, On, 1)]_ZWriteMode("ZWriteMode", float) = 0
		[Space(20)]
		[Toggle]_FlowTrans("_FlowTrans", Float) = 0//是否转置波动贴图
		[Toggle]_FlowDir("_FlowDir", Float) = 0//波动方向
        _FlowLightSpeed("FlowLightSpeed", Range(-5, 5)) = 1
        _FlowLightTex("FlowLightTex", 2D) = "white" {}
		[Space(15)] 
        [Toggle(_UseLight)]_UseLight("_UseLight", Float) = 0
        [Toggle(_MY_FOG_ENABLE)] _MY_FOG_ENABLE("_UnityFogEnable", Float) = 0
	}

	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #pragma shader_feature_local _UseLight
        #pragma multi_compile_fog
        #pragma shader_feature_local _MY_FOG_ENABLE

		sampler2D _MainTex;
		half4 _MainTex_ST;
		half4 _BaseColor;

		bool _FlowTrans;
		bool _FlowDir;
        half _FlowLightSpeed;
        sampler2D _FlowLightTex;
		half4 _FlowLightTex_ST;

		struct a2v {
			half4 vertex : POSITION;//顶点位置
			half2 texcoord : TEXCOORD0;//纹理坐标
			half4 color : COLOR;//传递image的颜色用
		};
		

		struct v2f {
			half4 vertex : SV_POSITION;
			half2 texcoord : TEXCOORD2;
			half2 texcoord2 : TEXCOORD3;
			half4 color         : TEXCOORD1;//传递image的颜色用
            float fogFactor : TEXCOORD4;
		};

		v2f vert(a2v v)
		{
			v2f o;
			o.vertex = GetVertexPositionInputs(v.vertex.xyz).positionCS;
			o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

			o.texcoord2 = TRANSFORM_TEX(v.texcoord, _FlowLightTex);
			if(_FlowTrans)o.texcoord2=half2(o.texcoord2.y ,o.texcoord2.x);
			//else o.texcoord2 = half2(o.texcoord2.x,o.texcoord2.y);
			//每次波动的时间↓_FlowLightSpeed
            half flowLightOffset = (_Time.y * _FlowLightSpeed % 3);
			o.texcoord2+=half2(_FlowDir?0:flowLightOffset,_FlowDir?flowLightOffset:0);
			o.color = v.color;
			o.fogFactor = 0;
            #if _MY_FOG_ENABLE
            o.fogFactor = ComputeFogFactor(v.vertex.z);
            #endif
			return o;
		}



		half4 frag(v2f i) : SV_Target
		{
			half4 col = tex2D(_MainTex, i.texcoord);


            half4 flowLightColor = tex2D(_FlowLightTex, half2(i.texcoord2.x,i.texcoord2.y))*_BaseColor;
             #if _UseLight
            Light mainLight = GetMainLight();
            col.rgb *= mainLight.color;
            #endif
            #if _MY_FOG_ENABLE && (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
            col.rgb = MixFog(col.rgb, i.fogFactor);
            //col.rgb =  i.fogFactor;
            #endif
			return  half4(0.5*col.rgb+0.5*flowLightColor.rgb,col.a*max(1,flowLightColor.r))*i.color; 
		}
	ENDHLSL

    SubShader {
        LOD 20// 越靠前的subshader的lod值应越大，但是没看懂是干嘛的
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWriteMode] // 深度不写入，透明度混合中都应关闭深度写入
        Cull Off // 不剔除  Cull Back 剔除背面（背向摄像机的面） Cull Front 剔除前面 （朝向摄像机的面）

        Pass{
            HLSLPROGRAM
                #pragma vertex vert  //定点着色器
                #pragma fragment frag    //片段着色器
            ENDHLSL
        }

	}Fallback "Diffuse"//备选着色器
}


