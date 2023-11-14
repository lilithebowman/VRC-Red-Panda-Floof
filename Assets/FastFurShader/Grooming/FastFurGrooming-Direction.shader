Shader "Warren's Fast Fur/Internal Utilities/Direction"
{
    Properties
    {
        _FurGroomMouseHit("Mouse Hit Location", Vector) = (0,0,0,0)
        _FurMirror("Brush Mirror", int) = 1
        _FurMirrorX("Brush Mirror X Offset", float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Cull Off
        ZTest Off


        // The purpose of this shader is to create a map that contains the distance and direction of the
        // mouse cursor for every part of the avatar. This shader requires the output of the "Distance"
        // shader as its input texture. That way it can check if the pixel it is working on is the closest
        // occurance of that uv map pixel.


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

            sampler2D _MainTex;
            float4x4 _FurMeshMatrix;
            float4 _FurGroomMouseHit;
            int _FurMirror;
            float _FurMirrorX;
            int _SelectedUV = 0;

            struct meshData
            {
                float4 vertex : POSITION;
	            float2 uv0 : TEXCOORD0;
	            float2 uv1 : TEXCOORD1;
	            float2 uv2 : TEXCOORD2;
	            float2 uv3 : TEXCOORD3;
            };

            struct v2f {
                centroid float4 pos : SV_POSITION;
                centroid float2 uv : TEXCOORD0;
                centroid float3 worldPos : TEXCOORD1;
            };

            v2f vert(meshData v)
            {
                v2f o;

                float2 uv = _SelectedUV == 3 ? v.uv3 : _SelectedUV == 2 ? v.uv2 : _SelectedUV == 1 ? v.uv1 : v.uv0;
                o.uv = uv;

                // The geometry shader will take care of this
                o.pos = float4(0, 0, 0, 0);

                // We still need the world position though, so that we can tell if the mouse pointer is nearby
                o.worldPos = mul(_FurMeshMatrix, v.vertex);

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

                            o.worldPos = IN[i].worldPos;

                            tristream.Append(o);
                        }

                        tristream.RestartStrip();
                    }
                }
            }


            fixed4 frag(v2f i) : SV_Target
            {
                if(isinf(_FurGroomMouseHit.x)) return(float4(0,0,0,1e30));

                float3 direction = _FurGroomMouseHit.xyz - i.worldPos;
                float3 dirMirror = float3((_FurMirrorX - _FurGroomMouseHit.x) + _FurMirrorX, _FurGroomMouseHit.yz) - i.worldPos;

                float distMouse = distance(_FurGroomMouseHit.xyz, i.worldPos) * 200;
                float distMirror = distance(float3((_FurMirrorX - _FurGroomMouseHit.x) + _FurMirrorX, _FurGroomMouseHit.yz), i.worldPos) * 200;

                // Don't output anything if we aren't the closest occurance of this particular pixel
                float closest = tex2D(_MainTex, i.uv).r;
                float dist = distMouse;
                if(_FurMirror > 0 && distMirror < distMouse) dist = distMirror;

                if(closest < dist) discard;
                
                if(_FurMirror > 0 && distMirror < distMouse) return(float4(dirMirror, -distMirror));
                return(float4(direction, distMouse));
            }

            ENDCG
        }
    }
}
