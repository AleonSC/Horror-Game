// A flame on a quad: additive, fog-free, animated. uv (0,0) is the bottom left, the flame rising from the
// bottom middle. A teardrop of fire, torn and licking upward with scrolling noise, white-yellow at its hot
// core, orange, then red and transparent at its ragged edges and tip. _Seed varies each flame.
// HDRP (see HorrorHDRP.hlsl). Lives in a Resources folder so it's always included in builds.
Shader "Horror/Flame"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 0.55, 0.15, 1)
        _Intensity ("Intensity", Float) = 2
        _Seed ("Seed", Float) = 0
        _Speed ("Speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "HorrorHDRP.hlsl"

            float4 _Color;
            float _Intensity, _Seed, _Speed;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.pos = HorrorObjectToClip(vertex.xyz);
                o.uv = uv;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            float fbm(float2 p)
            {
                float s = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { s += noise(p) * a; p *= 2.03; a *= 0.5; }
                return s;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y * _Speed + _Seed;

                // Turbulence: the flame sways and tears, more the higher it goes.
                float n = fbm(float2(uv.x * 3.0 + _Seed, uv.y * 2.5 - t * 2.2));
                float x = (uv.x - 0.5) + (n - 0.5) * 0.4 * uv.y;
                float y = uv.y;

                // A teardrop: rounded at the bottom, narrowing to a point at the top.
                float width = 0.42 * pow(saturate(1.0 - y), 0.85) * smoothstep(0.0, 0.22, y + 0.04);
                float body = saturate(1.0 - abs(x) / max(width, 0.001));

                // Licking tongues: noise rising through it eats away the edges and the tip.
                float lick = fbm(float2(uv.x * 5.0 + 3.0 + _Seed, uv.y * 4.0 - t * 3.1));
                float shape = body * smoothstep(0.0, 0.55, lick + 0.6 - y * 0.75);
                shape = saturate(shape * 1.7);

                // Hot at the base of the core, cooling outward and upward.
                float heat = shape * (1.0 - y * 0.65);
                float3 col = lerp(float3(0.55, 0.04, 0.0), _Color.rgb, smoothstep(0.1, 0.5, heat));
                col = lerp(col, float3(1.0, 0.93, 0.72), smoothstep(0.55, 0.95, heat));
                return float4(col * shape * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
