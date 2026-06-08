Shader "NeonSmash/BurnFuse"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _BurnAmount ("Burn Amount (0-1)", Range(0, 1)) = 1.0
        _BurnSoftness ("Burn Softness", Range(0, 0.5)) = 0.1
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
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 centered : TEXCOORD1;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _Color;
            float _BurnAmount;
            float _BurnSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.centered = v.uv - float2(0.5, 0.5);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);

                // Radiale Distanz vom Center
                float dist = length(i.centered);

                // Burn-Threshold mit Softness
                float burnThreshold = _BurnAmount * 0.707; // 0.707 ≈ diagonal
                float burnEdge = smoothstep(burnThreshold + _BurnSoftness, burnThreshold - _BurnSoftness, dist);

                // Alpha kombinieren
                fixed4 col = texColor * _Color;
                col.a *= burnEdge * texColor.a;

                return col;
            }
            ENDCG
        }
    }
}
