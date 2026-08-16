Shader "Hidden/SoundVision"
{
    Properties
    {
        _WaveColor("Wave Color", Color) = (1, 1, 1, 1)
        _BackgroundColor("Background Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Sound Vision"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MAX_PULSES 8

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _PulseOrigins[MAX_PULSES];
            float _PulseRadii[MAX_PULSES];
            float _PulseIntensities[MAX_PULSES];
            float _PulseActives[MAX_PULSES];

            float _PulseWidth;
            float _PulseSpeed;
            float _MaxRadius;
            float _RevealDuration;

            half4 _WaveColor;
            half4 _BackgroundColor;

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS =
                    GetFullScreenTriangleVertexPosition(
                        input.vertexID
                    );

                output.uv =
                    GetFullScreenTriangleTexCoord(
                        input.vertexID
                    );

                return output;
            }

            float3 WorldPositionFromDepth(float2 uv)
            {
                float rawDepth =
                    SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0001)
                        return float3(0, 0, 0);
                #else
                    if (rawDepth >= 0.9999)
                        return float3(0, 0, 0);
                #endif

                return ComputeWorldSpacePosition(
                    uv,
                    rawDepth,
                    UNITY_MATRIX_I_VP
                );
            }

            float Hash21(float2 value)
            {
                value =
                    frac(
                        value *
                        float2(123.34, 456.21)
                    );

                value +=
                    dot(
                        value,
                        value + 45.32
                    );

                return frac(
                    value.x * value.y
                );
            }

            float ComicNoise(float3 worldPosition)
            {
                float2 cell =
                    floor(worldPosition.xz * 14.0);

                return Hash21(cell) * 2.0 - 1.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                float3 centerPosition =
                    WorldPositionFromDepth(uv);

                if (length(centerPosition) < 0.001)
                    return _BackgroundColor;

                float2 pixelSize =
                    1.0 / _ScreenParams.xy;

                float3 leftPosition =
                    WorldPositionFromDepth(
                        uv + float2(-pixelSize.x, 0)
                    );

                float3 rightPosition =
                    WorldPositionFromDepth(
                        uv + float2(pixelSize.x, 0)
                    );

                float3 upPosition =
                    WorldPositionFromDepth(
                        uv + float2(0, pixelSize.y)
                    );

                float3 downPosition =
                    WorldPositionFromDepth(
                        uv + float2(0, -pixelSize.y)
                    );

                float horizontalDifference =
                    distance(
                        leftPosition,
                        rightPosition
                    );

                float verticalDifference =
                    distance(
                        upPosition,
                        downPosition
                    );

                float depthEdge =
                    max(
                        horizontalDifference,
                        verticalDifference
                    );

                float objectEdge =
                    smoothstep(
                        0.06,
                        0.28,
                        depthEdge
                    );

                float comicNoise =
                    ComicNoise(centerPosition);

                float comicEdge =
                    saturate(
                        objectEdge +
                        comicNoise * 0.15
                    );

                float comicSteps = 4.0;

                comicEdge =
                    floor(
                        comicEdge * comicSteps
                    ) /
                    max(
                        comicSteps - 1.0,
                        1.0
                    );

                comicEdge =
                    smoothstep(
                        0.25,
                        0.75,
                        comicEdge
                    );

                float totalReveal = 0.0;
                float totalWave = 0.0;

                for (int i = 0; i < MAX_PULSES; i++)
                {
                    if (_PulseActives[i] < 0.5)
                        continue;

                    float3 pulseOrigin =
                        _PulseOrigins[i].xyz;

                    float pulseRadius =
                        _PulseRadii[i];

                    float pulseIntensity =
                        _PulseIntensities[i];

                    float distanceFromOrigin =
                        distance(
                            centerPosition,
                            pulseOrigin
                        );

                    // За пределами конкретной волны
                    // объект не проявляется.
                    float insideRadius =
                        step(
                            distanceFromOrigin,
                            _MaxRadius
                        );

                    float distanceFromFront =
                        abs(
                            distanceFromOrigin -
                            pulseRadius
                        );

                    // Видимый фронт текущей волны.
                    float waveFront =
                        1.0 -
                        smoothstep(
                            0.0,
                            _PulseWidth,
                            distanceFromFront
                        );

                    // Сильное затухание к внешнему радиусу.
                    float waveEndFade =
                        1.0 -
                        smoothstep(
                            _MaxRadius * 0.5,
                            _MaxRadius,
                            pulseRadius
                        );

                    waveEndFade =
                        pow(
                            waveEndFade,
                            3.5
                        );

                    float currentWave =
                        waveFront *
                        waveEndFade *
                        insideRadius *
                        pulseIntensity;

                    // Всё, через что фронт уже прошёл,
                    // остаётся временно видимым.
                    float reached =
                        step(
                            distanceFromOrigin,
                            pulseRadius
                        );

                    float currentReveal =
                        reached *
                        insideRadius *
                        pulseIntensity;

                    totalWave =
                        max(
                            totalWave,
                            currentWave
                        );

                    totalReveal =
                        max(
                            totalReveal,
                            currentReveal
                        );
                }

                float revealedObjects =
                    totalReveal *
                    comicEdge;

                float visibleWave =
                    totalWave *
                    (0.45 + comicEdge * 0.45);

                float finalIntensity =
                    saturate(
                        revealedObjects +
                        visibleWave
                    );

                return half4(
                    lerp(
                        _BackgroundColor.rgb,
                        _WaveColor.rgb,
                        finalIntensity
                    ),
                    1.0
                );
            }

            ENDHLSL
        }
    }

    Fallback Off
}