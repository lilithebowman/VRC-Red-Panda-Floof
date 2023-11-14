Shader "Warren's Fast Fur/Internal Utilities/Fix Edges"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Cull Off
        ZTest Off

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

            SamplerState my_point_repeat_sampler;
            UNITY_DECLARE_TEX2D(_MainTex);
            float4 _MainTex_TexelSize;

            v2f vert(meshData v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // sample the texture
                float4 col = _MainTex.SampleLevel(my_point_repeat_sampler, i.uv, 0);
                if (col.a < 0.0098) {// ie. less than (2.5 / 255)
                    col.a = 0.0;
                    float4 texR = _MainTex.SampleLevel(my_point_repeat_sampler, i.uv + float2(_MainTex_TexelSize.x, 0),0);
                    float4 texL = _MainTex.SampleLevel(my_point_repeat_sampler, i.uv - float2(_MainTex_TexelSize.x, 0),0);
                    float4 texU = _MainTex.SampleLevel(my_point_repeat_sampler, i.uv + float2(0, _MainTex_TexelSize.y),0);
                    float4 texD = _MainTex.SampleLevel(my_point_repeat_sampler, i.uv - float2(0, _MainTex_TexelSize.y),0);
                    texR *= (texR.a > 0.0098 ? 1 : 0);
                    texL *= (texL.a > 0.0098 ? 1 : 0);
                    texU *= (texU.a > 0.0098 ? 1 : 0);
                    texD *= (texD.a > 0.0098 ? 1 : 0);
                    float valid = (texR.a > 0.0098 ? 1 : 0) + (texL.a > 0.0098 ? 1 : 0) + (texU.a > 0.0098 ? 1 : 0) + (texD.a > 0.0098 ? 1 : 0);
                    if (valid > 0) {
                        col = (texR + texL + texU + texD) / valid;
                        col.a = max(col.a, 0.0196078); // Make sure the density is at least (5 / 255)
                    }
                }
                else col.a = max(col.a, 0.0196078); // Make sure the density is at least (5 / 255)
                return col;
            }
            ENDCG
        }
    }
}
