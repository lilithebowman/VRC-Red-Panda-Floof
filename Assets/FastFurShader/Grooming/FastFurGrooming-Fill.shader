Shader "Warren's Fast Fur/Internal Utilities/Fill"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_Threshold("Threshold", float) = 0.5
		_UVThreshold("UV Threshold", float) = 0.01
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Cull Off
		ZTest Off


		// The purpose of this shader is to do a 2D surface fill, rather than painting with a 3D 
		// cursor. However, I don't know how to deal with hard-edges, and so this shader doesn't
		// recognize them as being connected.


		Pass
		{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			struct meshData
			{
				float4 vertex : POSITION;
	            float2 uv0 : TEXCOORD0;
	            float2 uv1 : TEXCOORD1;
	            float2 uv2 : TEXCOORD2;
	            float2 uv3 : TEXCOORD3;
			};

			struct v2f
			{
				centroid float4 vertex : SV_POSITION;
				centroid float2 uv : TEXCOORD0;
			};
            
            int _SelectedUV = 0;
			float2 _HitUV;
			float2 _MirrorHitUV;
			float _UVThreshold;
			float4 _MainTex_TexelSize;
			float _Threshold;
			float _FurGroomBrushRadius;

			v2f vert(meshData v)
			{
				v2f o;

                float2 uv = _SelectedUV == 3 ? v.uv3 : _SelectedUV == 2 ? v.uv2 : _SelectedUV == 1 ? v.uv1 : v.uv0;
                o.uv = uv;

				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}

			SamplerState my_point_repeat_sampler;
			UNITY_DECLARE_TEX2D(_MainTex);

			fixed4 frag(v2f i) : SV_Target
			{
				// sample the texture
				float4 originalCol = _MainTex.SampleLevel(my_point_repeat_sampler, i.uv, 0);
				float dist = abs(originalCol.a);
				float distUV = min(length(i.uv - _HitUV), length(i.uv - _MirrorHitUV));
				if(dist < _Threshold && distUV < _UVThreshold) return (originalCol);
				if(dist > _FurGroomBrushRadius) return float4(0.5,0,0,1000000);
				float2 loc = i.uv;
				for(int z = 0 ; z < 64 ; z++)// 32 is probably as much as we need, but I'm doubling it in case someone is using a high-res texture
				{
					float closest = 900000;
					int closestX = 0;
					int closestY = 0;

					for(int x = -3 ; x < 4 ; x++)
					{
						for(int y = -3 ; y < 4 ; y++)
						{
							float2 searchUV = loc + float2(_MainTex_TexelSize.x * x, _MainTex_TexelSize.y * y);
							float tex = abs(_MainTex.SampleLevel(my_point_repeat_sampler, searchUV, 0).a);
							distUV = min(length(searchUV - _HitUV), length(searchUV - _MirrorHitUV));
							
							if(tex > 0.0098) // Alpha of less than (2.5 / 255) are non-rendered pixels, so we need to ignore them
							{
								if(tex <= _Threshold && distUV < _UVThreshold) return (originalCol);
								if(tex < closest)
								{
									closest = tex;
									closestX = x;
									closestY = y;
								}
							}
						}
					}

					if(closestX == 0 && closestY == 0) return float4(0,1,1,1000000);

					loc = loc + float2(_MainTex_TexelSize.x * closestX, _MainTex_TexelSize.y * closestY);
				}

				return float4(1,1,0,1000000);
			}
			ENDCG
		}
	}
}
