using UnityEngine;

/// <summary>
/// One of the animals that appear when a zone of the forest is recovered (GDD §6.1.4), from the "Animals FREE - Animated
/// Low Poly 3D Models" pack (ithappy) through the variants in <c>Prefabs/Environment/RecoveryAnimals</c>. It is pure
/// decoration: no collider, and it never leaves the short stretch of forest floor <c>LevelEnvironmentBuilder</c> checked
/// for it, off the path and clear of waste, checkpoints and obstacles. A resting animal stays at <see cref="PointA"/>
/// playing its idle loop; a walker paces from <see cref="PointA"/> to <see cref="PointB"/> and back, resting a few seconds
/// at each end. It drives the pack's animator (blend tree: <c>Vert</c> 0 idle, 1 walk; <c>State</c> 0 walk, 1 run).
/// <see cref="ForestRecovery"/> keeps it inactive until its zone is recovered.
/// </summary>
public class RecoveryAnimal : MonoBehaviour
{
    private static readonly int VertId = Animator.StringToHash("Vert");
    private static readonly int StateId = Animator.StringToHash("State");

    [SerializeField, Tooltip("Where the animal stands (world space); a resting animal stays here.")]
    private Vector3 pointA;

    [SerializeField, Tooltip("The other end of a walker's stretch (world space).")]
    private Vector3 pointB;

    [SerializeField, Tooltip("Paces between the two points; otherwise rests at the first one.")]
    private bool walks;

    [SerializeField, Min(0.1f), Tooltip("Walking speed in m/s.")]
    private float walkSpeed = 0.6f;

    [SerializeField, Min(10f), Tooltip("Turning speed in degrees per second.")]
    private float turnSpeed = 140f;

    [SerializeField, Tooltip("Seconds a walker rests at each end (min, max).")]
    private Vector2 restSeconds = new Vector2(2f, 5f);

    private Animator animator;
    private bool towardB = true;
    private float restUntil;
    private float vert;

    public Vector3 PointA => pointA;
    public Vector3 PointB => pointB;
    public bool Walks => walks;

    /// <summary>True while a walker is actually moving (not resting or turning round).</summary>
    public bool IsMoving { get; private set; }

    /// <summary>Sets where the animal lives (used by <c>LevelEnvironmentBuilder</c>).</summary>
    public void Configure(Vector3 a, Vector3 b, bool walker)
    {
        pointA = a;
        pointB = b;
        walks = walker;
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        restUntil = Time.time + Random.Range(0f, restSeconds.y); // walkers set off at different times
    }

    private void Update()
    {
        IsMoving = false;
        if (walks && Time.time >= restUntil)
        {
            Vector3 target = towardB ? pointB : pointA;
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0025f)
            {
                towardB = !towardB;
                restUntil = Time.time + Random.Range(restSeconds.x, restSeconds.y);
            }
            else
            {
                var facing = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, facing) < 25f)
                {
                    IsMoving = true;
                    Vector3 flat = Vector3.MoveTowards(Flat(transform.position), Flat(target), walkSpeed * Time.deltaTime);
                    float along = Mathf.InverseLerp(0f, Flat(pointB - pointA).magnitude, Flat(flat - pointA).magnitude);
                    transform.position = new Vector3(flat.x, Mathf.Lerp(pointA.y, pointB.y, along), flat.z);
                }
            }
        }

        vert = Mathf.MoveTowards(vert, IsMoving ? 1f : 0f, 3f * Time.deltaTime);
        if (animator != null)
        {
            animator.SetFloat(VertId, vert);
            animator.SetFloat(StateId, 0f);
        }
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
