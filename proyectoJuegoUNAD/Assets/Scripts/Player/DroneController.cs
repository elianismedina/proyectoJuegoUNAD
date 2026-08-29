using UnityEngine;
using UnityEngine.InputSystem;

public class DroneController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float sprintSpeed = 12f;
    [SerializeField] private float minAltitude = 1.5f;
    [SerializeField] private float maxAltitude = 15f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Movement - Momentum")]
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float sprintAcceleration = 26f;
    [SerializeField] private float deceleration = 10f;

    [Header("Movement - Vertical Thrust")]
    [SerializeField] private float maxClimbForce = 30f;
    [SerializeField] private float maxDescendForce = 20f;
    [SerializeField] private float verticalDamping = 4f;

    [Header("Movement - Altitude Boundary")]
    [SerializeField] private float altitudeBoundarySpring = 40f;
    [SerializeField] private float altitudeBoundaryDamping = 8f;

    [Header("Visual Tilt (cosmetic, DroneModel child only)")]
    [SerializeField] private Transform droneModel;
    [SerializeField] private float maxPitchAngle = 12f;
    [SerializeField] private float maxRollAngle = 10f;
    [SerializeField] private float tiltSmoothSpeed = 6f;

    [Header("Beam")]
    [SerializeField] private float beamRange = 100f;
    [SerializeField] private float beamDamagePerSecond = 50f;
    [SerializeField] private Transform beamOrigin;
    [SerializeField] private LayerMask beamLayerMask;
    [SerializeField] private LineRenderer beamVFX;

    [Header("Beam Energy")]
    [SerializeField] private float maxBeamEnergy = 100f;
    [SerializeField] private float beamEnergyDrain = 30f;
    [SerializeField] private float beamEnergyRechargeRate = 20f;

    [Header("Planting")]
    [SerializeField] private float plantingDetectionRadius = 5f;
    [SerializeField] private float plantingCooldown = 2f;
    [SerializeField] private LayerMask plantingZoneLayerMask;

    private Rigidbody rb;
    private Vector2 moveInput;
    private float altitudeInput;
    private bool isFiring;
    private bool isSprinting;
    private float beamEnergy;
    private float lastPlantTime;

    public enum DroneState { Flying, Hovering, Planting }
    private DroneState state = DroneState.Flying;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        beamEnergy = maxBeamEnergy;

        if (beamOrigin == null)
            beamOrigin = transform.Find("BeamOrigin");

        if (droneModel == null)
            droneModel = transform.Find("DroneModel");

        if (beamVFX == null)
            beamVFX = GetComponentInChildren<LineRenderer>();

        if (beamVFX != null)
            beamVFX.enabled = false;
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleRotation();
        HandleAltitudeBoundary();
        HandleVisualTilt();
    }

    private void Update()
    {
        UpdateBeamEnergy();
        HandleBeam();
    }

    private void HandleMovement()
    {
        Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        Vector3 newHorizontal;

        if (moveInput == Vector2.zero)
        {
            state = DroneState.Hovering;
            newHorizontal = Vector3.MoveTowards(currentHorizontal, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }
        else
        {
            state = DroneState.Flying;
            Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
            moveDirection = transform.parent != null ? transform.parent.TransformDirection(moveDirection) : moveDirection;
            moveDirection.Normalize();

            float targetSpeed = isSprinting ? sprintSpeed : moveSpeed;
            float accel = isSprinting ? sprintAcceleration : acceleration;
            Vector3 targetVelocity = moveDirection * targetSpeed;
            newHorizontal = Vector3.MoveTowards(currentHorizontal, targetVelocity, accel * Time.fixedDeltaTime);
        }

        rb.linearVelocity = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);

        float hoverThrust = rb.mass * -Physics.gravity.y;
        float playerThrust = altitudeInput >= 0f ? altitudeInput * maxClimbForce : altitudeInput * maxDescendForce;
        float verticalDampingForce = -rb.linearVelocity.y * verticalDamping;
        rb.AddForce(Vector3.up * (hoverThrust + playerThrust + verticalDampingForce));
    }

    private void HandleRotation()
    {
        if (moveInput != Vector2.zero)
        {
            Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            if (transform.parent != null)
                moveDirection = transform.parent.TransformDirection(moveDirection);

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void HandleAltitudeBoundary()
    {
        float y = transform.position.y;
        float penetration = 0f;

        if (y > maxAltitude)
            penetration = y - maxAltitude;
        else if (y < minAltitude)
            penetration = y - minAltitude;

        if (penetration != 0f)
        {
            float springForce = -penetration * altitudeBoundarySpring;
            float dampingForce = -rb.linearVelocity.y * altitudeBoundaryDamping;
            rb.AddForce(Vector3.up * (springForce + dampingForce));
        }
    }

    private void HandleVisualTilt()
    {
        if (droneModel == null) return;

        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        float referenceSpeed = isSprinting ? sprintSpeed : moveSpeed;
        float pitchTarget = Mathf.Clamp(-localVel.z / referenceSpeed, -1f, 1f) * maxPitchAngle;
        float rollTarget = Mathf.Clamp(-localVel.x / referenceSpeed, -1f, 1f) * maxRollAngle;

        Quaternion targetTilt = Quaternion.Euler(pitchTarget, 0f, rollTarget);
        droneModel.localRotation = Quaternion.Slerp(droneModel.localRotation, targetTilt, tiltSmoothSpeed * Time.fixedDeltaTime);
    }

    private void UpdateBeamEnergy()
    {
        if (!isFiring || beamEnergy <= 0)
        {
            beamEnergy = Mathf.Min(beamEnergy + beamEnergyRechargeRate * Time.deltaTime, maxBeamEnergy);
        }
        else
        {
            beamEnergy = Mathf.Max(beamEnergy - beamEnergyDrain * Time.deltaTime, 0);
        }
    }

    private void HandleBeam()
    {
        if (!isFiring || beamOrigin == null || beamEnergy <= 0)
        {
            if (beamVFX != null)
                beamVFX.enabled = false;
            return;
        }

        Vector3 beamStart = beamOrigin.position;
        Vector3 beamDirection = beamOrigin.forward;

        if (Physics.Raycast(beamStart, beamDirection, out RaycastHit hit, beamRange, beamLayerMask))
        {
            SmogZone smogZone = hit.collider.GetComponent<SmogZone>();
            if (smogZone != null)
                smogZone.TakeDamage(beamDamagePerSecond * Time.deltaTime);

            if (beamVFX != null)
            {
                beamVFX.enabled = true;
                beamVFX.SetPosition(0, beamStart);
                beamVFX.SetPosition(1, hit.point);
            }

            Debug.DrawLine(beamStart, hit.point, Color.cyan, 0.1f);
        }
        else
        {
            if (beamVFX != null)
            {
                beamVFX.enabled = true;
                beamVFX.SetPosition(0, beamStart);
                beamVFX.SetPosition(1, beamStart + beamDirection * beamRange);
            }

            Debug.DrawLine(beamStart, beamStart + beamDirection * beamRange, Color.yellow, 0.1f);
        }
    }

    private void TryPlant()
    {
        if (Time.time - lastPlantTime < plantingCooldown)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, plantingDetectionRadius, plantingZoneLayerMask);

        foreach (Collider hit in hits)
        {
            PlantingZone zone = hit.GetComponent<PlantingZone>();
            if (zone != null)
            {
                zone.TryPlant();
                lastPlantTime = Time.time;
                state = DroneState.Planting;
            }
        }
    }

    public float GetBeamEnergyNormalized() => beamEnergy / maxBeamEnergy;
    public DroneState GetState() => state;
    public bool CanFire() => beamEnergy > 0;

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnAltitude(InputValue value)
    {
        altitudeInput = value.Get<float>();
    }

    public void OnAttack(InputValue value)
    {
        isFiring = value.isPressed;
    }

    public void OnSprint(InputValue value)
    {
        isSprinting = value.isPressed;
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
            TryPlant();
    }
}
