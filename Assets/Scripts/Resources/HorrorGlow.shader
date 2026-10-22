// A soft round glow on a quad: additive, fog-free, brightest in the middle and fading smoothly to nothing
// at the edge. Used for the halo round things you can pick up. HDRP (see HorrorHDRP.hlsl).
// Lives in a Resources folder so it's always included in builds.
Shader "Horror/Glow"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
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
            float _Intensity;

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

            float4 frag(v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = pow(saturate(1.0 - d), 2.2);
                return float4(_Color.rgb * a * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
