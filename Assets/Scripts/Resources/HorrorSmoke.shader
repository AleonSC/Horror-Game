// Wisps of smoke on camera-facing quads. Each puff is a ragged, billowing cloud of fractal noise that
// slowly churns, rather than a round blob; dense in the middle, frayed and see-through at the edges.
// Ignores fog, so smoke columns can be seen rising far off across the forest. Alpha-blended.
// Per-puff colour/opacity (_Color) and a random _Seed come from a MaterialPropertyBlock.
// Works in both the Built-in pipeline and URP. Lives in a Resources folder so it's always included in builds.
Shader "Horror/Smoke"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.3, 0.3, 0.3)
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
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
            float _Seed;

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

            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0, amp = 0.5;
                for (int i = 0; i < 4; i++) { v += noise(p) * amp; p = p * 2.1 + 3.7; amp *= 0.5; }
                return v;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float t = _Time.y * 0.12;
                float2 offset = float2(_Seed * 7.13, _Seed * 3.71);

                // Domain warp: the cloud folds over itself as it churns.
                float2 q = float2(fbm(p * 1.6 + offset + t), fbm(p * 1.6 + offset - t + 5.2));
                float n = fbm(p * 2.2 + q * 1.4 + offset);

                // Ragged edge: the noise eats into the outline.
                float r = length(p);
                float body = saturate(1 - smoothstep(0.25, 1.0, r + (0.5 - n) * 0.75));
                float density = body * saturate(n * 1.6 - 0.15);

                // A little shading: thicker parts darker underneath, thin wisps paler.
                float3 color = _Color.rgb * lerp(0.75, 1.2, saturate(n + p.y * 0.25));
                return float4(color, density * _Color.a);
            }
            ENDCG
        }
    }
}
