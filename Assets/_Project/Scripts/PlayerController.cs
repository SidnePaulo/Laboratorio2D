using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public sealed class PlayerController : MonoBehaviour
{
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private static readonly int VerticalVelocityParameter = Animator.StringToHash("VerticalVelocity");
    private static readonly int IsGroundedParameter = Animator.StringToHash("IsGrounded");

    [SerializeField] private float speed = 2f;
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private float horizontalInput;
    private bool isGrounded;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
        }

        if (horizontalInput != 0f)
        {
            spriteRenderer.flipX = horizontalInput < 0f;
        }

        animator.SetFloat(SpeedParameter, Mathf.Abs(horizontalInput));
        animator.SetFloat(VerticalVelocityParameter, body.linearVelocity.y);
        animator.SetBool(IsGroundedParameter, isGrounded);
    }

    private void FixedUpdate()
    {
        body.linearVelocity = new Vector2(horizontalInput * speed, body.linearVelocity.y);
    }
}
