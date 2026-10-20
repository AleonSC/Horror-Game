// A solid shape seen through haze: its colour is blended toward the haze colour by _Haze, more so low
// down (_GroundHaze, from _HazeBase up through _HazeHeight in world space: mist rising off the ground),
// with slow drifting wisps of mist across it so its outline never reads as a clean cutout.
// Ignores the scene fog (it does its own). Works in both the Built-in pipeline and URP.
// Lives in a Resources folder so it's always included in builds.
Shader "Horror/Haze"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0, 0, 0, 1)
        _HazeColor ("Haze Color", Color) = (0.02, 0.025, 0.035, 1)
        _Haze ("Haze", Range(0, 1)) = 0.5
        _GroundHaze ("Ground Haze", Range(0, 1)) = 0.35
        _HazeBase ("Haze Base (world y)", Float) = 0
        _HazeHeight ("Haze Height", Float) = 100
        _WispScale ("Wisp Scale", Float) = 0.01
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color, _HazeColor;
            float _Haze, _GroundHaze, _HazeBase, _HazeHeight, _WispScale;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
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

            float4 frag(v2f i) : SV_Target
            {
                // Mist thickest near the ground, thinning upward.
                float up = saturate((i.world.y - _HazeBase) / max(_HazeHeight, 0.001));
                float ground = (1 - up) * (1 - up) * _GroundHaze;

                // Slow wisps drifting sideways across it.
                float2 p = float2(i.world.x + i.world.z, i.world.y) * _WispScale + float2(_Time.y * 0.03, _Time.y * 0.01);
                float wisps = noise(p) * 0.6 + noise(p * 2.7 + 5.3) * 0.4;

                float h = saturate(_Haze + ground + (wisps - 0.5) * 0.35);
                return float4(lerp(_Color.rgb, _HazeColor.rgb, h), 1);
            }
            ENDCG
        }
    }
}
