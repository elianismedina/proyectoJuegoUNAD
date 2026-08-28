using UnityEngine;
using UnityEngine.InputSystem;

public class DroneController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float sprintSpeed = 12f;
    [SerializeField] private float altitudeSpeed = 4f;
    [SerializeField] private float minAltitude = 1.5f;
    [SerializeField] private float maxAltitude = 15f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float velocityDamping = 0.95f;

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

        if (beamVFX == null)
            beamVFX = GetComponentInChildren<LineRenderer>();

        if (beamVFX != null)
            beamVFX.enabled = false;
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleRotation();
        ClampAltitude();
    }

    private void Update()
    {
        UpdateBeamEnergy();
        HandleBeam();
    }

    private void HandleMovement()
    {
        if (moveInput == Vector2.zero)
        {
            state = DroneState.Hovering;
            rb.linearVelocity *= velocityDamping;
        }
        else
        {
            state = DroneState.Flying;
            Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
            moveDirection = transform.parent != null ? transform.parent.TransformDirection(moveDirection) : moveDirection;

            float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
            Vector3 targetVelocity = moveDirection.normalized * currentSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y + altitudeInput * altitudeSpeed * Time.fixedDeltaTime, rb.linearVelocity.z);
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

    private void ClampAltitude()
    {
        Vector3 pos = transform.position;
        pos.y = Mathf.Clamp(pos.y, minAltitude, maxAltitude);
        transform.position = pos;

        if ((pos.y <= minAltitude && altitudeInput < 0) || (pos.y >= maxAltitude && altitudeInput > 0))
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
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
