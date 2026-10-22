// The view through the door: shows a camera's render texture in screen space, so the doorway acts as
// a window into the other place. Unlit and fog-free (the other camera's picture already has fog).
// HDRP (see HorrorHDRP.hlsl). Lives in a Resources folder so it's always included in builds.
Shader "Horror/Portal"
{
    Properties
    {
        _MainTex ("View", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "HorrorHDRP.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return HorrorObjectToClip(vertex.xyz);
            }

            float4 frag(float4 pos : SV_POSITION) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, HorrorScreenUV(pos));
            }
            ENDHLSL
        }
    }
}
