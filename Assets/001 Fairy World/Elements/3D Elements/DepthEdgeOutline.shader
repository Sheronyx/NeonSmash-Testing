Shader "Custom/DepthEdgeOutline"
{
    // Vollbild-Post-Effekt: zeichnet schwarze Konturlinien ueberall dort, wo sich die
    // linearisierte Kameratiefe sprunghaft aendert (Silhouette UND Facetten-/Wuerfelkanten).
    // Wird ueber ein "Full Screen Pass Renderer Feature" der URP eingebunden.
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.03, 0.02, 0.05, 1)
        _DepthThreshold ("Depth Threshold", Range(0.0005, 0.2)) = 0.02
        _EdgeThicknessPx ("Edge Thickness (px)", Range(1, 4)) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "DepthEdgeOutline"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _OutlineColor;
            float _DepthThreshold;
            float _EdgeThicknessPx;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy * _EdgeThicknessPx;

                float rawD0 = SampleSceneDepth(uv + float2(-texel.x, -texel.y));
                float rawD1 = SampleSceneDepth(uv + float2( texel.x,  texel.y));
                float rawD2 = SampleSceneDepth(uv + float2(-texel.x,  texel.y));
                float rawD3 = SampleSceneDepth(uv + float2( texel.x, -texel.y));

                // unity_OrthoParams.w ist 1 fuer orthographische Kameras, 0 fuer perspektivische.
                // Die Spielkamera hier ist orthographisch -- dort ist die Rohtiefe bereits linear,
                // LinearEyeDepth() (fuer perspektivische Kameras gedacht) wuerde falsche/kaum
                // variierende Werte liefern und die Kantenerkennung praktisch stilllegen.
                bool isOrtho = unity_OrthoParams.w > 0.5;
                // Rohtiefe (0..1) auf echte Welteinheiten (near..far) hochskalieren, damit
                // derselbe _DepthThreshold fuer Ortho- und Perspektiv-Kameras sinnvoll bleibt.
                float orthoRange = max(_ProjectionParams.z - _ProjectionParams.y, 0.0001);

                float ld0 = isOrtho ? rawD0 * orthoRange : LinearEyeDepth(rawD0, _ZBufferParams);
                float ld1 = isOrtho ? rawD1 * orthoRange : LinearEyeDepth(rawD1, _ZBufferParams);
                float ld2 = isOrtho ? rawD2 * orthoRange : LinearEyeDepth(rawD2, _ZBufferParams);
                float ld3 = isOrtho ? rawD3 * orthoRange : LinearEyeDepth(rawD3, _ZBufferParams);

                // Roberts-Cross auf linearisierter Tiefe, relativ zur Entfernung normiert
                // (sonst wird die Linie bei weiter entfernten Objekten unsichtbar duenn).
                float refDepth = isOrtho ? 1.0 : max(ld0, 0.0001);
                float edge = (abs(ld0 - ld1) + abs(ld2 - ld3)) / refDepth;

                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float mask = step(_DepthThreshold, edge);

                return lerp(sceneColor, _OutlineColor, mask);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
