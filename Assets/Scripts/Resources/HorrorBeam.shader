// A visible beam of light: additive, fog-free glow on a long cone. uv.y runs from the source (0) to the
// far end (1); the beam is brightest along its core and fades at its edges and toward the end.
// Works in both the Built-in pipeline and URP. Lives in a Resources folder so it's always included in builds.
Shader "Horror/Beam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.8, 0.75, 1)
        _FadeIn ("Fade In From Source (0 = off)", Float) = 0
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
            float _FadeIn;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 view : TEXCOORD2;
                float3 world : TEXCOORD3;
            };

            v2f vert(float4 vertex : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.uv = uv;
                o.normal = UnityObjectToWorldNormal(normal);
                o.view = normalize(WorldSpaceViewDir(vertex));
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Soft edges: faces seen edge-on (the beam's outline) fade out.
                float facing = abs(dot(normalize(i.normal), normalize(i.view)));
                float core = facing * facing;
                float along = lerp(1.0, 0.35, i.uv.y);
                // Optionally faded in from nothing at the source (a column of light hanging out of the dark).
                along *= _FadeIn > 0.0001 ? smoothstep(0.0, _FadeIn, i.uv.y) : 1.0;

                // Seen from inside the cone, its walls face you everywhere and would wash the whole view out
                // in a flat sheet with a hard edge. Inside, you see the light on the ground, not the beam: fade
                // it out the deeper inside you are. (The cone's apex is at the origin, axis +z, radius 1 at z = 1.)
                float3 cam = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float radial = length(cam.xy) / max(cam.z, 0.0001);
                float insideAlong = smoothstep(0.0, 0.05, cam.z) * (1.0 - smoothstep(0.95, 1.1, cam.z));
                float inside = insideAlong * (1.0 - smoothstep(0.55, 1.15, radial));
                float visible = 1.0 - 0.95 * inside;

                // And no hard line where the cone's surface passes close by the camera.
                float near = saturate((distance(i.world, _WorldSpaceCameraPos) - 1.0) / 8.0);

                return float4(_Color.rgb * core * along * _Color.a * visible * near, 1);
            }
            ENDCG
        }
    }
}
