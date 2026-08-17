// Zweckgebauter Biege-Shader für Sprites mit Wurzel-Kante an einem Rand (links oder rechts, siehe
// _PivotU) -- der Biegewinkel wächst LINEAR mit dem horizontalen UV-Abstand von dieser Kante, komplett
// unabhängig von der Höhe (V), damit eine durchgehende senkrechte Wurzel-Kante (nicht nur ein Punkt)
// als Ganzes starr bleibt und nur die äußere Fläche als EIN zusammenhängendes Stück mitbiegt.
// Pixelweise berechnet (Fragment-Shader), dadurch weich/kontinuierlich statt in starren Segmenten
// geknickt -- kein fremdes Uber-Shader-Paket, keine versteckten Clamps, keine Ambient-Wind-Interferenz.
Shader "NeonSmash/SpriteBend"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BendAngle ("Bend Angle (Grad, am äußersten Rand)", Range(-90, 90)) = 0
        _PivotU ("Wurzel-Kante (0 = links, 1 = rechts)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
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
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _BendAngle;
            float _PivotU;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // KEINE Rotation der UV-Ebene (das wäre eine seitliche 2D-Scherung, links/rechts) --
                // stattdessen eine reine horizontale STAUCHUNG Richtung Wurzel-Kante, die eine zusätzliche
                // Y-Achsen-Rotation (Foreshortening, vorn/hinten) simuliert. cos(theta) ist an der Wurzel
                // (theta=0) exakt 1 (kein Effekt) und nimmt mit dem Abstand ab -- betrifft NUR die
                // X-Koordinate, die Y-Koordinate bleibt unangetastet. Dadurch wirkt der Effekt automatisch
                // an JEDEM Punkt fern der Wurzel-Kante gleich stark, egal ob oben oder unten (Spitzen).
                float d = i.uv.x - _PivotU;
                float theta = radians(_BendAngle) * abs(d);
                float squish = cos(theta);
                float2 bentUV = float2(_PivotU + d * squish, i.uv.y);

                fixed4 col = tex2D(_MainTex, bentUV) * i.color;
                return col;
            }
            ENDCG
        }
    }
}
