// Flat colour that ignores fog. Used for things that must read from far away through the fog
// (the eye in the sky, the giant). HDRP (see HorrorHDRP.hlsl).
// Lives in a Resources folder so it's always included in builds.
Shader "Horror/Unlit No Fog"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" "Queue" = "Transparent-10" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            ZWrite [_ZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "HorrorHDRP.hlsl"

            float4 _Color;

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return HorrorObjectToClip(vertex.xyz);
            }

            float4 frag() : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
