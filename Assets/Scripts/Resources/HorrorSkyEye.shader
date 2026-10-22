// The giant eye in the sky. Draws an eyeball texture inside an almond-shaped opening whose lids
// open and close (_Open), with the eyeball shifted by the gaze (_Look) and a slit pupil drawn on
// top (_Pupil = width, height). Ignores fog, blends softly at the lid edges. HDRP (see
// HorrorHDRP.hlsl). Lives in a Resources folder so it's always included in builds.
Shader "Horror/Sky Eye"
{
    Properties
    {
        _MainTex ("Eyeball", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1, 1, 1, 1)
        _Open ("Open", Range(0, 1)) = 1
        _Look ("Look offset", Vector) = (0, 0, 0, 0)
        _Pupil ("Pupil size", Vector) = (0.07, 0.3, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-100" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "HorrorHDRP.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;
            float _Open;
            float4 _Look;
            float4 _Pupil;

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
                float2 p = i.uv * 2 - 1;                       // -1..1 across the quad

                // Almond-shaped opening: the lids meet at the corners and part in the middle.
                float lid = 0.5 * (1 - p.x * p.x) * _Open;
                float edge = lid - abs(p.y);
                float mask = saturate(edge / 0.035);

                // The eyeball moves under the lids as it looks around.
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - _Look.xy);

                // Slit pupil, centred on the iris.
                float2 q = (p - _Look.xy * 2) / max(_Pupil.xy, 0.001);
                float pupil = dot(q, q);
                col.rgb *= smoothstep(0.7, 1.3, pupil);

                // Shadow just inside the lids, like lashes and the lid's overhang.
                col.rgb *= saturate(edge * 7 + 0.15);

                return float4(col.rgb * _Color.rgb, mask * _Color.a);
            }
            ENDHLSL
        }
    }
}
