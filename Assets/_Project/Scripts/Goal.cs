using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level goal trigger at final zone X~18-19. Shows responsive Overlay UI
/// "¡Nivel completado!" without changing scene, limits player input,
/// preserves final coin count.
/// </summary>
public sealed class Goal : MonoBehaviour
{
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private Text victoryText;
    [SerializeField] private GameObject finalCountTextObject;
    [SerializeField] private AudioClip victoryClip;
    [SerializeField] private AudioSource audioSource;

    private bool isCompleted;

    private void Awake()
    {
        EnsureTriggerCollider();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
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
        if (collider.size == Vector2.zero || collider.size.x < 0.3f)
        {
            collider.size = new Vector2(0.6f, 0.9f);
            collider.offset = new Vector2(0f, 0.1f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCompleted)
        {
            return;
        }
        if (!other.CompareTag("Player"))
        {
            return;
        }

        isCompleted = true;
        ShowVictory(other.gameObject);
    }

    private void ShowVictory(GameObject player)
    {
        // Stop/limit input: disable PlayerController without editing its file
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        // Show UI panel
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        // Update text with final coin count if available
        GameManager gm = FindAnyObjectByType<GameManager>();
        int count = gm != null ? gm.CoinCount : 0;
        if (victoryText != null)
        {
            victoryText.text = "¡Nivel completado!";
        }

        // Secondary text shows "Monedas: X/5" using current CoinCount
        if (finalCountTextObject != null)
        {
            Text countText = finalCountTextObject.GetComponent<Text>();
            if (countText != null)
            {
                countText.text = "Monedas: " + count + "/5";
            }
            else
            {
                // Fallback if object has Text on children
                Text childText = finalCountTextObject.GetComponentInChildren<Text>();
                if (childText != null)
                {
                    childText.text = "Monedas: " + count + "/5";
                }
            }
        }
        else if (victoryText != null)
        {
            // Fallback: append count to main text if secondary object missing
            victoryText.text = "¡Nivel completado! Monedas: " + count + "/5";
        }

        // SFX
        if (victoryClip != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(victoryClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(victoryClip, transform.position);
            }
        }
        else
        {
            AudioManager manager = FindAnyObjectByType<AudioManager>();
            if (manager != null)
            {
                AudioSource[] sources = manager.GetComponents<AudioSource>();
                foreach (AudioSource src in sources)
                {
                    if (!src.loop && src.clip != null)
                    {
                        // fallback to coin or barrel clip
                        var field = typeof(AudioManager).GetField("coinClip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
    }
}
