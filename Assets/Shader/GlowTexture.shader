Shader "GlowTexture"{

	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
		[HDR]_BaseColor("_BaseColor", Color) = (1,1,1,1)
		
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 10
		[Space(20)]
		[Toggle]_FlowDir("_FlowDir", Float) = 0//波动方向
        _FlowLightSpeed("FlowLightSpeed", Range(-5, 5)) = 1
        //_FlowLightTex("FlowLightTex", 2D) = "white" {}
		[Space(20)]
		_ExtraInterval("_ExtraInterval", Range(0, 10)) = 1 //额外波的间隔       
		_ExtraSpeed("_ExtraSpeed", Range(-5, 5)) = 1        //额外波的速度
		_ExtraWidth("_ExtraWidth", Range(0, 1)) = 1        //额外波的宽度
		_ExtraScale("_ExtraScale", Range(1, 10)) = 1        //额外波的透明度乘数
	}

	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		sampler2D _MainTex;
		half4 _MainTex_ST;
		half4 _BaseColor;

		bool _FlowDir;
        half _FlowLightSpeed;
		half _ExtraInterval;
		half _ExtraSpeed;
		half _ExtraWidth;
		half _ExtraScale;

		struct a2v {
			half4 vertex : POSITION;//顶点位置
			half2 texcoord : TEXCOORD0;//纹理坐标
			half4 color : COLOR;//传递image的颜色用
		};
		

		struct v2f {
			half4 vertex : SV_POSITION;
			half2 texcoord : TEXCOORD2;
			half4 color         : TEXCOORD1;//传递image的颜色用
		};

		v2f vert(a2v v)
		{
			v2f o;
			o.vertex = GetVertexPositionInputs(v.vertex.xyz).positionCS;
			o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
			//每次波动的时间↓_FlowLightSpeed  
            //half Offest = (_Time.y * _FlowLightSpeed % 1);
			//o.texcoord+=half2(_FlowDir?0:Offest,_FlowDir?Offest:0);
			o.color = v.color;
			return o;
		}



		half4 frag(v2f i) : SV_Target
		{
			half Offest = (_Time.y * _FlowLightSpeed % 1);
			half2 uv= (i.texcoord.x+(_FlowDir?0:Offest),i.texcoord.y+(_FlowDir?Offest:0));
			half4 col = tex2D(_MainTex,uv)*i.color;
			
			half value=_FlowDir?i.texcoord.y:i.texcoord.x;
			half scale=_FlowDir?_MainTex_ST.y:_MainTex_ST.x;

			//波动的值(0,1)
			half Offest2 = (_Time.y * abs(_ExtraSpeed)%_ExtraInterval);
			//波动方向
			half dir =(_ExtraSpeed>0?1:-1);
			//像素的位置(0,1)
			half value2=(value/scale);

			half ExtraAlpha = abs(value2-(1+dir*Offest2))<_ExtraWidth?_ExtraScale:1;


			return  half4(col.rgb*_BaseColor.rgb,col.r*_BaseColor.a*ExtraAlpha); 
		}
	ENDHLSL

    SubShader {
        LOD 20// 越靠前的subshader的lod值应越大，但是没看懂是干嘛的
        Blend [_SrcBlend] [_DstBlend]
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


