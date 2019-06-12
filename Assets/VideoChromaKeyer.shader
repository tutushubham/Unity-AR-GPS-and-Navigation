Shader "Holistic3D/VideoChromaKeyer"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_KeyColor("Green Color", Color) = (0,0,0,0)
		_Sensitivity("Threshold Sensitivity", Range(0,1)) = 0.03
		_Smooth("Smoothing", Range(0,1)) = 0.3
		[Toggle]_ShowOriginal("Show Original Video", Float) = 0
	}
		
		SubShader
		{
			Tags{ "Queue" = "Transparent" "RenderType" = "Opaque" }
			GrabPass {
				Name "BACKGROUND"
			Tags { "LightMode" = "Always" }
		}

			Pass
			{
				Name "BACKGROUND"
				Tags { "LightMode" = "Always" }
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"

				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					float4 vertex : SV_POSITION;
					float4 uvgrab : TEXCOORD1;
				};

				v2f vert(appdata v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.uvgrab = ComputeGrabScreenPos(o.vertex);
					o.uv = v.uv;
					
					return o;
				}

				sampler2D _MainTex;
				float3 _KeyColor;
				float _Sensitivity;
				float _Smooth;
				float _ShowOriginal;

				sampler2D _GrabTexture;
				float4 _GrabTexture_TexelSize;

				fixed4 frag(v2f i) : SV_Target
				{
					if (_ShowOriginal)
					{
						return tex2D(_MainTex, i.uv);
					}
					
				    fixed4 col = tex2D(_MainTex, i.uv); 
					fixed4 col2 = tex2Dproj(_GrabTexture, UNITY_PROJ_COORD(i.uvgrab));

					float maskY = 0.2989 * _KeyColor.r + 0.5866 * _KeyColor.g + 0.1145 * _KeyColor.b;
					float maskCr = 0.7132 * (_KeyColor.r - maskY);
					float maskCb = 0.5647 * (_KeyColor.b - maskY);

					float Y = 0.2989 * col.r + 0.5866 * col.g + 0.1145 * col.b;
					float Cr = 0.7132 * (col.r - Y);
					float Cb = 0.5647 * (col.b - Y);

					float alpha = smoothstep(_Sensitivity, _Sensitivity + _Smooth, distance(float2(Cr, Cb), float2(maskCr, maskCb)));

					col = (alpha * col) + ((1 - alpha) * col2);
					
					return col;
				}
				ENDCG
			}
		}
}