Shader "Custom/OutlineVertexMasked"
{
    // Inverted-Hull-Outline wie Gritline/Outline_BackFaceCull, aber mit einer zusaetzlichen
    // Vertex-Color-Maske: Vertex-Farbe (roter Kanal) steuert PRO VERTEX, wie stark der
    // Kanten-Versatz ausfaellt. Weiss (1) = normale Outline-Dicke, Schwarz (0) = keine Outline.
    // So bleibt die Kontur ueberall exakt so dick wie gewuenscht, verschwindet aber gezielt an
    // bemalten Stellen (Augen, Mund, Nase) -- ohne die Geometrie selbst anzufassen und ohne den
    // geteilten Gritline-Shader zu veraendern (der bleibt fuer alle anderen Charaktere unberuehrt).
    Properties
    {
        _Color ("Color", Color) = (0.02, 0.02, 0.03, 1)
        _OutlineThickness ("Outline Thickness (Objektraum)", Range(0, 0.05)) = 0.012
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _OutlineThickness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float mask = IN.color.r; // von Vertex-Painting in Blender: 1 = volle Dicke, 0 = keine Outline
                float3 posOS = IN.positionOS.xyz + normalize(IN.normalOS) * (_OutlineThickness * mask);
                OUT.positionHCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
