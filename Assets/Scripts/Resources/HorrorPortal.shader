// The view through the door: shows a camera's render texture in screen space, so the doorway acts as
// a window into the other place. Unlit and fog-free (the other camera's picture already has fog).
// Works in both the Built-in pipeline and URP. Lives in a Resources folder so it's always included in builds.
Shader "Horror/Portal"
{
    Properties
    {
        _MainTex ("View", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.screenPos.xy / i.screenPos.w);
            }
            ENDCG
        }
    }
}
