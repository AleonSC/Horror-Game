// A scour's mouth: its throat, lips and fangs, lit from inside. Unlit and fog-free. Each vertex's colour is
// its own shade (dark flesh, ivory fangs) and its alpha how much of the glow from down its throat reaches
// it (_Color, the burning light, set as the mouth opens). Faces are shaded a little by how they turn toward
// the glow, so the fangs read as solid shapes against it. HDRP (see HorrorHDRP.hlsl).
// Lives in a Resources folder so it's always included in builds.
Shader "Horror/Throat"
{
    Properties
    {
        [HDR] _Color ("Glow", Color) = (4, 0.3, 0.2, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" "Queue" = "Transparent-10" }
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

            float4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float shade : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION, float3 normal : NORMAL, float4 color : COLOR)
            {
                v2f o;
                o.pos = HorrorObjectToClip(vertex.xyz);
                o.color = color;
                // Facing the glow deep in the throat (behind, along -z) catches more of it.
                o.shade = 0.55 + 0.45 * saturate(dot(normalize(normal), float3(0, 0, -1)) * 0.5 + 0.5);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 col = i.color.rgb * i.shade + _Color.rgb * i.color.a * i.shade;
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
