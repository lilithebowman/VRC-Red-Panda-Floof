Shader "Warren's Fast Fur/Internal Utilities/Stencil"
{
    Properties
    {
        //[HideInInspector]
        _MainTex("Fur Depth and Combing Map", 2D) = "white" {}
		_FurShapeMap("Original Fur Depth and Combing Map", 2D) = "grey" {}
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

                #pragma vertex vert
                #pragma fragment frag

                texture2D _MainTex;
                float4 _MainTex_TexelSize;
                texture2D _FurShapeMap;

                struct meshData
                {
                    float4 vertex : POSITION;
	                float2 uv : TEXCOORD0;
                };

                struct v2f {
                    centroid float4 pos : SV_POSITION;
                    centroid float2 uv : TEXCOORD0;
                };

                v2f vert(meshData v)
                {
                    v2f o;
                    o.pos = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    int4 uvInt = int4(floor(frac(i.uv) * _MainTex_TexelSize.zw), 0, 0);
	                float4 col = _MainTex.Load(uvInt);

                    if(col.a > 0.0098) return(col); // ie. greater than (2.5 / 255)

                    return(_FurShapeMap.Load(uvInt));
                }

                ENDCG
            }
        }
}
