// The night sky on a dome around the camera: twinkling stars and slow-drifting clouds that blot them out.
// Unlit and fog-free, and it lights nothing: it's painted over the background colour, fading out toward
// the horizon where the fog is. HDRP (see HorrorHDRP.hlsl).
// Lives in a Resources folder so it's always included in builds.
Shader "Horror/Night Sky"
{
    Properties
    {
        _StarBrightness ("Star Brightness", Float) = 1
        _CloudColor ("Cloud Color", Color) = (0.05, 0.055, 0.065, 1)
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.5
        _CloudOffset ("Cloud Offset", Vector) = (0, 0, 0, 0)
        _Flash ("Lightning Flash", Range(0, 1)) = 0
        _FlashColor ("Flash Color", Color) = (0.6, 0.65, 0.85, 1)
        _FlashDir ("Flash Direction", Vector) = (0, 1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-99" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            ZWrite Off
            Cull Front
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "HorrorHDRP.hlsl"

            float _StarBrightness;
            float4 _CloudColor;
            float _CloudCover;
            float4 _CloudOffset;
            float _Flash;
            float4 _FlashColor;
            float4 _FlashDir;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = HorrorObjectToClip(vertex.xyz);
                o.dir = HorrorWorldVector(vertex.xyz);
                return o;
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 hash33(float3 p)
            {
                return float3(hash31(p), hash31(p + 17.1), hash31(p + 41.7));
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0, amp = 0.5;
                for (int i = 0; i < 5; i++) { v += noise(p) * amp; p = p * 2.03 + 7.1; amp *= 0.5; }
                return v;
            }

            // One layer of stars: a few cells of a 3D grid over the sky hold a star each.
            float stars(float3 dir, float density, float chance, float size)
            {
                float3 p = dir * density;
                float3 cell = floor(p);
                float h = hash31(cell);
                if (h > chance) return 0;
                float3 star = cell + 0.25 + 0.5 * hash33(cell + 3.7);
                float d = length(p - star);
                float twinkle = 0.6 + 0.4 * sin(_Time.y * (1.5 + h * 40) + h * 100);
                float bright = 0.4 + 0.6 * frac(h * 13.7);
                return saturate(1 - d / size) * bright * twinkle;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float up = dir.y;
                float horizon = saturate((up - 0.02) / 0.25); // fade into the fog near the horizon

                float s = stars(dir, 160, 0.12, 0.09) + stars(dir, 320, 0.2, 0.08) * 0.6 + stars(dir, 70, 0.04, 0.07) * 1.6;
                float3 starColor = lerp(float3(0.75, 0.82, 1), float3(1, 0.92, 0.8), hash31(floor(dir * 160))) * s * _StarBrightness;

                // Clouds on a flat ceiling high above, drifting.
                float2 uv = dir.xz / (up + 0.12) * 0.6 + _CloudOffset.xy;
                float n = fbm(uv);
                float cloud = smoothstep(1 - _CloudCover, 1 - _CloudCover + 0.25, n);
                float3 cloudColor = _CloudColor.rgb * (0.7 + 0.6 * fbm(uv * 2.3 + 5)); // a little lumpy shading

                float3 color = lerp(starColor, cloudColor, cloud);
                float alpha = saturate(max(s * (1 - cloud), cloud * 0.95)) * horizon;

                // Lightning: the clouds light up from within, brightest toward the bolt.
                if (_Flash > 0.001)
                {
                    float toward = pow(saturate(dot(dir, normalize(_FlashDir.xyz)) * 0.5 + 0.5), 4);
                    float lit = _Flash * (0.15 + 0.85 * toward);
                    float low = saturate((up + 0.08) / 0.3); // reaches right down to the treeline
                    color += _FlashColor.rgb * lit * (0.3 + cloud * 1.2);
                    alpha = max(alpha, lit * (0.35 + 0.6 * cloud) * low);
                }
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
