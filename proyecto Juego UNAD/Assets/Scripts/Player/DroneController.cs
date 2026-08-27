using UnityEngine;
using UnityEngine.InputSystem;

public class DroneController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float altitudeSpeed = 4f;
    [SerializeField] private float minAltitude = 1.5f;
    [SerializeField] private float maxAltitude = 15f;

    [Header("Beam")]
    [SerializeField] private float beamRange = 100f;
    [SerializeField] private float beamDamagePerSecond = 50f;
    [SerializeField] private Transform beamOrigin;
    [SerializeField] private LayerMask beamLayerMask;
    [SerializeField] private LineRenderer beamVFX;

    [Header("Planting")]
    [SerializeField] private float plantingDetectionRadius = 5f;
    [SerializeField] private LayerMask plantingZoneLayerMask;

    private Rigidbody rb;
    private Vector2 moveInput;
    private float altitudeInput;
    private bool isFiring;
    private bool isPlanting;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (beamOrigin == null)
        {
            beamOrigin = transform.Find("BeamOrigin");
        }

        if (beamVFX == null)
        {
            beamVFX = GetComponentInChildren<LineRenderer>();
        }

        if (beamVFX != null)
        {
            beamVFX.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        HandleMovement();
        ClampAltitude();
    }

    private void Update()
    {
        HandleBeam();
    }

    private void HandleMovement()
    {
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        moveDirection = transform.parent != null ? transform.parent.TransformDirection(moveDirection) : moveDirection;

        Vector3 targetVelocity = moveDirection.normalized * moveSpeed;
        targetVelocity.y = altitudeInput * altitudeSpeed;

        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y + altitudeInput * altitudeSpeed * Time.fixedDeltaTime, targetVelocity.z);
    }

    private void ClampAltitude()
    {
        Vector3 pos = transform.position;
        pos.y = Mathf.Clamp(pos.y, minAltitude, maxAltitude);
        transform.position = pos;

        // Stop upward/downward velocity if at bounds
        if ((pos.y <= minAltitude && altitudeInput < 0) || (pos.y >= maxAltitude && altitudeInput > 0))
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        }
    }

    private void HandleBeam()
    {
        if (!isFiring || beamOrigin == null)
        {
            if (beamVFX != null)
            {
                beamVFX.enabled = false;
            }
            return;
        }

        Vector3 beamStart = beamOrigin.position;
        Vector3 beamDirection = beamOrigin.forward;

        if (Physics.Raycast(beamStart, beamDirection, out RaycastHit hit, beamRange, beamLayerMask))
        {
            SmogZone smogZone = hit.collider.GetComponent<SmogZone>();
            if (smogZone != null)
            {
                smogZone.TakeDamage(beamDamagePerSecond * Time.deltaTime);
            }

            if (beamVFX != null)
            {
                beamVFX.enabled = true;
                beamVFX.SetPosition(0, beamStart);
                beamVFX.SetPosition(1, hit.point);
            }

            // Debug visualization
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
        Collider[] hits = Physics.OverlapSphere(transform.position, plantingDetectionRadius, plantingZoneLayerMask);

        foreach (Collider hit in hits)
        {
            PlantingZone zone = hit.GetComponent<PlantingZone>();
            if (zone != null)
            {
                zone.TryPlant();
            }
        }
    }

    // Input callbacks (via PlayerInput with Send Messages behavior)
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

    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {
            TryPlant();
        }
    }
}
