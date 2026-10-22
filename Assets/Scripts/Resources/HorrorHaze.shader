// A solid shape seen through haze: its colour is blended toward the haze colour by _Haze, more so low
// down (_GroundHaze, from _HazeBase up through _HazeHeight in world space: mist rising off the ground),
// with slow drifting wisps of mist across it so its outline never reads as a clean cutout.
// Ignores the scene fog (it does its own). HDRP (see HorrorHDRP.hlsl).
//
// Far-off shapes in the fog (the giant, its chains, the landmark tree) are drawn "fog aware"
// (MaterialFactory.MakeFogAware): rather than painting over the fog that's already there, they add the
// difference between themselves and the plain fog colour. So light in the mist in front of them (your
// flashlight's beam) stays on top, instead of being cut out in a black silhouette. They also mark the
// stencil, so the stars aren't drawn over them.
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
        _SkyFade ("Sky Fade", Range(0, 1)) = 0
        _SkyFadeStart ("Sky Fade Start (world y)", Float) = 1000
        _SkyFadeLength ("Sky Fade Length", Float) = 1000
        [HideInInspector] _FogAware ("Fog Aware", Float) = 0
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" "Queue" = "Transparent-10" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            Stencil
            {
                Ref [_StencilRef]
                WriteMask 64 // (HDRP's first user bit)
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "HorrorHDRP.hlsl"

            float4 _Color, _HazeColor;
            float _Haze, _GroundHaze, _HazeBase, _HazeHeight, _WispScale;
            float _SkyFade, _SkyFadeStart, _SkyFadeLength;
            float _FogAware;

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

                // Things reaching up into the sky (the chains) melt into it as they rise.
                float sky = smoothstep(0, 1, saturate((i.world.y - _SkyFadeStart) / max(_SkyFadeLength, 0.001))) * _SkyFade;

                float h = saturate(_Haze + ground + (wisps - 0.5) * 0.35 + sky);
                float3 col = lerp(_Color.rgb, _HazeColor.rgb, h);
                // Fog aware: added onto the fog already there, as the change from plain fog (often negative).
                return _FogAware > 0.5 ? float4(col - _HazeColor.rgb, 1) : float4(col, 1);
            }
            ENDHLSL
        }
    }
}
