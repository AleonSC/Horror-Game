// A visible beam of light: additive, fog-free glow on a long cone. uv.y runs from the source (0) to the
// far end (1); the beam is brightest along its core and fades at its edges and toward the end.
// Works in both the Built-in pipeline and URP. Lives in a Resources folder so it's always included in builds.
Shader "Horror/Beam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.8, 0.75, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 view : TEXCOORD2;
            };

            v2f vert(float4 vertex : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.uv = uv;
                o.normal = UnityObjectToWorldNormal(normal);
                o.view = normalize(WorldSpaceViewDir(vertex));
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Soft edges: faces seen edge-on (the beam's outline) fade out.
                float facing = abs(dot(normalize(i.normal), normalize(i.view)));
                float core = facing * facing;
                float along = lerp(1.0, 0.35, i.uv.y);
                return float4(_Color.rgb * core * along * _Color.a, 1);
            }
            ENDCG
        }
    }
}
