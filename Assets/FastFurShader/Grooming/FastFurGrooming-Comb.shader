Shader "Warren's Fast Fur/Internal Utilities/Comb"
{
    Properties
    {
        _DirectionTex("Direction Map", 2D) = "white" {}
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

            #pragma target 4.6
            #pragma require geometry

            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            float4x4 _FurMeshMatrix;
            sampler2D _DirectionTex;
            float4 _FurGroomMouseHit;
            int _SelectedUV = 0;

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
                centroid half4 pos : SV_POSITION;
                centroid half2 uv : TEXCOORD0;
                centroid half3 normal : TEXCOORD1;
                centroid half3 tangent : TEXCOORD2;
                centroid half3 bitangent : TEXCOORD3;
            };

            v2f vert(meshData v)
            {
                v2f o;

                float2 uv = _SelectedUV == 3 ? v.uv3 : _SelectedUV == 2 ? v.uv2 : _SelectedUV == 1 ? v.uv1 : v.uv0;
                o.uv = uv;

                o.normal = normalize(mul(_FurMeshMatrix, v.normal));
                o.tangent = normalize(mul(_FurMeshMatrix, v.tangent.xyz));
                o.bitangent = cross(o.normal, o.tangent) * v.tangent.w * unity_WorldTransformParams.w;

                // The geometry shader will take care of this
                o.pos = float4(0, 0, 0, 0);

                return o;
            }


            // The geometry shader is needed in order to handle triangles whose UVs are outside of the unit square.
            [maxvertexcount(12)]
            void geom(triangle v2f IN[3], inout TriangleStream<v2f> tristream)
            {
                v2f o = (v2f)0;

                // Check if the UVs need to be re-mapped
                float2 maxPos = max(max(IN[0].uv, IN[1].uv), IN[2].uv);
                float2 offset = float2(0,0);
                if(maxPos.x < 0 || maxPos.x > 1) offset.x = -floor(maxPos.x);
                if(maxPos.y < 0 || maxPos.y > 1) offset.y = -floor(maxPos.y);

                // Check if the UVs are still outside of the unit square
                bool outOfBounds = false;
                for (int i = 0; i < 3; i++)
                {
                    if(any((IN[i].uv + offset) < 0)) outOfBounds = true;
                }

                for (int xOffset = 0 ; xOffset < (outOfBounds ? 1 : 2) ; xOffset++)
                {
                    for (int yOffset = 0 ; yOffset < (outOfBounds ? 1 : 2) ; yOffset++)
                    {
                        for (i = 0; i < 3; i++)
                        {
                            o.uv = IN[i].uv + offset + float2(xOffset, yOffset);
                            // We're rendering to a flat UV mapped texture, not the screen, so we need to pass the UV map coordinates instead of the 3D vertex position
                            float x = o.uv.x * 2 - 1;
                            float y = (1 - o.uv.y) * 2 - 1;
                            o.pos = float4(x, y, 0, 1);

                            o.normal = IN[i].normal;
                            o.tangent = IN[i].tangent;
                            o.bitangent = IN[i].bitangent;

                            tristream.Append(o);
                        }

                        tristream.RestartStrip();
                    }
                }
            }


            fixed4 frag(v2f i) : SV_Target
            {
                float3 worldVector = tex2D(_DirectionTex, i.uv).xyz;

                // Re-map the vector from worldspace to tangent space, since the end-goal of all these calculations is to get positions on the surface
                float3 localVector;
                localVector.x = dot(worldVector, i.tangent);
                localVector.y = dot(worldVector, i.bitangent);
                localVector.z = dot(worldVector, i.normal); // We don't actually need this, since we just want to know x-y cursor movement

                return(float4(localVector.xyz, 1));
            }

            ENDCG
        }
    }
}
