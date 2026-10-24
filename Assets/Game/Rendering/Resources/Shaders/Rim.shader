// An outline of light (section 4.4): drawn on a copy of a model, pushed out a little along its normals, glowing
// only where its surface turns away from you, so the shape is traced in light round its edges. Added to what's
// behind, fading into the fog. For pickups (the batteries' green).
Shader "Game/Rim"
{
    Properties
    {
        [HDR] _GlowColor ("Colour", Color) = (0.25, 1, 0.35, 1)
        _Thickness ("Pushed out by (metres)", Range(0, 0.05)) = 0.006
        _Power ("Edge tightness", Range(0.5, 8)) = 2.5
        _FogAmount ("How much the fog dims it", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+8" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Horror.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float _Thickness;
                float _Power;
                float _FogAmount;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionRWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionRWS = TransformObjectToWorld(input.positionOS) + o.normalWS * _Thickness;
                o.positionCS = TransformWorldToHClip(o.positionRWS);
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 toCamera = normalize(-input.positionRWS);
                float rim = pow(1.0 - saturate(abs(dot(normalize(input.normalWS), toCamera))), _Power);
                float depth = length(input.positionRWS);
                float fog = HorrorFogTransmittance(depth, _FogAmount);
                return float4(HorrorExposed(_GlowColor.rgb) * rim * fog, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
