// A soft shaft of light (section 4.4): drawn on a cone (apex at the light, along +Z, length 1, radius 1 at the
// far end, scaled to fit), brightest along its middle and fading to nothing at its edges, along its length, where
// it cuts into surfaces, and into the fog. Dust drifts in it. For floodlights, searchlights and glares.
Shader "Game/Beam"
{
    Properties
    {
        [HDR] _GlowColor ("Colour", Color) = (1, 0.9, 0.75, 1)
        _EdgeSoftness ("Edge softness (higher: thinner core)", Range(0.5, 8)) = 2.5
        _Falloff ("Fade along it", Range(0.2, 6)) = 1.6
        _Soft ("Soft edge into surfaces (metres)", Range(0.05, 10)) = 1.5
        _Dust ("Dust in it", Range(0, 1)) = 0.35
        _FogAmount ("How much the fog dims it", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+5" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Horror.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float _EdgeSoftness;
                float _Falloff;
                float _Soft;
                float _Dust;
                float _FogAmount;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionRWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float along : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionRWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionRWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.along = input.uv.y;
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 toCamera = normalize(-input.positionRWS);
                // Facing you: the middle of the shaft. Edge on: its rim, which melts away.
                float core = pow(saturate(abs(dot(normalize(input.normalWS), toCamera))), _EdgeSoftness);
                float along = saturate(input.along);
                float length = pow(saturate(1.0 - along), _Falloff) * smoothstep(0.0, 0.04, along);
                float3 world = GetAbsolutePositionWS(input.positionRWS);
                float dust = lerp(1.0, 0.4 + 1.2 * HorrorNoise(world * 0.7 + float3(0.0, _TimeParameters.x * 0.25, _TimeParameters.x * 0.1)), _Dust);
                float depth = -TransformWorldToView(input.positionRWS).z;
                float soft = saturate((HorrorSceneDepth(input.positionCS) - depth) / max(_Soft, 0.01));
                float near = saturate(depth / 1.5); // (no blinding wall of light when it's right on you)
                float fog = HorrorFogTransmittance(depth, _FogAmount);
                float3 c = HorrorExposed(_GlowColor.rgb) * core * length * dust * soft * near * fog;
                return float4(c, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
