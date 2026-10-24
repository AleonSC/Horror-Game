// Fire (section 4.4): a flame on an upright quad that turns about its own up to face you (pivot at its base),
// licking and flickering with drifting noise: white-blue at the root, yellow, orange, a red tip that breaks up.
// For the torch, the lighter, the flare and campfires.
Shader "Game/Flame"
{
    Properties
    {
        [HDR] _GlowColor ("Brightness and tint", Color) = (3, 3, 3, 1)
        _Speed ("How fast it licks", Range(0.2, 6)) = 2.2
        _Wobble ("How much it wavers", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+12" "IgnoreProjector" = "True" }

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
                float _Speed;
                float _Wobble;
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
                float seed : TEXCOORD1;
                float depth : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 centre = HorrorObjectCentreRWS();
                float3 upAxis = float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y);
                float height = length(upAxis);
                float width = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float3 up = upAxis / max(height, 1e-5);
                float3 toCamera = normalize(-centre);
                float3 right = cross(up, toCamera);
                right = dot(right, right) > 1e-6 ? normalize(right) : float3(1, 0, 0);
                float3 p = centre + right * input.positionOS.x * width + up * (input.positionOS.y + 0.5) * height;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = input.uv;
                float3 absolute = GetAbsolutePositionWS(centre);
                o.seed = frac(sin(dot(absolute, float3(12.9898, 78.233, 37.719))) * 43758.5453) * 50.0;
                o.depth = -TransformWorldToView(centre).z;
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _TimeParameters.x * _Speed;
                float y = input.uv.y;
                float x = input.uv.x * 2.0 - 1.0;
                // Wavering more toward the top.
                float n1 = HorrorNoise(float3(input.uv.x * 3.0, y * 2.5 - t, input.seed));
                float n2 = HorrorNoise(float3(input.uv.x * 7.0, y * 6.0 - t * 1.7, input.seed + 9.0));
                x += (n1 - 0.5) * _Wobble * y * 1.6;
                // A teardrop: round at the root, drawn to a point.
                float w = 0.62 * sin(3.14159 * pow(saturate(y), 0.55)) * (1.0 - y * 0.35);
                float body = saturate((w - abs(x)) / max(w * 0.55, 0.001));
                float tip = saturate(1.0 - y * 1.05 + (n2 - 0.5) * 0.6);
                float mask = body * smoothstep(0.0, 0.35, tip) * smoothstep(0.0, 0.06, y);
                // Hot at the root and the core, cooling up and out.
                float heat = saturate(mask * (1.25 - y));
                float3 c = lerp(float3(0.55, 0.06, 0.01), float3(1.0, 0.42, 0.08), saturate(heat * 1.6));
                c = lerp(c, float3(1.0, 0.86, 0.45), saturate(heat * 2.0 - 0.6));
                c = lerp(c, float3(0.75, 0.82, 1.0), saturate((0.18 - y) * 5.0) * body * 0.6); // (the blue root)
                float flicker = 0.85 + 0.15 * HorrorNoise(float3(t * 3.0, input.seed, 0.0));
                float near = saturate(input.depth / 0.15);
                return float4(HorrorExposed(_GlowColor.rgb) * c * mask * flicker * near, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
