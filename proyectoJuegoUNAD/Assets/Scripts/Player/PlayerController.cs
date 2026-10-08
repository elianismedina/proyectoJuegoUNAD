using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Side-scroller movement for the Forest Guardian: horizontal run and jump on the X axis.
/// The Z axis is frozen so the 3D character stays on the course plane.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpHeight = 1.8f;

    [Header("Jump Assist")]
    [Tooltip("Seconds after leaving a ledge during which a jump is still allowed.")]
    [SerializeField] private float coyoteTime = 0.2f;
    [Tooltip("Seconds a jump press is remembered before landing.")]
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Visuals")]
    [Tooltip("Child object holding the character mesh; rotated to face the movement direction.")]
    [SerializeField] private Transform model;
    [SerializeField] private float turnSpeed = 15f;

    private Rigidbody body;
    private CapsuleCollider capsule;
    private float moveInput;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private float facingYaw = 90f; // Facing +X (right) by default.

    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (jumpAction != null)
        {
            jumpAction.action.Enable();
            jumpAction.action.performed += OnJumpPerformed;
        }
    }

    private void OnDisable()
    {
        if (jumpAction != null) jumpAction.action.performed -= OnJumpPerformed;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        lastJumpPressedTime = Time.time;
    }

    private void Update()
    {
        moveInput = moveAction != null ? Mathf.Clamp(moveAction.action.ReadValue<Vector2>().x, -1f, 1f) : 0f;

        if (Mathf.Abs(moveInput) > 0.01f)
            facingYaw = moveInput > 0f ? 90f : -90f;

        if (model != null)
        {
            Quaternion target = Quaternion.Euler(0f, facingYaw, 0f);
            model.rotation = Quaternion.Slerp(model.rotation, target, turnSpeed * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        IsGrounded = CheckGrounded();
        if (IsGrounded) lastGroundedTime = Time.time;

        Vector3 velocity = body.linearVelocity;
        velocity.x = moveInput * moveSpeed;

        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool jumpRequested = Time.time - lastJumpPressedTime <= jumpBufferTime;

        if (canJump && jumpRequested)
        {
            velocity.y = Mathf.Sqrt(2f * -Physics.gravity.y * jumpHeight);
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
        }

        body.linearVelocity = velocity;
    }

    private bool CheckGrounded()
    {
        // The ray starts inside the player's own collider, so it never hits itself.
        float rayLength = capsule.height * 0.5f * transform.lossyScale.y + groundCheckDistance;
        Vector3 origin = transform.TransformPoint(capsule.center);
        return Physics.Raycast(origin, Vector3.down, rayLength, groundMask, QueryTriggerInteraction.Ignore);
    }
}
