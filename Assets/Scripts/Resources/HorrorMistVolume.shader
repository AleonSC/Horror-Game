// Ground mist as a real volume: drawn on the inside of a big box round the player, each pixel marches a ray
// from the camera through the box, adding up mist from a tiling 3D noise texture, thick low down and thinning
// with height above the ground (read from _MistMap: R = ground height, G = how much mist gathers there), and
// stops where the ray hits the ground or a tree (the camera depth texture). The mist is lit by the
// flashlight's cone and a few nearby point lights (lamps, torches), so beams and halos show in it.
// Premultiplied alpha. Works in URP (the camera must have its depth texture on: GroundMist does that).
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
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler3D _Noise;
            sampler2D _MistMap;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            float4 _MapRect;            // xy: world xz at uv 0; zw: size in metres
            float4 _BoxMin, _BoxMax;    // the box, world space
            float4 _MistColor;          // its own faint colour in the dark
            float _Density, _MistHeight, _NoiseScale, _MaxDistance, _Patchiness;
            float4 _Wind;               // how far the noise has drifted
            float4 _SpotPos;            // xyz position, w range
            float4 _SpotDir;            // xyz direction, w cos(outer half-angle)
            float4 _SpotColor;          // rgb colour * strength, w cos(inner half-angle)
            float4 _PointPos[4];        // xyz position, w range
            float4 _PointColor[4];      // rgb colour * strength
            float _PointCount;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float4 screen : TEXCOORD1;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float Density(float3 p)
            {
                float2 uv = (p.xz - _MapRect.xy) / _MapRect.zw;
                float4 m = tex2Dlod(_MistMap, float4(uv, 0, 0));
                float h = p.y - m.r;
                // Thick on the ground, thinning away above it; none under it.
                float height = exp(-max(h, 0) / _MistHeight) * saturate(h * 4 + 1);
                // Billowing: two scales of drifting noise, gathered into thicker patches where the map says.
                float n1 = tex3Dlod(_Noise, float4(p * _NoiseScale + _Wind.xyz, 0)).r;
                float n2 = tex3Dlod(_Noise, float4(p * _NoiseScale * 3.1 + _Wind.xyz * 1.7, 0)).r;
                float shape = saturate((n1 * 0.7 + n2 * 0.3) * 1.8 - 0.6 + m.g * 0.55);
                return shape * lerp(_Patchiness, 1, m.g) * height * _Density;
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
                return c;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 ro = _WorldSpaceCameraPos;
                float3 rd = normalize(i.world - ro);

                // How far the ray gets before it hits something solid.
                float2 suv = i.screen.xy / i.screen.w;
                float eye = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, suv));
                float3 fwd = -UNITY_MATRIX_V[2].xyz;
                float sceneDist = eye / max(dot(rd, fwd), 0.001);

                // Where it's inside the box.
                float3 safe = rd + (float3)(rd >= 0) * 2e-5 - 1e-5; // (never exactly 0)
                float3 t0 = (_BoxMin.xyz - ro) / safe, t1 = (_BoxMax.xyz - ro) / safe;
                float3 tmin = min(t0, t1), tmax = max(t0, t1);
                float enter = max(max(tmin.x, tmin.y), max(tmin.z, 0));
                float leave = min(min(tmax.x, tmax.y), tmax.z);
                leave = min(leave, min(sceneDist, _MaxDistance));
                if (leave <= enter) return 0;

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
                    float dens = Density(p) * saturate((_MaxDistance - t) / 15);
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
            ENDCG
        }
    }
}
