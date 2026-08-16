Shader "Echobound/Comic Character Outline"
{
    Properties
    {
        _InkColor("Ink Color", Color) = (0.85, 0.85, 0.85, 1)
        _OutlinePower("Outline Power", Range(0.5, 8)) = 2.2
        _OutlineThreshold("Outline Threshold", Range(0, 1)) = 0.35
        _NoiseScale("Ink Noise Scale", Range(1, 40)) = 14
        _NoiseStrength("Ink Noise Strength", Range(0, 0.5)) = 0.18
        _ComicSteps("Comic Steps", Range(2, 8)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Back
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Comic Ink Outline"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _InkColor;
                float _OutlinePower;
                float _OutlineThreshold;
                float _NoiseScale;
                float _NoiseStrength;
                float _ComicSteps;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);

                return output;
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);

                return frac(value.x * value.y);
            }

            float ComicNoise(float3 positionWS)
            {
                float2 cell = floor(
                    positionWS.xz * _NoiseScale
                );

                float noise = Hash21(cell);

                return noise * 2.0 - 1.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS =
                    normalize(input.normalWS);

                float3 viewDirectionWS =
                    normalize(
                        GetWorldSpaceNormalizeViewDir(
                            input.positionWS
                        )
                    );

                float facing =
                    saturate(
                        dot(normalWS, viewDirectionWS)
                    );

                // 0 на фронтальной поверхности,
                // 1 на силуэте.
                float rim =
                    pow(
                        1.0 - facing,
                        _OutlinePower
                    );

                float noise =
                    ComicNoise(input.positionWS);

                rim += noise * _NoiseStrength;
                rim = saturate(rim);

                // Ступенчатый, рисованный переход вместо
                // идеально плавного градиента.
                float stepped =
                    floor(rim * _ComicSteps) /
                    max(_ComicSteps - 1.0, 1.0);

                float outline =
                    smoothstep(
                        _OutlineThreshold,
                        1.0,
                        stepped
                    );

                // Убираем почти прозрачный центр модели.
                clip(outline - 0.05);

                half4 result = _InkColor;
                result.a = outline * _InkColor.a;

                return result;
            }

            ENDHLSL
        }
    }

    Fallback Off
}