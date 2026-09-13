using System.Collections;
using UnityEngine;

/// <summary>
/// Spike hazard with trigger-based damage, cooldown invulnerability,
/// SFX fallback and visual flash. Respawns player at last safe checkpoint
/// without resetting collected coins. Does not modify PlayerController.
/// Values: cooldown 0.75s (range 0.5-1s), flash 0.6s, invulnerability window.
/// </summary>
public sealed class SpikeHazard : MonoBehaviour
{
    [SerializeField] private float cooldownDuration = 0.75f;
    [SerializeField] private float flashDuration = 0.6f;
    [SerializeField] private AudioClip hazardClip;
    [SerializeField] private AudioSource audioSource;

    // Global invulnerability to avoid loop when staying inside trigger
    private static float lastDamageTime = -999f;

    private void Awake()
    {
        EnsureTriggerCollider();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void EnsureTriggerCollider()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
        }
        collider.isTrigger = true;
        // Tight fit for spike sprite (roughly 0.32 width, 0.16 height with offset near base)
        if (collider.size == Vector2.zero || collider.size.y > 0.5f)
        {
            collider.size = new Vector2(0.30f, 0.16f);
            collider.offset = new Vector2(0f, -0.06f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (Time.time < lastDamageTime + cooldownDuration)
        {
            return;
        }

        lastDamageTime = Time.time;

        // SFX: hazard specific if assigned, otherwise fallback to AudioManager barrel/coin or error/toggle
        PlayHazardSfx();

        // Visual feedback: player sprite flash
        SpriteRenderer playerRenderer = other.GetComponent<SpriteRenderer>();
        if (playerRenderer == null)
        {
            playerRenderer = other.GetComponentInChildren<SpriteRenderer>();
        }
        if (playerRenderer != null)
        {
            StartCoroutine(FlashRoutine(playerRenderer));
        }
        else
        {
            // Fallback flash via coroutine on hazard itself (no crash)
            StartCoroutine(FlashRoutine(null));
        }

        // Respawn at last safe checkpoint (static tracker or initial spawn)
        Vector3 respawnPosition = GetRespawnPosition(other.transform);
        Rigidbody2D body = other.attachedRigidbody;
        if (body == null)
        {
            body = other.GetComponent<Rigidbody2D>();
        }
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = respawnPosition;
            other.transform.position = respawnPosition;
        }
        else
        {
            other.transform.position = respawnPosition;
        }

        // Preserve coins: do not touch GameManager CoinCount or deactivate coins.
    }

    private static Vector3 GetRespawnPosition(Transform player)
    {
        // Prefer CheckpointTracker if present
        // Use reflection-free static access via FindObjectOfType
        CheckpointTracker tracker = FindAnyObjectByType<CheckpointTracker>();
        if (tracker != null)
        {
            Vector3 pos = CheckpointTracker.LastSafePosition;
            if (pos != Vector3.zero)
            {
                return pos;
            }
        }

        // Fallback to initial spawn documented value
        return new Vector3(-7.5f, -0.25f, 0f);
    }

    private void PlayHazardSfx()
    {
        AudioClip clip = hazardClip;
        if (clip == null)
        {
            // Try AudioManager barrelClip as fallback, then error_007/toggle_001 from pack
            AudioManager manager = FindAnyObjectByType<AudioManager>();
            if (manager != null)
            {
                var field = typeof(AudioManager).GetField("barrelClip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    clip = field.GetValue(manager) as AudioClip;
                }
            }
        }
        if (clip == null)
        {
            clip = Resources.Load<AudioClip>("hazard_fallback");
        }

        if (clip != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, transform.position);
            }
        }
        else
        {
            // Last resort: try AudioManager sfxSource with its current clip
            AudioManager manager = FindAnyObjectByType<AudioManager>();
            if (manager != null)
            {
                AudioSource[] sources = manager.GetComponents<AudioSource>();
                foreach (AudioSource src in sources)
                {
                    if (!src.loop && src.clip != null)
                    {
                        src.PlayOneShot(src.clip);
                        break;
                    }
                }
            }
        }
    }

    private IEnumerator FlashRoutine(SpriteRenderer renderer)
    {
        // Simple flash: toggle alpha / color 3 times over flashDuration
        float elapsed = 0f;
        bool visible = true;
        Color original = renderer != null ? renderer.color : Color.white;
        Color flashColor = new Color(original.r, original.g, original.b, 0.3f);

        while (elapsed < flashDuration)
        {
            if (renderer != null)
            {
                renderer.color = visible ? flashColor : original;
            }
            visible = !visible;
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (renderer != null)
        {
            renderer.color = original;
            renderer.enabled = true;
        }
    }
}
