Shader "NeonSmash/LineGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.2, 0.8, 1)
        _GlowColor ("Glow Color", Color) = (1, 0.2, 0.8, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            float4 _Color;
            float4 _GlowColor;
            float _GlowIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = _Color * i.color;
                col.rgb = lerp(col.rgb, _GlowColor.rgb, _GlowIntensity * 0.3);
                return col;
            }
            ENDCG
        }
    }
}
