// A soft glow (section 4.4): a camera-facing disc of light, brightest in the middle, added to what's behind.
// For eyes, bulbs, embers and pickups. It fades where it meets surfaces (no hard edge where it cuts into the
// ground) and into the fog by distance (Fog Amount 0: it shines through the fog, like the scours' eyes).
Shader "Game/Glow"
{
    Properties
    {
        [HDR] _GlowColor ("Colour", Color) = (1, 0.6, 0.3, 1)
        _Falloff ("Falloff (higher: tighter core)", Range(0.5, 8)) = 2.5
        _Soft ("Soft edge into surfaces (metres)", Range(0.01, 5)) = 0.4
        _FogAmount ("How much the fog dims it", Range(0, 1)) = 1
        _NearFade ("Fade when closer than (metres)", Range(0, 5)) = 0.3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

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
                float _Falloff;
                float _Soft;
                float _FogAmount;
                float _NearFade;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float depth : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // A quad (-0.5..0.5) turned to face the camera, as big as the object's scale.
                float3 centre = HorrorObjectCentreRWS();
                float size = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 p = centre + (right * input.positionOS.x + up * input.positionOS.y) * size;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = input.uv;
                o.depth = -TransformWorldToView(centre).z;
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float d = length(input.uv * 2.0 - 1.0);
                float shape = pow(saturate(1.0 - d), _Falloff);
                float scene = HorrorSceneDepth(input.positionCS);
                float soft = saturate((scene - input.depth) / max(_Soft, 0.001) + 0.5);
                float near = saturate(input.depth / max(_NearFade, 0.001));
                float fog = HorrorFogTransmittance(input.depth, _FogAmount);
                float3 c = HorrorExposed(_GlowColor.rgb) * shape * soft * near * fog;
                return float4(c, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
