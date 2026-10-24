// A silhouette that fades into the sky as it climbs (section 6.11): the giant's chains. Like the HDRP Unlit
// silhouette it stands in for (a colour and how much of what's behind it hides, untouched by the fog), but
// thinning out between two heights, so a chain rising into the dark is gone before it ever ends.
Shader "Game/SkyFade"
{
    Properties
    {
        _UnlitColor ("Colour (alpha: how much it hides)", Color) = (0, 0, 0, 1)
        _FadeStart ("Starts fading at this height (world metres)", Float) = 0
        _FadeEnd ("Gone by this height (world metres)", Float) = 100
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Horror.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _UnlitColor;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float height : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 rws = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(rws);
                o.height = GetAbsolutePositionWS(rws).y;
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float fade = 1.0 - smoothstep(_FadeStart, max(_FadeEnd, _FadeStart + 0.01), input.height);
                return float4(HorrorExposed(_UnlitColor.rgb), _UnlitColor.a * fade);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
