using UnityEngine;

/// <summary>
/// Tracks last safe grounded position for respawn.
/// Attached to Player, uses GroundCheck overlap so PlayerController is not modified.
/// Initial checkpoint is (-7.5, -0.25) as documented. Updated after 0.2s of stable ground.
/// </summary>
public sealed class CheckpointTracker : MonoBehaviour
{
    public static Vector3 LastSafePosition = new Vector3(-7.5f, -0.25f, 0f);

    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    private float stableGroundTime;

    private void Awake()
    {
        if (groundCheck == null)
        {
            Transform found = transform.Find("GroundCheck");
            if (found != null)
            {
                groundCheck = found;
            }
        }

        if (groundLayer.value == 0)
        {
            int floorLayer = LayerMask.NameToLayer("Floor");
            if (floorLayer >= 0)
            {
                groundLayer = 1 << floorLayer;
            }
        }

        LastSafePosition = new Vector3(-7.5f, -0.25f, 0f);
    }

    private void Update()
    {
        if (groundCheck == null)
        {
            return;
        }

        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);
        if (isGrounded)
        {
            stableGroundTime += Time.deltaTime;
            if (stableGroundTime > 0.2f)
            {
                // Save position slightly above ground to avoid instant re-trigger
                LastSafePosition = transform.position;
            }
        }
        else
        {
            stableGroundTime = 0f;
        }
    }

    // Allow SpikeHazard to update via editor serialization fallback
    public void Configure(Transform check, float radius, LayerMask layer)
    {
        groundCheck = check;
        groundRadius = radius;
        groundLayer = layer;
    }
}
