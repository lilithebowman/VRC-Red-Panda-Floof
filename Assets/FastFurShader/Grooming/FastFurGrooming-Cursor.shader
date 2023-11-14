Shader "Warren's Fast Fur/Internal Utilities/Cursor"
{
    Properties
    {
        //[HideInInspector]
        _MainTex("Albedo Map", 2D) = "white" {}
        _DirectionTex("Direction Map", 2D) = "white" {}

        _FurGroomBrushRadius("Brush Radius", float) = 4
        _FurGroomBrushFalloff("Brush Falloff", float) = 0.5
        _FurGroomBrushVisibility("Brush Visibility", float) = 0.5
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

                sampler2D _MainTex;
                sampler2D _DirectionTex;
                float _FurGroomBrushRadius;
                float _FurGroomBrushFalloff;
                float _FurGroomBrushVisibility;

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
                    fixed4 col = tex2D(_MainTex, i.uv);

                    fixed4 dim = fixed4(col.r * sin(_Time.z), col.g * cos(_Time.z), col.b, 1);
                    fixed4 lit = fixed4(sin(_Time.z),cos(_Time.z), 1, 1);

                    fixed4 dimMirror = fixed4(col.r, col.g * sin(_Time.z), col.b * cos(_Time.z), 1);
                    fixed4 litMirror = fixed4(1, sin(_Time.z), cos(_Time.z), 1);

                    float brightness = col.r * 0.2 + col.g * 0.7 + col.b * 0.1;
                    float highlight = saturate(0.1 - brightness) * 10;
                    fixed4 cursorCol = dim + lit * highlight;
                    fixed4 cursorMirrorCol = dimMirror + litMirror * highlight;

                    float dist = tex2D(_DirectionTex, i.uv).a;
                    
                    float fade = reset(_FurGroomBrushRadius, _FurGroomBrushRadius * _FurGroomBrushFalloff * 0.95, 0, 1, abs(dist));
                    fade = smoothstep(1, 0, fade) * _FurGroomBrushVisibility;
                    if(dist >= 0 && dist <= _FurGroomBrushRadius) return((cursorCol * fade) + (col * (1 - fade)));
                    if(dist <  0 && abs(dist) <= _FurGroomBrushRadius) return((cursorMirrorCol * fade) + (col * (1 - fade)));
                    return(col);
                }

                ENDCG
            }
        }
}
