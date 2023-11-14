Shader "Warren's Fast Fur/Internal Utilities/Closest"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Cull Off
        ZTest Off

        // The purpose of this shader is to keep track of the closest that a particular pixel has been
        // to the centre of the cursor since the mouse button was pressed. This buffers changes made
        // near the centre of the cursor, and prevents the outer edge of the cursor from overwriting
        // those changes as the cursor moves away.

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            struct meshData
            {
                float4 vertex : POSITION;
	            float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                centroid float4 pos : SV_POSITION;
	            centroid float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _ClosestTex;

            v2f vert(meshData v)
            {
                v2f o;

                o.uv = v.uv;

                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float closest = min(tex2D(_MainTex, i.uv).r, tex2D(_ClosestTex, i.uv).r);
                return(float4(closest,0,0,1));
            }
            ENDCG
        }
    }
}
