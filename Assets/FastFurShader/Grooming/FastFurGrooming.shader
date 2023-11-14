Shader "Warren's Fast Fur/Internal Utilities/Groomer"
{
	Properties
	{
		//[HideInInspector]
		_FurShapeMap("Fur Depth and Combing Map", 2D) = "grey" {}
		_FurGroomingMask("Fur Grooming Mask", 2D) = "grey" {}
		_FurShapeMapPreEdit("Pre-Edited Fur Depth and Combing Map", 2D) = "grey" {}
		_DirectionTex("Direction Map", 2D) = "white" {}
		_ClosestTex("Closest Map", 2D) = "white" {}

		_FurCombPosition1("Comb Position 1", 2D) = "black"{}
		_FurCombPosition2("Comb Position 2", 2D) = "black"{}

		_FurBrushMode("Brush Mode", int) = 0
		_FurLengthMode("Length Mode", int) = 0
		_FurDensityMode("targetDensity Mode", int) = 0
		_FurCombingMode("Combing Mode", int) = 0

		_FurGroomBrushRadius("Brush Radius", float) = 4
		_FurGroomBrushFalloff("Brush Falloff", float) = 0.5
		_FurGroomBrushStrength("Brush Strength", float) = 0.5
		_FurGroomFurHeight("Fur Height", float) = 1
		_FurGroomFurFlatness("Fur Flatness", float) = 1
		_FurGroomFurDensity("Fur targetDensity", float) = 1
		_FurGroomFurHeightEnabled("Fur Height Enabled", int) = 0
		_FurGroomFurFlatnessEnabled("Fur Flatness Enabled", int) = 0
		_FurGroomFurDensityEnabled("Fur targetDensity Enabled", int) = 0
		_FurMirror("Brush Mirror", int) = 1
		_FurMirrorX("Brush Mirror X Offset", float) = 0

		_FurBaseDensity("Base targetDensity", float) = 0
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Cull Off
		ZTest Off

		Pass
		{
			CGPROGRAM

			#include "UnityCG.cginc"
			#include "FastFur-Functions.cginc"

			#pragma target 4.6

			#pragma require geometry

			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag

			float4x4 _FurMeshMatrix; // Updated every frame by the grooming script
			UNITY_DECLARE_TEX2D(_DirectionTex);
			UNITY_DECLARE_TEX2D(_ClosestTex);
			UNITY_DECLARE_TEX2D(_FurShapeMap);
			UNITY_DECLARE_TEX2D(_FurShapeMapPreEdit);
			UNITY_DECLARE_TEX2D(_FurCombPosition1);
			UNITY_DECLARE_TEX2D(_FurCombPosition2);
			UNITY_DECLARE_TEX2D(_FurGroomingMask);

			int _SelectedUV = 0;

			float _FurMinHeight;
			float _FurBaseDensity;

			float _FurGroomBrushRadius;
			float _FurGroomBrushFalloff;
			float _FurGroomBrushStrength;
			float _FurGroomFurHeight;
			float _FurGroomFurCombing;
			float _FurGroomFurDensity;

			int _FurGroomFurHeightEnabled;
			int _FurGroomFurCombingEnabled;
			int _FurGroomFurDensityEnabled;
			int _FurGroomFurHeightSetAll;
			int _FurGroomFurCombingSetAll;
			int _FurGroomFurDensitySetAll;

			int _FurBrushMode;
			int _FurLengthMode;
			int _FurDensityMode;
			int _FurCombingMode;

			struct meshData
			{
				float4 vertex : POSITION;
				float2 uv0 : TEXCOORD0;
				float2 uv1 : TEXCOORD1;
				float2 uv2 : TEXCOORD2;
				float2 uv3 : TEXCOORD3;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
			};

			struct v2f {
				centroid float4 pos : SV_POSITION;
				centroid float2 uv : TEXCOORD0;
				centroid float3 worldPos : TEXCOORD1;
				centroid float3 normal : TEXCOORD2;
				centroid float3 tangent : TEXCOORD3;
				centroid float3 bitangent : TEXCOORD4;
				centroid float  relativeDensity : TEXCOORD5;
			};

			v2f vert(meshData v)
			{
				v2f o;

				float2 uv = _SelectedUV == 3 ? v.uv3 : _SelectedUV == 2 ? v.uv2 : _SelectedUV == 1 ? v.uv1 : v.uv0;
				o.uv = uv;

				// We need the 3D position so that we can tell if the mouse pointer is nearby
				o.worldPos = mul(_FurMeshMatrix, v.vertex);

				// Calculate the surface normals, tangents, and bitangents
				o.normal = normalize(mul(_FurMeshMatrix, v.normal));
				o.tangent = normalize(mul(_FurMeshMatrix, v.tangent.xyz));
				o.bitangent = cross(o.normal, o.tangent) * v.tangent.w * unity_WorldTransformParams.w;

				// The geometry shader will take care of these
				o.pos = float4(0, 0, 0, 0);
				o.relativeDensity = 1;

				return o;
			}


			// The geometry shader is needed in order to handle triangles whose UVs are outside of the unit square.
			// We also use it to calculate relative targetDensity. 
			[maxvertexcount(12)]
			void geom(triangle v2f IN[3], inout TriangleStream<v2f> tristream)
			{
				v2f o = (v2f)0;

				// Calculate the relative pixel targetDensity, based upon how far away the vertexes are in the uv map compared to the world
				float relativeDensity[3];
				relativeDensity[0] = distance(IN[0].uv % 1, IN[1].uv % 1) / distance(IN[0].worldPos, IN[1].worldPos);
				relativeDensity[1] = distance(IN[0].uv % 1, IN[2].uv % 1) / distance(IN[0].worldPos, IN[2].worldPos);
				relativeDensity[2] = distance(IN[1].uv % 1, IN[2].uv % 1) / distance(IN[1].worldPos, IN[2].worldPos);
				float minRelativeDensity = 10000;
				//float totalRelativeDensity = 0;
				for (int i = 0; i < 3; i++)
				{
					minRelativeDensity = min(minRelativeDensity, relativeDensity[i]);
					//totalRelativeDensity += relativeDensity[i];
				}
				// Weighting to the minimum targetDensity (ie. most stretched-out UV map pixels) and ignoring the average seems to give the best overall results.
				o.relativeDensity = minRelativeDensity;

				// Check if the UVs need to be re-mapped
				float2 maxPos = max(max(IN[0].uv, IN[1].uv), IN[2].uv);
				float2 offset = float2(0,0);
				if (maxPos.x < 0 || maxPos.x > 1) offset.x = -floor(maxPos.x);
				if (maxPos.y < 0 || maxPos.y > 1) offset.y = -floor(maxPos.y);

				// Check if the UVs are still outside of the unit square
				bool outOfBounds = false;
				for (i = 0; i < 3; i++)
				{
					if (any((IN[i].uv + offset) < 0)) outOfBounds = true;
				}

				for (int xOffset = 0; xOffset < (outOfBounds ? 1 : 2); xOffset++)
				{
					for (int yOffset = 0; yOffset < (outOfBounds ? 1 : 2); yOffset++)
					{
						for (i = 0; i < 3; i++)
						{
							o.uv = IN[i].uv + offset + float2(xOffset, yOffset);
							// We're rendering to a flat UV mapped texture, not the screen, so we need to pass the UV map coordinates instead of the 3D vertex position
							float x = o.uv.x * 2 - 1;
							float y = (1 - o.uv.y) * 2 - 1;
							o.pos = float4(x, y, 0, 1);

							o.worldPos = IN[i].worldPos;

							o.normal = IN[i].normal;
							o.tangent = IN[i].tangent;
							o.bitangent = IN[i].bitangent;

							tristream.Append(o);
						}

						tristream.RestartStrip();
					}
				}
			}

			float4 frag(v2f i) : SV_Target
			{
				// Get our textures and starting colour
				float4 col = UNITY_SAMPLE_TEX2D(_FurShapeMap, i.uv);
				float4 finalCol = col;
				float4 preEdit = UNITY_SAMPLE_TEX2D(_FurShapeMapPreEdit, i.uv);
				float4 maskCol = UNITY_SAMPLE_TEX2D(_FurGroomingMask, i.uv);
				float4 directionMap = UNITY_SAMPLE_TEX2D(_DirectionTex, i.uv);
				float4 closestMap = UNITY_SAMPLE_TEX2D(_ClosestTex, i.uv);

				float dist = abs(directionMap.a);
				float targetDensity = _FurGroomFurDensity;

				// Apply density to the alpha channel
				targetDensity = _FurBrushMode == 3 ? maskCol.a : _FurGroomFurDensity;
				// Relative density
				if (_FurDensityMode == 1 && _FurBrushMode != 3)// We can't do relative density when copying from a mask
				{
					// Unpack the targetDensity so that 0 -> 0.01, 0.5 -> 1, 1 -> 100
					float sliderDensity = pow(10, _FurGroomFurDensity * 4 - 2);

					// Relativize
					targetDensity = (sliderDensity * _FurBaseDensity) / i.relativeDensity;

					// Re-pack the targetDensity so that 0.01 -> 0, 1 -> 0.5, 100 -> 1
					targetDensity = saturate((log10(targetDensity) + 2) * 0.25);
				}

				float fade = _FurGroomBrushFalloff >= 1 ? 1 : saturate(reset(_FurGroomBrushRadius, _FurGroomBrushRadius * _FurGroomBrushFalloff, 0, 1, dist));
				fade = smoothstep(0, 1, fade);
				fade *= (_FurGroomBrushStrength + 0.03) / 1.03;

				float2 combVector = col.xy;

				if (dist <= closestMap.r)// We only need to check for 'closest' when combing
				{
					// Apply combing in the direction cursor is moving to the red and green channels
					float4 comb1 = UNITY_SAMPLE_TEX2D(_FurCombPosition1, i.uv);
					float4 comb2 = UNITY_SAMPLE_TEX2D(_FurCombPosition2, i.uv);

					// Determine the target strength
					float targetCombStrength = _FurGroomFurCombing;
					if (_FurBrushMode == 3) targetCombStrength = length(maskCol.xy * 2 - 1);// Copy from mask
					if (_FurCombingMode == 2) targetCombStrength = length(preEdit.xy * 2 - 1);// Direction only
					// If we are pinching or repelling fur, weaken the strength at the very centre, otherwise it creates artifacts
					if (_FurCombingMode == 3) targetCombStrength *= saturate((10.0 * dist) / _FurGroomBrushRadius);
					if (_FurCombingMode == 4) targetCombStrength *= saturate((6.0 * dist) / _FurGroomBrushRadius);

					// Determine the target direction
					float2 targetCombDirection = normalize(comb1.xy - comb2.xy);
					if (_FurBrushMode == 3) targetCombDirection = normalize(maskCol.xy * 2 - 1);// Copy from mask
					if (_FurCombingMode == 1) targetCombDirection = normalize(preEdit.xy * 2 - 1);// Strength only
					if (_FurCombingMode == 3) targetCombDirection = normalize(comb1.xy);// Pinch fur together
					if (_FurCombingMode == 4) targetCombDirection = -normalize(comb1.xy);// Repel fur apart

					// Combine the two to get a target vector
					float2 targetVector = targetCombDirection * targetCombStrength;
					float2 preEditVector = preEdit.xy * 2 - 1;
					float2 currentEditVector = col.xy * 2 - 1;
					combVector = (targetVector * fade) + (preEditVector * (1 - fade));

					if (_FurCombingMode == 2) // Direction only
					{
						combVector = normalize(combVector) * length(preEditVector);
					}

					if (length(combVector) > 1) combVector = normalize(combVector);
					combVector = (comb1.a + comb2.a >= 2) && _FurGroomFurCombingEnabled ? combVector : currentEditVector;

                    combVector = combVector * 0.5 + 0.5;
				}

				// Apply length to the blue channel
				float targetHeight = _FurBrushMode == 3 ? maskCol.b : _FurGroomFurHeight;
				float furLength = _FurGroomFurHeightEnabled ? (targetHeight * fade) + (preEdit.b * (1 - fade)) : col.b;
				if (col.b > preEdit.b) furLength = max(col.b, furLength);
				if (col.b < preEdit.b) furLength = min(col.b, furLength);
				//furLength = round(furLength * 64) * 0.015625;
				//col.b = round(col.b * 64) * 0.015625;

				if (_FurGroomFurDensitySetAll < 1)
				{
					targetDensity = _FurGroomFurDensityEnabled ? ((targetDensity * fade) + (preEdit.a * (1 - fade))) : col.a;
					if (col.a > preEdit.a) targetDensity = max(col.a, targetDensity);
					if (col.a < preEdit.a) targetDensity = min(col.a, targetDensity);
				}

				// If we are outside the cursor, discard the changes
				finalCol = dist < _FurGroomBrushRadius ? float4(combVector, furLength, targetDensity) : col;

				// Check if we should "Set All"
				finalCol.rg = _FurGroomFurCombingSetAll > 0 ? (_FurBrushMode == 3 ? maskCol.bg : (normalize(finalCol.rg * 2 - 1) * _FurGroomFurCombing) * 0.5 + 0.5) : finalCol.rg;
				finalCol.b = _FurGroomFurHeightSetAll > 0 ? (_FurBrushMode == 3 ? maskCol.b : _FurGroomFurHeight) : finalCol.b;
				finalCol.a = _FurGroomFurDensitySetAll > 0 ? (_FurBrushMode == 3 ? maskCol.a : targetDensity) : finalCol.a;

				// If the mask is active, possibly discard the changes
				if (_FurLengthMode == 1 && maskCol.b < _FurMinHeight) finalCol = col;
				if (_FurLengthMode == 2 && maskCol.b >= _FurMinHeight) finalCol = col;

				// Check the Increase/Decrease mode to see if we should discard the changes
				if (_FurBrushMode == 1) // Increase only
				{
					finalCol.ba = max(finalCol.ba, preEdit.ba);
					if (length(finalCol.rg * 2 - 1) < (length(preEdit.rg * 2 - 1) - 0.01)) finalCol.rg = col.rg;
				}
				if (_FurBrushMode == 2) // Decrease only
				{
					finalCol.ba = min(finalCol.ba, preEdit.ba);
					if (length(finalCol.rg * 2 - 1) > (length(preEdit.rg * 2 - 1) + 0.01)) finalCol.rg = col.rg;
				}

				return(float4(finalCol.xyz, max(finalCol.a, 0.0196078)));// Enforce a minimum density of (5 / 255), so that we have a way to tell later if a pixel has been rendered or not
			}

		ENDCG
		}
	}
}