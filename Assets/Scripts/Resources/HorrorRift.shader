// The face of a rift (RiftEvent): a ragged, shifting oval that shows a camera's render texture in screen
// space, so it's a hole through to somewhere else. The view swirls a little toward the edge, darkens just
// inside it, and the edge itself burns a cold glow. Drawn on a mesh whose object-space x and y are the
// oval's (radius 1 is the edge before its ragged wobble). Unlit and fog-free. Works in both the Built-in
// pipeline and URP. Lives in a Resources folder so it's always included in builds.
Shader "Horror/Rift"
{
    Properties
    {
        _MainTex ("View", 2D) = "black" {}
        [HDR] _RimColor ("Rim", Color) = (1.4, 1.2, 2.2, 1)
        _Flicker ("Flicker", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" }
        Pass
        {
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _RimColor;
            float _Flicker;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 local : TEXCOORD1;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                o.local = vertex.xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.local;
                float r = length(p);
                float angle = atan2(p.y, p.x);
                float t = _Time.y;
                float wobble = 0.05 * sin(angle * 5.0 + t * 2.3) + 0.035 * sin(angle * 9.0 - t * 3.1) + 0.02 * sin(angle * 17.0 + t * 5.3);
                float edge = r / (1.0 + wobble);
                clip(1.0 - edge);

                // Swirling toward the edge.
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float2 tangent = r > 0.0001 ? float2(-p.y, p.x) / r : float2(0, 0);
                float swirl = smoothstep(0.45, 1.0, edge);
                uv += tangent * swirl * swirl * 0.025 * sin(edge * 26.0 - t * 4.0);
                float3 view = tex2D(_MainTex, uv).rgb;

                view *= 1.0 - 0.6 * smoothstep(0.62, 0.9, edge); // a dark band just inside the rim
                float rim = smoothstep(0.84, 1.0, edge);
                float3 col = lerp(view, _RimColor.rgb * _Flicker, rim * rim);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
