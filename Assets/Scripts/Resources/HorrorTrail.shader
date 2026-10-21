// A glowing trail (a LineRenderer or TrailRenderer, tiled UVs: uv.x runs along it, uv.y across): additive and
// fog-free, a soft glowing band with a bright thread down its middle and faint streaks flowing along it,
// shimmering. Vertex colour alpha fades it. Used for the flare's trail of fire.
// Works in both the Built-in pipeline and URP. Lives in a Resources folder so it's always included in builds.
Shader "Horror/Trail"
{
    Properties
    {
        _Color ("Colour", Color) = (0.55, 0.85, 1, 1)
        _Intensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _Intensity;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(float4 vertex : POSITION, float2 uv : TEXCOORD0, float4 color : COLOR)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.uv = uv;
                o.color = color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float across = abs(i.uv.y - 0.5) * 2.0;
                float band = pow(saturate(1.0 - across), 1.6);
                float thread = pow(saturate(1.0 - across), 7.0);
                // Streaks drifting along it, the way you went.
                float flow = frac(i.uv.x * 0.3 - _Time.y * 0.45);
                float streak = smoothstep(0.0, 0.07, flow) * pow(1.0 - flow, 3.0);
                float shimmer = 0.75 + 0.25 * sin(i.uv.x * 2.1 + _Time.y * 3.0) * sin(i.uv.x * 0.7 - _Time.y * 1.3);
                float b = (band * 0.3 + thread * 0.7 + streak * band * 1.3) * shimmer;
                return float4(_Color.rgb * b * _Intensity * i.color.a, 1);
            }
            ENDCG
        }
    }
}
