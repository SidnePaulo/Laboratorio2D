using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerController : MonoBehaviour
{
    [SerializeField] private float speed = 2f;
    [SerializeField] private float jumpForce = 4f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private float horizontalInput;
    private bool isGrounded;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            body.velocity = new Vector2(body.velocity.x, jumpForce);
        }

        if (horizontalInput != 0f)
        {
            spriteRenderer.flipX = horizontalInput < 0f;
        }
    }

    private void FixedUpdate()
    {
        body.velocity = new Vector2(horizontalInput * speed, body.velocity.y);
    }
}
