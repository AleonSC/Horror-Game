// Soft round puffs of smoke on camera-facing quads. Ignores fog, so smoke columns can be seen
// rising far off across the forest. Alpha-blended; per-puff colour and opacity come from _Color
// (set with a MaterialPropertyBlock). Works in both the Built-in pipeline and URP.
// Lives in a Resources folder so it's always included in builds.
Shader "Horror/Smoke"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.3, 0.3, 0.3)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.uv = uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r2 = dot(p, p);
                // Soft-edged blob, slightly lumpy.
                float lumps = 0.85 + 0.15 * sin(p.x * 7.0 + p.y * 5.0) * sin(p.y * 6.0 - p.x * 3.0);
                float a = saturate(1 - r2) * saturate(1 - r2) * lumps;
                return float4(_Color.rgb, a * _Color.a);
            }
            ENDCG
        }
    }
}
