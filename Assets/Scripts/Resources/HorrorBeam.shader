// A visible beam of light, drawn as a soft shaft rather than a cone's surface: a strip along the beam's axis
// that turns about the axis to face the camera wherever you are, brightest down its middle and fading to
// nothing at its sides, along its length and at its far end. It melts into whatever it meets (the ground,
// a trunk) instead of cutting through it with a hard line, using the camera's depth texture, and it fades
// away when you look straight along it or stand inside it (then you see the light on the ground, not a
// sheet of beam). Additive, fog-free.
//
// The object is a cone as before: apex at its origin, axis +z, radius 1 at z = 1, scaled by its transform.
// Use it with WatcherEvent.BuildBeamMesh (a strip with uv.x across, -1 to 1, and uv.y along, 0 to 1).
// Works in URP (with the camera's depth texture on; GroundMist turns it on) and the Built-in pipeline.
// Lives in a Resources folder so it's always included in builds.
Shader "Horror/Beam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.8, 0.75, 1)
        _FadeIn ("Fade In From Source (0 = off)", Float) = 0
        _FadeOut ("Fade Out Toward The End (0 = off)", Float) = 0
        _Softness ("Soft Edge Where It Meets Things (m)", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _FadeIn, _FadeOut, _Softness;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screen : TEXCOORD1;
                float3 world : TEXCOORD2;
                float eye : TEXCOORD3;
                float viewAxis : TEXCOORD4; // how nearly you're looking straight along the beam (0-1)
                float inside : TEXCOORD5;   // how far inside the beam the camera is (0-1)
            };

            v2f vert(float4 vertex : POSITION, float2 uv : TEXCOORD0)
            {
                v2f o;
                float3 apex = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 tip = mul(unity_ObjectToWorld, float4(0, 0, 1, 1)).xyz;
                float3 axisVec = tip - apex;
                float len = max(length(axisVec), 0.0001);
                float3 axis = axisVec / len;
                float radiusEnd = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));

                float t = uv.y;
                float3 p = apex + axisVec * t;
                float radius = lerp(0.03, 1.0, t) * radiusEnd;

                // Turned about its axis to face the camera.
                float3 toCam = _WorldSpaceCameraPos - p;
                float3 side = cross(axis, toCam);
                float sideLen = length(side);
                side = sideLen > 0.0001 ? side / sideLen : normalize(cross(axis, abs(axis.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 world = p + side * (radius * 1.2 * uv.x);

                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
                o.uv = uv;
                o.screen = ComputeScreenPos(o.pos);
                o.world = world;
                o.eye = -mul(UNITY_MATRIX_V, float4(world, 1)).z;
                o.viewAxis = abs(dot(axis, normalize(toCam)));

                // Is the camera inside the cone?
                float3 c = _WorldSpaceCameraPos - apex;
                float along = dot(c, axis) / len;
                float3 off = c - axis * dot(c, axis);
                float radiusThere = lerp(0.03, 1.0, saturate(along)) * radiusEnd;
                float within = 1.0 - smoothstep(0.6, 1.3, length(off) / max(radiusThere, 0.0001));
                o.inside = within * smoothstep(-0.02, 0.05, along) * (1.0 - smoothstep(0.95, 1.1, along));
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Across: brightest down the middle, nothing at the sides.
                float across = saturate(1.0 - i.uv.x * i.uv.x);
                float core = across * across;

                // Along: dimming as it spreads, optionally fading in from the source and out before its end,
                // and always softly dying away over its last stretch (never a hard rim).
                float t = i.uv.y;
                float along = lerp(1.0, 0.35, t);
                along *= _FadeIn > 0.0001 ? smoothstep(0.0, _FadeIn, t) : 1.0;
                along *= _FadeOut > 0.0001 ? 1.0 - smoothstep(1.0 - _FadeOut, 1.0, t) : 1.0;
                along *= 1.0 - smoothstep(0.8, 1.0, t);
                along *= smoothstep(0.0, 0.02, t);

                // Where it meets the ground (or anything): melting into it, no hard line.
                float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                float soft = saturate((scene - i.eye) / max(_Softness, 0.01));

                // Looking straight along it (it'd be a flat ribbon edge-on), or standing in it: it fades away,
                // leaving the light it throws. Close by the camera, too.
                float axisFade = 1.0 - smoothstep(0.8, 0.98, i.viewAxis);
                float insideFade = 1.0 - 0.85 * i.inside;
                float near = saturate((distance(i.world, _WorldSpaceCameraPos) - 0.5) / 5.0);

                float a = core * along * soft * axisFade * insideFade * near * _Color.a * 1.4;
                return float4(_Color.rgb * a, 1);
            }
            ENDCG
        }
    }
}
