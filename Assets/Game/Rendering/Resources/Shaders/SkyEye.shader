// The eye in the sky (section 6.11): a vast, dull, living eye opening in a swirl of cloud. A mauve-brown iris of
// fine radial fibres with a paler ring round the pupil and a dark rim, a glint low in the pupil, the white barely
// showing; lids that close over it from above and below; and round it, the clouds drawn into rings, lit warm
// from underneath near the eye and dark blue-grey further out, with the fold of a lid arcing pale above it,
// thinning out into the sky at the edge. All drawn on one quad (-0.5..0.5), untouched by the fog.
Shader "Game/SkyEye"
{
    Properties
    {
        _Open ("Open (0 shut, 1 wide)", Range(0, 1)) = 1
        _Look ("Where the pupil looks (x, y: share of the eye's width)", Vector) = (0, 0, 0, 0)
        _Pupil ("Pupil size (share of the iris)", Range(0.1, 0.9)) = 0.36
        _Bright ("Brightness", Float) = 1
        _Cloud ("How much cloud swirls round it (0: none, nothing shows but the open eye)", Range(0, 1)) = 1
        _Spin ("How fast the clouds turn", Float) = 0.02
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Horror.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Open;
                float4 _Look;
                float _Pupil;
                float _Bright;
                float _Cloud;
                float _Spin;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                // Drawn at the far end of the sky, whatever distance its quad is at: everything in front of it (the
                // giant, its chain, the trees) hides it, as the sky would be hidden.
                #if UNITY_REVERSED_Z
                o.positionCS.z = o.positionCS.w * 1e-6;
                #else
                o.positionCS.z = o.positionCS.w * (1.0 - 1e-6);
                #endif
                o.uv = input.uv * 2.0 - 1.0;
                return o;
            }

            float Hash2(float2 p) { return frac(sin(p.x * 127.1 + p.y * 311.7) * 43758.5453); }

            float Noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = p - i;
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash2(i), b = Hash2(i + float2(1, 0)), c = Hash2(i + float2(0, 1)), d = Hash2(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p, int octaves)
            {
                float v = 0.0, a = 0.5;
                for (int i = 0; i < octaves; i++)
                {
                    v += a * Noise2(p);
                    p = p * 2.03 + float2(1.7, 9.2);
                    a *= 0.5;
                }
                return v;
            }

            // Noise round a circle with no seam where the angle wraps: the two ends blended across it.
            float FbmRound(float angle, float k, float r, int octaves)
            {
                float t = (angle + PI) / (2.0 * PI);
                return lerp(Fbm(float2(angle * k, r), octaves), Fbm(float2((angle - 2.0 * PI) * k, r), octaves), t);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                const float A = 0.36; // the opening's half-width, as a share of the quad's
                float2 uv = input.uv;
                float x = uv.x / A, y = uv.y / A;
                float o = _Open;

                // The lids: a high arch above, flatter below, the corners a little tilted.
                float w = saturate(1.0 - x * x);
                float tilt = 0.06 * x * o;
                float yt = 0.78 * pow(w, 0.62) * o + tilt - (1.0 - o) * 0.08;
                float yb = -0.50 * pow(w, 0.85) * o + tilt - (1.0 - o) * 0.08;
                float inside = smoothstep(0.0, 0.025, yt - y) * smoothstep(0.0, 0.025, y - yb) * smoothstep(1.0, 0.97, abs(x));

                // The ball: the white, the iris, the pupil.
                const float R = 0.66;
                float2 iv = float2(x, y) - _Look.xy;
                float r = length(iv) / R;
                float ang = atan2(iv.y, iv.x);
                float3 white = lerp(float3(0.30, 0.22, 0.23), float3(0.42, 0.33, 0.33), w) * (0.75 + 0.25 * Fbm(float2(x, y) * 6.0, 3));
                float fib = FbmRound(ang, 7.6, r * 2.5, 4);     // radial fibres
                float fib2 = FbmRound(ang, 19.0, r * 6.0, 3);
                float3 iris = lerp(float3(0.30, 0.18, 0.18), float3(0.60, 0.42, 0.40), saturate(fib * 1.3 - 0.15));
                iris *= 0.8 + 0.4 * fib2;
                iris = lerp(iris, float3(0.78, 0.58, 0.52), smoothstep(0.62, 0.5, r) * smoothstep(0.32, 0.45, r) * 0.55 * (0.7 + 0.6 * fib2)); // the paler ring
                iris *= 1.0 - 0.7 * smoothstep(0.82, 1.0, r);                                      // the dark rim
                float3 ball = lerp(iris, white, smoothstep(0.98, 1.03, r));
                float pr = r / _Pupil;
                ball *= smoothstep(0.9, 1.15, pr);
                // A glint, low in the pupil.
                float2 g = iv / (R * _Pupil) + float2(0.0, 0.5);
                ball += float3(0.95, 0.88, 0.78) * 0.9 * smoothstep(0.5, 0.35, abs(g.x)) * smoothstep(0.2, 0.1, abs(g.y));
                // In the shadow of the upper lid.
                ball *= 0.45 + 0.55 * smoothstep(0.0, 0.32, yt - y);
                ball *= 0.75 + 0.25 * smoothstep(0.0, 0.15, y - yb);

                // The clouds round it, drawn into rings (wider than tall), slowly turning.
                float ex = uv.x / A, ey = uv.y / A * 1.45;
                float rr = length(float2(ex, ey));
                float th = atan2(ey, ex);
                float sw = th + 0.35 * sin(rr * 1.3) + _TimeParameters.x * _Spin / max(rr, 0.5);
                sw = sw - 2.0 * PI * floor((sw + PI) / (2.0 * PI)); // (back into -pi..pi)
                float warp = Fbm(float2(cos(sw), sin(sw)) * 1.5 + float2(3.0, 0.0), 3);
                float band = FbmRound(sw, 1.6, rr * 5.0 + warp * 2.0, 5);
                float streak = FbmRound(sw, 2.5, rr * 16.0 + warp * 3.0, 3);
                float c = saturate(band * 1.6 - 0.35) * 0.8 + saturate(streak * 1.6 - 0.5) * 0.5;
                float near = exp(-max(rr - 1.0, 0.0) * 0.7);
                float3 cloud = lerp(float3(0.07, 0.08, 0.11), float3(0.46, 0.34, 0.31), near * near) * (0.45 + 0.9 * c);
                cloud *= 1.0 + 0.9 * smoothstep(0.3, -2.0, y) * exp(-max(rr - 1.0, 0.0) * 0.5); // lit from below
                // The fold of the lid, arcing pale and violet above it; and the wet rim of the opening.
                float cw = saturate(1.0 - (x / 1.35) * (x / 1.35));
                float creaseY = 0.78 * pow(cw, 0.6) + 0.30 + 0.04 * x + 0.04 * (Fbm(float2(x * 3.0, y * 1.5), 2) - 0.5);
                cloud += float3(0.40, 0.30, 0.42) * smoothstep(0.16, 0.0, abs(y - creaseY)) * pow(cw, 0.7) * (0.6 + 0.6 * streak);
                cloud += float3(0.35, 0.25, 0.27) * 0.6 * smoothstep(0.06, 0.0, abs(y - yt)) * smoothstep(1.0, 0.7, abs(x)) * step(0.02, o);

                float3 rgb = lerp(cloud, ball, inside);
                // Solid round the eye, breaking up into cloud toward the edge of the quad (and, with less cloud,
                // close in round the eye).
                float q = length(float2(uv.x, uv.y * 1.25));
                float reach = max(0.001, lerp(0.0, 0.98, _Cloud));
                float alpha = smoothstep(reach, reach * 0.56, q + 0.25 * (band - 0.5));
                alpha = max(alpha, inside);
                rgb = pow(max(rgb, 0.0), 2.2); // (its colours were picked as they look on screen)
                return float4(rgb * _Bright * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
