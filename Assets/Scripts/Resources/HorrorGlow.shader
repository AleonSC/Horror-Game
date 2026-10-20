// A soft round glow on a quad: additive, fog-free, brightest in the middle and fading smoothly to nothing
// at the edge. Used for the halo round things you can pick up. Works in both the Built-in pipeline and URP.
// Lives in a Resources folder so it's always included in builds.
Shader "Horror/Glow"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
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
            float _Intensity;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.uv = uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = pow(saturate(1.0 - d), 2.2);
                return float4(_Color.rgb * a * _Intensity, 1);
            }
            ENDCG
        }
    }
}
