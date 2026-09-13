using UnityEngine;

public sealed class Barrel : MonoBehaviour
{
    [SerializeField] private float bounceForce = 7f;
    [SerializeField] private float explodeDuration = 0.55f;
    [SerializeField] private AudioClip barrelClip;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameObject explosionParticlesPrefab;

    private bool hasExploded;
    private Animator animator;
    private BoxCollider2D mainCollider;
    private BoxCollider2D topTrigger;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
        // main collider is non-trigger, top trigger is child
        foreach (BoxCollider2D c in colliders)
        {
            if (!c.isTrigger)
            {
                mainCollider = c;
            }
        }

        Transform top = transform.Find("TopTrigger");
        if (top != null)
        {
            topTrigger = top.GetComponent<BoxCollider2D>();
        }
        else
        {
            // fallback search in children
            topTrigger = GetComponentInChildren<BoxCollider2D>();
            if (topTrigger == mainCollider)
            {
                topTrigger = null;
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasExploded)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        Rigidbody2D playerBody = other.attachedRigidbody;
        if (playerBody == null)
        {
            playerBody = other.GetComponent<Rigidbody2D>();
        }

        if (playerBody == null)
        {
            return;
        }

        // Must be falling or stationary vertically and above barrel
        if (playerBody.linearVelocity.y > 0.05f)
        {
            return;
        }

        if (other.transform.position.y <= transform.position.y + 0.15f)
        {
            return;
        }

        Explode(playerBody);
    }

    private void Explode(Rigidbody2D playerBody)
    {
        hasExploded = true;

        if (mainCollider != null)
        {
            mainCollider.enabled = false;
        }

        if (topTrigger != null)
        {
            topTrigger.enabled = false;
        }

        if (animator != null)
        {
            animator.SetTrigger("Explode");
        }

        // Apply vertical bounce keeping horizontal velocity
        Vector2 velocity = playerBody.linearVelocity;
        velocity.y = bounceForce;
        playerBody.linearVelocity = velocity;

        // Play SFX via AudioManager or AudioSource
        if (barrelClip != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(barrelClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(barrelClip, transform.position);
            }
        }
        else
        {
            // Fallback to AudioManager barrelClip
            AudioManager manager = FindAnyObjectByType<AudioManager>();
            if (manager != null)
            {
                // Try to play via manager's sfxSource via reflection or direct
                AudioSource[] sources = manager.GetComponents<AudioSource>();
                foreach (AudioSource src in sources)
                {
                    if (!src.loop)
                    {
                        // Assume SFX source is non-looping
                        // Try to get barrelClip from manager serialized field
                        var field = typeof(AudioManager).GetField("barrelClip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            AudioClip clip = field.GetValue(manager) as AudioClip;
                            if (clip != null)
                            {
                                src.PlayOneShot(clip);
                                break;
                            }
                        }
                    }
                }
            }
        }

        // Particle effect: polvo/chispas simple 0.4-0.6s, auto-destroy, synced with anim+SFX+bounce
        if (explosionParticlesPrefab != null)
        {
            GameObject fx = Instantiate(explosionParticlesPrefab, transform.position, Quaternion.identity);
            ParticleSystem ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }
            Destroy(fx, 1f);
        }

        // Destroy after animation duration
        Destroy(gameObject, explodeDuration);
    }
}
