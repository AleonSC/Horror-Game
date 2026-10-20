// Flat colour that ignores fog. Used for things that must read from far away through the fog
// (the eye in the sky, the giant). Works in both the Built-in pipeline and URP.
// Lives in a Resources folder so it's always included in builds.
Shader "Horror/Unlit No Fog"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            ZWrite [_ZWrite]
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            float4 frag() : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
