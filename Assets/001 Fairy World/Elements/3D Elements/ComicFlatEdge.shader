Shader "Custom/ComicFlatEdge"
{
    // Neue Variante neben Custom/ComicPosterize (bewusst nicht ueberschrieben):
    // statt Multi-Ton-Shading anhand von UV/Palette-Textur nur EINE flache Fuellfarbe
    // fuer die gesamte Oberflaeche, plus eine per Fresnel/Rim-Term eingeblendete
    // Kantenfarbe (blickwinkelabhaengig, unabhaengig von Facettenzahl/Topologie der
    // jeweiligen Mesh-Instanz -> macht alle Elemente optisch einheitlich).
    Properties
    {
        _BaseColor ("Fill Color", Color) = (0.90, 0.76, 0.92, 1)
        _EdgeColor ("Edge Color", Color) = (0.08, 0.04, 0.12, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.0
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.35
        _ToonSteps ("Toon Light Steps", Range(1, 4)) = 2
        _ShadowTint ("Shadow Tint", Range(0, 1)) = 0.75
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float _RimPower;
                float _RimThreshold;
                float _ToonSteps;
                float _ShadowTint;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS = normInputs.normalWS;
                OUT.positionWS = posInputs.positionWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Fresnel/Rim-Term: je flacher der Blickwinkel zur Oberflaeche
                // (Silhouette/Kante), desto staerker faerbt sich die Kantenfarbe ein.
                float NdotV = saturate(dot(normalWS, viewDir));
                float rim = pow(1.0 - NdotV, _RimPower);
                float edgeMask = smoothstep(_RimThreshold, 1.0, rim);

                half3 albedo = lerp(_BaseColor.rgb, _EdgeColor.rgb, edgeMask);

                // Einfaches Toon-Banding wie im ComicPosterize-Shader, mit Ambient-Fallback
                half3 ambient = SampleSH(normalWS);

                Light mainLight = GetMainLight();
                float NdotL = dot(normalWS, mainLight.direction);
                float litStep = saturate(ceil(saturate(NdotL) * _ToonSteps) / _ToonSteps);
                float toonShade = lerp(_ShadowTint, 1.0, litStep);

                half3 directLight = mainLight.color * toonShade;
                half3 totalLight = max(ambient, directLight);

                half3 finalColor = albedo * totalLight;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
