using UnityEngine;

public class SoundPulseManager : MonoBehaviour
{
    public static SoundPulseManager Instance { get; private set; }

    private const int MaxPulses = 8;
    private const int OcclusionRayCount = 96;
    private const int MaxOcclusionSamples =
        MaxPulses * OcclusionRayCount;

    [Header("Pulse")]
    [SerializeField]
    private float pulseSpeed = 8f;

    [SerializeField]
    private float maxRadius = 15f;

    [SerializeField]
    private float pulseWidth = 0.8f;

    [SerializeField]
    private float revealDuration = 2.5f;

    [Header("Tap Refresh")]
    [SerializeField]
    private float mergeDistance = 1.5f;

    [Header("Obstacles")]
    [SerializeField]
    private LayerMask obstacleMask = ~(1 << 6);

    [SerializeField, Min(0f)]
    private float obstacleRayHeight = 0.35f;

    private readonly Vector4[] pulseOrigins =
        new Vector4[MaxPulses];

    private readonly float[] pulseRadii =
        new float[MaxPulses];

    private readonly float[] pulseIntensities =
        new float[MaxPulses];

    private readonly float[] pulseTimers =
        new float[MaxPulses];

    private readonly bool[] pulseActive =
        new bool[MaxPulses];

    private readonly float[] pulseFadeTimers =
        new float[MaxPulses];

    private readonly float[] pulseActives =
        new float[MaxPulses];

    private readonly float[] pulseOcclusionDistances =
        new float[MaxOcclusionSamples];
    
    private static readonly int PulseOriginsId =
        Shader.PropertyToID("_PulseOrigins");

    private static readonly int PulseRadiiId =
        Shader.PropertyToID("_PulseRadii");

    private static readonly int PulseIntensitiesId =
        Shader.PropertyToID("_PulseIntensities");

    private static readonly int PulseActivesId =
        Shader.PropertyToID("_PulseActives");

    private static readonly int PulseOcclusionDistancesId =
        Shader.PropertyToID("_PulseOcclusionDistances");

    private static readonly int PulseWidthId =
        Shader.PropertyToID("_PulseWidth");

    private static readonly int PulseSpeedId =
        Shader.PropertyToID("_PulseSpeed");

    private static readonly int MaxRadiusId =
        Shader.PropertyToID("_MaxRadius");

    private static readonly int RevealDurationId =
        Shader.PropertyToID("_RevealDuration");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        ClearAllPulses();
        UploadShaderData();
    }

    private void Update()
    {
        UpdatePulses();
        UploadShaderData();
    }

    public void CreatePulse(Vector3 worldPosition)
    {
        // Всегда создаём новую волну.
        // Старые импульсы не сбрасываются.
        int pulseIndex = FindFreePulse();

        // Если все слоты заняты,
        // заменяем только самый старый импульс.
        if (pulseIndex < 0)
            pulseIndex = FindOldestPulse();

        pulseOrigins[pulseIndex] =
            new Vector4(
                worldPosition.x,
                worldPosition.y,
                worldPosition.z,
                0f
            );

        pulseRadii[pulseIndex] = 0f;
        pulseTimers[pulseIndex] = 0f;
        pulseFadeTimers[pulseIndex] = 0f;
        pulseIntensities[pulseIndex] = 1f;
        pulseActive[pulseIndex] = true;

        BuildOcclusionDistances(
            pulseIndex,
            worldPosition
        );
    }

    private void BuildOcclusionDistances(
        int pulseIndex,
        Vector3 worldPosition)
    {
        int startIndex =
            pulseIndex * OcclusionRayCount;

        Vector3 rayOrigin =
            worldPosition + Vector3.up * obstacleRayHeight;

        for (int i = 0; i < OcclusionRayCount; i++)
        {
            float angle =
                (Mathf.PI * 2f * i) /
                OcclusionRayCount;

            Vector3 direction =
                new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle)
                );

            float visibleDistance = maxRadius;

            if (Physics.Raycast(
                    rayOrigin,
                    direction,
                    out RaycastHit hit,
                    maxRadius,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                visibleDistance = hit.distance;
            }

            pulseOcclusionDistances[startIndex + i] =
                visibleDistance;
        }
    }

    private void UpdatePulses()
    {
        for (int i = 0; i < MaxPulses; i++)
        {
            if (!pulseActive[i])
                continue;

            if (pulseRadii[i] < maxRadius)
            {
                pulseRadii[i] +=
                    pulseSpeed * Time.deltaTime;

                if (pulseRadii[i] >= maxRadius)
                {
                    pulseRadii[i] = maxRadius;
                    pulseFadeTimers[i] = 0f;
                }
            }
            else
            {
                pulseFadeTimers[i] +=
                    Time.deltaTime;
            }

            pulseTimers[i] +=
                Time.deltaTime;

            if (pulseRadii[i] >= maxRadius)
            {
                pulseIntensities[i] =
                    1f -
                    Mathf.Clamp01(
                        pulseFadeTimers[i] /
                        revealDuration
                    );
            }
            else
            {
                pulseIntensities[i] = 1f;
            }

            if (pulseFadeTimers[i] >= revealDuration)
            {
                pulseActive[i] = false;
                pulseIntensities[i] = 0f;
            }
        }
    }

    private int FindPulseNearPosition(Vector3 position)
    {
        float mergeDistanceSqr =
            mergeDistance * mergeDistance;

        for (int i = 0; i < MaxPulses; i++)
        {
            if (!pulseActive[i])
                continue;

            Vector3 pulsePosition =
                pulseOrigins[i];

            Vector3 difference =
                pulsePosition - position;

            difference.y = 0f;

            if (difference.sqrMagnitude <= mergeDistanceSqr)
                return i;
        }

        return -1;
    }

    private int FindFreePulse()
    {
        for (int i = 0; i < MaxPulses; i++)
        {
            if (!pulseActive[i])
                return i;
        }

        return -1;
    }

    private int FindOldestPulse()
    {
        int oldestIndex = 0;
        float oldestTime = float.MinValue;

        for (int i = 0; i < MaxPulses; i++)
        {
            if (pulseTimers[i] > oldestTime)
            {
                oldestTime = pulseTimers[i];
                oldestIndex = i;
            }
        }

        return oldestIndex;
    }

    private void UploadShaderData()
    {
        for (int i = 0; i < MaxPulses; i++)
        {
            if (!pulseActive[i])
            {
                pulseActives[i] = 0f;
                pulseIntensities[i] = 0f;
            }
            else
            {
                pulseActives[i] = 1f;
            }
        }

        Shader.SetGlobalVectorArray(
            PulseOriginsId,
            pulseOrigins
        );

        Shader.SetGlobalFloatArray(
            PulseRadiiId,
            pulseRadii
        );

        Shader.SetGlobalFloatArray(
            PulseIntensitiesId,
            pulseIntensities
        );

        Shader.SetGlobalFloatArray(
            PulseActivesId,
            pulseActives
        );

        Shader.SetGlobalFloatArray(
            PulseOcclusionDistancesId,
            pulseOcclusionDistances
        );

        Shader.SetGlobalFloat(
            PulseWidthId,
            pulseWidth
        );

        Shader.SetGlobalFloat(
            PulseSpeedId,
            pulseSpeed
        );

        Shader.SetGlobalFloat(
            MaxRadiusId,
            maxRadius
        );

        Shader.SetGlobalFloat(
            RevealDurationId,
            revealDuration
        );
    }

    private void ClearAllPulses()
    {
        for (int i = 0; i < MaxPulses; i++)
        {
            pulseOrigins[i] = Vector4.zero;
            pulseRadii[i] = 0f;
            pulseIntensities[i] = 0f;
            pulseTimers[i] = 0f;
            pulseFadeTimers[i] = 0f;
            pulseActives[i] = 0f;
            pulseActive[i] = false;

            int startIndex = i * OcclusionRayCount;

            for (int j = 0; j < OcclusionRayCount; j++)
                pulseOcclusionDistances[startIndex + j] = maxRadius;
        }
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            ClearAllPulses();
            UploadShaderData();
            Instance = null;
        }
    }
}
