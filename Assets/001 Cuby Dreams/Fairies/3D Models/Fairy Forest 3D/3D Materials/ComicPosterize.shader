Shader "Custom/ComicPosterize"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Saturation ("Saturation", Range(0, 3)) = 1.6
        _Contrast ("Contrast", Range(0, 3)) = 1.35
        _PosterizeLevels ("Posterize Levels", Range(2, 12)) = 4
        _PosterizeAmount ("Posterize Amount", Range(0, 1)) = 1.0
        _ToonSteps ("Toon Light Steps", Range(1, 4)) = 2
        _ShadowTint ("Shadow Tint", Range(0, 1)) = 0.6
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
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float2 uv          : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Saturation;
                float _Contrast;
                float _PosterizeLevels;
                float _PosterizeAmount;
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
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half3 AdjustSaturation(half3 color, float saturation)
            {
                half luma = dot(color, half3(0.299, 0.587, 0.114));
                return lerp(half3(luma, luma, luma), color, saturation);
            }

            half3 AdjustContrast(half3 color, float contrast)
            {
                return saturate((color - 0.5) * contrast + 0.5);
            }

            half3 Posterize(half3 color, float levels)
            {
                return floor(color * levels) / max(levels - 1.0, 1.0);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = texColor.rgb * _BaseColor.rgb;

                // Stylize the base color: saturation -> contrast -> posterize
                half3 stylized = AdjustSaturation(albedo, _Saturation);
                stylized = AdjustContrast(stylized, _Contrast);
                half3 posterized = Posterize(stylized, _PosterizeLevels);
                half3 finalAlbedo = lerp(stylized, posterized, _PosterizeAmount);

                // Simple toon-banded lighting with ambient fallback (avoids pure-black in unlit scenes)
                half3 ambient = SampleSH(normalWS);

                Light mainLight = GetMainLight();
                float NdotL = dot(normalWS, mainLight.direction);
                float litStep = saturate(ceil(saturate(NdotL) * _ToonSteps) / _ToonSteps);
                float toonShade = lerp(_ShadowTint, 1.0, litStep);

                half3 directLight = mainLight.color * toonShade;
                half3 totalLight = max(ambient, directLight);

                half3 finalColor = finalAlbedo * totalLight;

                return half4(finalColor, texColor.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
