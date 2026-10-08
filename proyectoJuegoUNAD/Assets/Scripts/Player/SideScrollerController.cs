using StarterAssets;
using UnityEngine;

/// <summary>
/// Side-scroller version of the Starter Assets third person controller.
/// Adapted from <c>StarterAssets.ThirdPersonController</c>: movement is restricted to the world X axis,
/// the character faces its travel direction, camera look is removed (Cinemachine handles the camera),
/// and jumping gains coyote time and a jump buffer.
/// The animation events <c>OnFootstep</c> and <c>OnLand</c> are forwarded by <see cref="PlayerAnimationEvents"/>.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(StarterAssetsInputs))]
public class SideScrollerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Run speed in m/s. The Starter Assets blend tree reaches the full Run clip at 6.")]
    public float MoveSpeed = 6f;

    [Tooltip("Acceleration and deceleration rate (higher = snappier).")]
    public float SpeedChangeRate = 10f;

    [Tooltip("How long the character takes to turn toward its travel direction.")]
    public float RotationSmoothTime = 0.08f;

    [Tooltip("Horizontal input below this magnitude is ignored (stick drift).")]
    [Range(0f, 0.5f)] public float InputDeadZone = 0.2f;

    [Header("Jump")]
    [Tooltip("Peak jump height in meters. Must clear the tallest static obstacle with margin.")]
    public float JumpHeight = 1.7f;

    public float Gravity = -20f;

    [Tooltip("Seconds after leaving a ledge during which a jump is still allowed.")]
    public float CoyoteTime = 0.2f;

    [Tooltip("Seconds a jump press is remembered before landing.")]
    public float JumpBufferTime = 0.15f;

    [Tooltip("Seconds in the air before the FreeFall animation starts.")]
    public float FallTimeout = 0.15f;

    [Header("Ground Check")]
    public bool Grounded = true;
    public float GroundedOffset = -0.14f;

    [Tooltip("Should match the radius of the CharacterController.")]
    public float GroundedRadius = 0.28f;

    public LayerMask GroundLayers = ~0;

    [Header("Audio")]
    public AudioClip LandingAudioClip;
    public AudioClip[] FootstepAudioClips;
    [Range(0f, 1f)] public float FootstepAudioVolume = 0.5f;

    /// <summary>Multiplier applied to <see cref="MoveSpeed"/>; used by mud zones (1 = normal).</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>When false, player input is ignored (paused, won or lost).</summary>
    public bool InputEnabled { get; set; } = true;

    private const float TerminalVelocity = 53f;

    private CharacterController _controller;
    private StarterAssetsInputs _input;
    private Animator _animator;
    private bool _hasAnimator;

    private float _planeZ;
    private float _velocityX;
    private float _verticalVelocity;
    private float _targetYaw = 90f; // Facing +X (right) by default.
    private float _yawVelocity;
    private float _lastGroundedTime = float.NegativeInfinity;
    private float _jumpBufferTimer;
    private float _fallTimeoutDelta;

    private int _animIDSpeed;
    private int _animIDGrounded;
    private int _animIDJump;
    private int _animIDFreeFall;
    private int _animIDMotionSpeed;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<StarterAssetsInputs>();
        _animator = GetComponentInChildren<Animator>();
        _hasAnimator = _animator != null;

        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDGrounded = Animator.StringToHash("Grounded");
        _animIDJump = Animator.StringToHash("Jump");
        _animIDFreeFall = Animator.StringToHash("FreeFall");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
    }

    private void Start()
    {
        _planeZ = transform.position.z;
        _fallTimeoutDelta = FallTimeout;
        transform.rotation = Quaternion.Euler(0f, _targetYaw, 0f);
    }

    private void Update()
    {
        GroundedCheck();
        JumpAndGravity();
        Move();
    }

    private void GroundedCheck()
    {
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
        Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

        // Ignore the ground while rising, so the frame after a jump cannot count as landed.
        bool landed = Grounded && _verticalVelocity <= 0f;
        if (landed) _lastGroundedTime = Time.time;

        if (_hasAnimator) _animator.SetBool(_animIDGrounded, Grounded);
    }

    private void JumpAndGravity()
    {
        // Consume the jump press into a short buffer so early presses still count.
        if (_input.jump)
        {
            if (InputEnabled) _jumpBufferTimer = JumpBufferTime;
            _input.jump = false;
        }

        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.deltaTime;

        bool landed = Grounded && _verticalVelocity <= 0f;
        if (landed)
        {
            _fallTimeoutDelta = FallTimeout;

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDJump, false);
                _animator.SetBool(_animIDFreeFall, false);
            }

            // Keep the controller pressed onto the ground.
            _verticalVelocity = -2f;
        }
        else if (!Grounded)
        {
            if (_fallTimeoutDelta >= 0f)
            {
                _fallTimeoutDelta -= Time.deltaTime;
            }
            else if (_hasAnimator)
            {
                _animator.SetBool(_animIDFreeFall, true);
            }
        }

        bool canJump = Time.time - _lastGroundedTime <= CoyoteTime;
        if (canJump && _jumpBufferTimer > 0f)
        {
            // v = sqrt(h * -2 * g) gives the launch velocity for the desired height.
            _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            _jumpBufferTimer = 0f;
            _lastGroundedTime = float.NegativeInfinity;

            if (_hasAnimator) _animator.SetBool(_animIDJump, true);
        }

        if (_verticalVelocity > -TerminalVelocity)
            _verticalVelocity += Gravity * Time.deltaTime;
    }

    private void Move()
    {
        float inputX = InputEnabled ? _input.move.x : 0f;
        float direction = Mathf.Abs(inputX) > InputDeadZone ? Mathf.Sign(inputX) : 0f;

        float targetVelocity = direction * MoveSpeed * SpeedMultiplier;
        _velocityX = Mathf.Lerp(_velocityX, targetVelocity, Time.deltaTime * SpeedChangeRate);
        if (Mathf.Abs(_velocityX) < 0.01f && direction == 0f) _velocityX = 0f;

        if (direction != 0f)
            _targetYaw = direction > 0f ? 90f : -90f;

        float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetYaw, ref _yawVelocity, RotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // The Z component pulls the character back onto the course plane if a collision ever pushes it off.
        float planeCorrectionZ = (_planeZ - transform.position.z) / Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 velocity = new Vector3(_velocityX, _verticalVelocity, planeCorrectionZ);
        _controller.Move(velocity * Time.deltaTime);

        if (_hasAnimator)
        {
            _animator.SetFloat(_animIDSpeed, Mathf.Abs(_velocityX));
            _animator.SetFloat(_animIDMotionSpeed, 1f);
        }
    }

    /// <summary>Called by <see cref="PlayerAnimationEvents"/> from the run/walk clips.</summary>
    public void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight <= 0.5f || FootstepAudioClips == null || FootstepAudioClips.Length == 0)
            return;

        int index = Random.Range(0, FootstepAudioClips.Length);
        AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
    }

    /// <summary>Called by <see cref="PlayerAnimationEvents"/> from the landing clips.</summary>
    public void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight <= 0.5f || LandingAudioClip == null)
            return;

        AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Grounded ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
    }
}
