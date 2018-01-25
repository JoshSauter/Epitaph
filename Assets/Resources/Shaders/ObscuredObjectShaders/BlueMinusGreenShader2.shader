﻿Shader "Custom/ObscuredObjectShaders/BlueMinusGreenShader2"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_BiasTowardsVisible ("Bias Towards Visible", Range(-1.0, 1.0)) = 0
		_Color ("Main Color", Color) = (1.0, 1.0, 1.0, 1.0)
	}
	SubShader
	{
		Tags { "Queue"="Geometry" "RenderType"="BlueMinusGreen2" }
		LOD 100

		Pass
		{
			ZWrite On ZTest LEqual
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			sampler2D _DiscardTex2;
			float _BiasTowardsVisible;
			float4 _Color;
			float4 _MainTex_ST;

			float _ResolutionX;
			float _ResolutionY;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv) * _Color;
				float2 viewportVertex = float2(i.vertex.x / _ResolutionX, i.vertex.y / _ResolutionY);
				float4 samplePixel = tex2D(_DiscardTex2, viewportVertex);
				clip(_BiasTowardsVisible + samplePixel.b - samplePixel.g);
				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
	
	Fallback "VertexLit"
}
