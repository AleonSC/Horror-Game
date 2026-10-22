// Ground mist as a real volume: drawn on the inside of a big box round the player, each pixel marches a ray
// from the camera through the box, adding up mist from a tiling 3D noise texture, thick low down and thinning
// with height above the ground (read from _MistMap: R = ground height, G = how much mist gathers there), and
// stops where the ray hits the ground or a tree (the camera depth texture). The mist is lit by the
// flashlight's cone and a few nearby point lights (lamps, torches), so beams and halos show in it.
// Premultiplied alpha. HDRP (see HorrorHDRP.hlsl); everything here is in true world space.
// Lives in a Resources folder so it's always included in builds.
Shader "Horror/MistVolume"
{
    Properties
    {
        _Noise ("Noise (3D)", 3D) = "" {}
        _MistMap ("Mist Map", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-20" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "HorrorHDRP.hlsl"

            TEXTURE3D(_Noise);
            SAMPLER(sampler_Noise);
            TEXTURE2D(_MistMap);
            SAMPLER(sampler_MistMap);

            float4 _MapRect;            // xy: world xz at uv 0; zw: size in metres
            float4 _BoxMin, _BoxMax;    // the box, world space
            float4 _MistColor;          // its own faint colour in the dark
            float _Density, _MistHeight, _NoiseScale, _MaxDistance, _Patchiness;
            float4 _NearClear;          // x: within this distance of you it's thinnest, y: by this it's full, z: how thin (0-1)
            float4 _Wind;               // how far the noise has drifted
            float4 _SpotPos;            // xyz position, w range
            float4 _SpotDir;            // xyz direction, w cos(outer half-angle)
            float4 _SpotColor;          // rgb colour * strength, w cos(inner half-angle)
            float4 _PointPos[4];        // xyz position, w range
            float4 _PointColor[4];      // rgb colour * strength
            float _PointCount;
            float4 _ClearPos[4];        // the nearest lamps: xyz position, w 1 if used
            float4 _LampClear;          // x: clearest within this distance, y: back to full by this, z: how thin (0-1)

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = HorrorObjectToClip(vertex.xyz);
                o.world = HorrorAbsolute(HorrorWorld(vertex.xyz));
                return o;
            }

            float Density(float3 p)
            {
                float2 uv = (p.xz - _MapRect.xy) / _MapRect.zw;
                float4 m = SAMPLE_TEXTURE2D_LOD(_MistMap, sampler_MistMap, uv, 0);
                float h = p.y - m.r;
                // Thick on the ground, thinning away above it; none under it.
                float height = exp(-max(h, 0) / _MistHeight) * saturate(h * 4 + 1);
                // Billowing: two scales of drifting noise, gathered into thicker patches where the map says.
                float n1 = SAMPLE_TEXTURE3D_LOD(_Noise, sampler_Noise, p * _NoiseScale + _Wind.xyz, 0).r;
                float n2 = SAMPLE_TEXTURE3D_LOD(_Noise, sampler_Noise, p * _NoiseScale * 3.1 + _Wind.xyz * 1.7, 0).r;
                float shape = saturate((n1 * 0.7 + n2 * 0.3) * 1.8 - 0.6 + m.g * 0.55);
                // Thinner round the lamps.
                float lamps = 1;
                for (int k = 0; k < 4; k++)
                {
                    if (_ClearPos[k].w < 0.5) continue;
                    float dl = distance(p.xz, _ClearPos[k].xz);
                    lamps = min(lamps, lerp(_LampClear.z, 1, smoothstep(_LampClear.x, _LampClear.y, dl)));
                }
                return shape * lerp(_Patchiness, 1, m.g) * height * _Density * lamps;
            }

            float3 Lighting(float3 p)
            {
                float3 c = _MistColor.rgb;

                // The flashlight's cone, fading with distance.
                float3 toP = p - _SpotPos.xyz;
                float d = length(toP);
                float cone = smoothstep(_SpotDir.w, _SpotColor.w, dot(toP / max(d, 0.001), _SpotDir.xyz));
                float att = saturate(1 - d / max(_SpotPos.w, 0.001));
                c += _SpotColor.rgb * cone * att * att;

                // A glow round each nearby lamp.
                for (int k = 0; k < 4; k++)
                {
                    if (k >= _PointCount) break;
                    float dl = distance(p, _PointPos[k].xyz);
                    float a = saturate(1 - dl / max(_PointPos[k].w, 0.001));
                    c += _PointColor[k].rgb * a * a;
                }
                return min(c, 0.45); // however many lights pile up, lit mist stays mist, never a white blaze
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 ro = HorrorCameraAbsolute();
                float3 rd = normalize(i.world - ro);

                // How far the ray gets before it hits something solid.
                float eye = HorrorSceneEyeDepth(i.pos);
                float3 fwd = -UNITY_MATRIX_V[2].xyz;
                float sceneDist = eye / max(dot(rd, fwd), 0.001);

                // Where it's inside the box.
                float3 safe = rd + (float3)(rd >= 0) * 2e-5 - 1e-5; // (never exactly 0)
                float3 t0 = (_BoxMin.xyz - ro) / safe, t1 = (_BoxMax.xyz - ro) / safe;
                float3 tmin = min(t0, t1), tmax = max(t0, t1);
                float enter = max(max(tmin.x, tmin.y), max(tmin.z, 0));
                float leave = min(min(tmax.x, tmax.y), tmax.z);
                leave = min(leave, min(sceneDist, _MaxDistance));
                if (leave <= enter) return float4(0, 0, 0, 0);

                const int STEPS = 32;
                float stepLen = (leave - enter) / STEPS;
                // A different offset per pixel, so the steps don't show as bands.
                float jitter = frac(52.9829189 * frac(dot(i.pos.xy, float2(0.06711056, 0.00583715))));
                float t = enter + stepLen * jitter;
                float trans = 1;
                float3 col = 0;
                for (int s = 0; s < STEPS; s++)
                {
                    float3 p = ro + rd * t;
                    // Thin right round you, so the ground and things close by are easy to see; full further off.
                    float dens = Density(p) * saturate((_MaxDistance - t) / 15)
                                 * lerp(_NearClear.z, 1, smoothstep(_NearClear.x, _NearClear.y, t));
                    if (dens > 0.0005)
                    {
                        float a = 1 - exp(-dens * stepLen);
                        col += trans * a * Lighting(p);
                        trans *= 1 - a;
                        if (trans < 0.02) break;
                    }
                    t += stepLen;
                }
                return float4(col, 1 - trans);
            }
            ENDHLSL
        }
    }
}
