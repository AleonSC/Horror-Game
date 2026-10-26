// A wall of dust (section 6.11): the giant's landing throwing it up, and the shockwave rolling out from it. Drawn
// on an open ring (its uv.y 0 at the ground, 1 at the top): billowing, torn at the top and thinning into the air,
// soft where it meets the ground, lit faintly in the colour it's given, and hazed by the fog with distance.
Shader "Game/ShockDust"
{
    Properties
    {
        _UnlitColor ("Colour (alpha: how thick)", Color) = (0.2, 0.19, 0.18, 0.6)
        _Billow ("Billow scale", Float) = 24
        _Rise ("How fast it billows upward", Float) = 0.25
        _Soft ("Soft into the ground (metres)", Float) = 6
        _FogAmount ("How much the fog hides it with distance", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One OneMinusSrcAlpha
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
                float _Billow;
                float _Rise;
                float _Soft;
                float _FogAmount;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 around : TEXCOORD0; // (its place round the ring, unscaled: no seam in the noise)
                float height : TEXCOORD1;
                float depth : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.around = float3(input.positionOS.x, 0.0, input.positionOS.z);
                o.height = input.uv.y;
                o.depth = -TransformWorldToView(world).z;
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float h = input.height;
                float t = _TimeParameters.x * _Rise;
                float3 p = input.around * _Billow + float3(0.0, h * 3.0 - t, 0.0);
                float n = HorrorNoise(p) * 0.55 + HorrorNoise(p * 2.1 + 7.3) * 0.3 + HorrorNoise(p * 4.3 + 1.9) * 0.15;
                // Thick low down, torn and thinning toward the top.
                float shape = smoothstep(0.0, 0.06, h) * pow(saturate(1.0 - h), 1.4);
                float a = _UnlitColor.a * shape * saturate(n * 1.8 - 0.35 + (1.0 - h) * 0.5);
                float scene = HorrorSceneDepth(input.positionCS);
                a *= saturate((scene - input.depth) / max(_Soft, 0.01));
                a *= saturate(input.depth / 4.0); // (not a sheet across your eyes as it passes)
                a *= HorrorFogTransmittance(input.depth, _FogAmount);
                float3 c = _UnlitColor.rgb * (0.7 + 0.6 * n);
                return float4(c * a, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
