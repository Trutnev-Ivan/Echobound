using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerEchoTap : MonoBehaviour
{
    [Header("Cane")]
    [SerializeField]
    private Transform caneTip;

    [SerializeField]
    private LayerMask groundMask = ~0;

    [SerializeField, Min(0.1f)]
    private float groundDetectionDistance = 2f;

    [Header("Tap")]
    [SerializeField, Min(0f)]
    private float cooldown = 0.35f;

    private float nextAllowedTapTime;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            TryCreatePulse();
    }

    private void TryCreatePulse()
    {
        if (Time.time < nextAllowedTapTime)
            return;

        if (SoundPulseManager.Instance == null)
        {
            Debug.LogWarning(
                "SoundPulseManager отсутствует в сцене.",
                this
            );

            return;
        }

        nextAllowedTapTime = Time.time + cooldown;

        Vector3 pulsePosition = FindTapPosition();

        SoundPulseManager.Instance.CreatePulse(
            pulsePosition
        );
    }

    private Vector3 FindTapPosition()
    {
        Vector3 canePosition = caneTip != null
            ? caneTip.position
            : transform.position;

        Vector3 rayOrigin =
            canePosition + Vector3.up * 0.25f;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundDetectionDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return canePosition;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 canePosition = caneTip != null
            ? caneTip.position
            : transform.position;

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(canePosition, 0.08f);

        Gizmos.color = Color.gray;
        Gizmos.DrawLine(
            canePosition + Vector3.up * 0.25f,
            canePosition + Vector3.down * groundDetectionDistance
        );
    }
}