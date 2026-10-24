// The night sky (section 4.4): a dome round the camera, drawn after the fog so it shows through it (and behind
// the far silhouettes, the giant first among them). Stars, twinkling; drifting clouds, their edges lit near the
// moon; the moon itself, faint markings on it, a halo, and rays streaming out from round whatever's in front of it.
// Colours here are as they show on screen (the fog's own colour comes from the look).
Shader "Game/NightSky"
{
    Properties
    {
        _MoonDir ("Toward the moon", Vector) = (0, 0.5, 1, 0)
        [HDR] _MoonColor ("Moon colour", Color) = (0.85, 0.88, 0.95, 1)
        _MoonSize ("Moon radius (degrees)", Range(0.5, 20)) = 5
        _MoonGlow ("Moon halo", Range(0, 3)) = 0.6
        _Rays ("Rays", Range(0, 3)) = 0.6
        _RayLength ("Ray length (degrees)", Range(2, 90)) = 28
        _Stars ("Stars", Range(0, 3)) = 0.7
        _StarDensity ("Star density", Range(0, 1)) = 0.5
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.5
        _CloudBrightness ("Cloud brightness", Range(0, 3)) = 1
        _CloudDrift ("Cloud drift (xz)", Vector) = (0.01, 0.004, 0, 0)
        _Horizon ("Fade below this height (0-1)", Range(0, 0.6)) = 0.12
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-50" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Horror.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MoonDir;
                float4 _MoonColor;
                float _MoonSize;
                float _MoonGlow;
                float _Rays;
                float _RayLength;
                float _Stars;
                float _StarDensity;
                float _CloudCover;
                float _CloudBrightness;
                float4 _CloudDrift;
                float _Horizon;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionRWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionRWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionRWS);
                return o;
            }

            // One layer of stars: one per cell of a grid over the sky (if its die roll says so), at a random spot
            // in the cell, a soft point at least a pixel across.
            float Stars(float3 dir, float scale, float chance, float seed)
            {
                float3 p = dir * scale;
                float3 cell = floor(p);
                float h = HorrorHash(cell + seed);
                if (h > chance) return 0.0;
                float3 spot = float3(HorrorHash(cell + seed + 7.1), HorrorHash(cell + seed + 3.3), HorrorHash(cell + seed + 11.7)) * 0.6 + 0.2;
                float d = length(frac(p) - spot);
                float pixel = max(fwidth(p.x), fwidth(p.z)) * 1.2;
                float radius = max(0.04, pixel);
                float core = saturate(1.0 - d / radius);
                float bright = pow(HorrorHash(cell + seed + 1.9), 3.0) * 0.8 + 0.2;
                float twinkle = 0.75 + 0.25 * sin(_TimeParameters.x * (2.0 + 4.0 * h) + h * 50.0);
                return core * core * bright * twinkle * (0.04 / radius);
            }

            float Fbm(float3 p)
            {
                float v = 0.0, a = 0.55;
                for (int i = 0; i < 5; i++)
                {
                    v += HorrorNoise(p) * a;
                    p = p * 2.03 + float3(5.2, 1.3, 7.7);
                    a *= 0.5;
                }
                return v;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.positionRWS);
                float up = saturate(dir.y);
                float horizon = smoothstep(0.0, max(_Horizon, 0.001), dir.y);
                if (horizon <= 0.0) return float4(0, 0, 0, 0);
                float3 moonDir = normalize(_MoonDir.xyz);
                float3 fog = _HorrorFogColor.rgb;

                // The moon: a disc with faint grey seas on it, a soft halo, and rays streaming out.
                float cosA = dot(dir, moonDir);
                float angle = degrees(acos(clamp(cosA, -1.0, 1.0)));
                float disc = saturate((_MoonSize - angle) / max(_MoonSize * 0.04, 0.05));
                float3 local = (dir - moonDir * cosA) / max(radians(_MoonSize), 1e-4);
                float seas = HorrorNoise(local * 2.2 + 3.0) * 0.6 + HorrorNoise(local * 5.0 + 9.0) * 0.4;
                float3 moon = _MoonColor.rgb * disc * (0.78 + 0.22 * smoothstep(0.35, 0.65, seas));
                float halo = _MoonGlow * (exp(-max(angle - _MoonSize, 0.0) / (_MoonSize * 0.6)) * 0.5 + exp(-angle / (_MoonSize * 4.0)) * 0.15);
                // Rays: bright and dark spokes round the moon, slowly turning, fading out along their length.
                float3 side = normalize(cross(moonDir, abs(moonDir.y) < 0.95 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 upAxis = cross(side, moonDir);
                float2 around = float2(dot(dir, side), dot(dir, upAxis));
                float2 spoke = around / max(length(around), 1e-4);
                float t = _TimeParameters.x * 0.02;
                float rays = pow(HorrorNoise(float3(spoke * 9.0, t)), 2.2) * 0.7 + pow(HorrorNoise(float3(spoke * 23.0, t * 1.7 + 4.0)), 3.0) * 0.5;
                float along = max(angle - _MoonSize, 0.0);
                rays *= exp(-along / max(_RayLength, 0.1)) * smoothstep(0.0, _MoonSize * 0.3, along) * (cosA > 0.0 ? 1.0 : 0.0);
                float3 light = moon + _MoonColor.rgb * (halo + rays * _Rays * 0.35);

                // Stars: fewer and dimmer toward the horizon (more air to see through).
                float density = _StarDensity;
                float stars = Stars(dir, 140.0, 0.06 * density, 0.0) * 1.4 + Stars(dir, 300.0, 0.12 * density, 31.0) * 0.6;
                stars *= _Stars * smoothstep(0.05, 0.5, up) * (1.0 - disc);
                float3 starLight = float3(0.85, 0.9, 1.0) * stars;

                // Clouds, drifting: dark against the sky, silver at the edges near the moon.
                float2 plane = dir.xz / max(dir.y + 0.08, 0.08) * 0.35 + _CloudDrift.xy * _TimeParameters.x;
                float n = Fbm(float3(plane, _TimeParameters.x * 0.004));
                float cover = 1.0 - _CloudCover;
                float cloud = smoothstep(cover - 0.08, cover + 0.22, n) * horizon;
                float edge = cloud * (1.0 - smoothstep(cover + 0.05, cover + 0.3, n)); // (the thin edges)
                float nearMoon = pow(saturate(cosA), 6.0);
                float3 cloudColour = fog * (0.55 + 0.25 * up) * _CloudBrightness + _MoonColor.rgb * (nearMoon * (0.15 + 1.2 * edge)) * _CloudBrightness;
                float a = cloud * 0.92;

                // Over the fog's colour behind: the clouds replace it where they are; light shows through thin cloud.
                float3 behind = (light + starLight) * horizon * (1.0 - a * 0.85);
                return float4(cloudColour * a + behind, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
