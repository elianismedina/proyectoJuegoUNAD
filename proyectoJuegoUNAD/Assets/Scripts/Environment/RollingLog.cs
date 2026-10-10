using UnityEngine;

/// <summary>
/// A log that rolls back and forth along its own local X axis at a fixed speed, so its timing is predictable.
/// It starts at its placed position, which is the middle of its run, and travels <see cref="travel"/> metres
/// in total; turn the object to point the run along or across the trail.
/// A trigger collider with <see cref="PlayerHazard"/> stumbles the player; the solid <see cref="body"/> stops
/// the player from walking through the log and pushes them aside when the log rolls into them (a moving
/// kinematic collider does not push a <see cref="CharacterController"/> on its own). Rolling stops with the
/// game (it follows <see cref="Time.deltaTime"/>, which is zero while paused).
/// </summary>
public class RollingLog : MonoBehaviour
{
    [Tooltip("Total length of the run in metres, centred on the placed position, along the object's local X axis.")]
    [SerializeField, Min(0.5f)] private float travel = 14f;

    [SerializeField] private float speed = 3f;

    [Tooltip("Log radius in meters; used to spin the mesh so it appears to roll without slipping.")]
    [SerializeField] private float radius = 0.5f;

    [Tooltip("Solid (non-trigger) capsule that covers the whole log, on a child so the stumble trigger can be larger.")]
    [SerializeField] private CapsuleCollider body;

    private readonly Collider[] overlaps = new Collider[8];
    private Vector3 origin;
    private Vector3 axis;      // World direction of the run (the placed local X); the log's own spin must not change it.
    private Vector3 spinAxis;  // Rolling toward +axis spins around up × axis (right-hand rule).
    private float offset;
    private float direction = 1f;

    private void Awake()
    {
        origin = transform.position;
        axis = transform.right;
        axis.y = 0f;
        axis.Normalize();
        spinAxis = Vector3.Cross(Vector3.up, axis);
    }

    private void Update()
    {
        float half = travel * 0.5f;
        offset += direction * speed * Time.deltaTime;

        if (offset >= half) { offset = half; direction = -1f; }
        else if (offset <= -half) { offset = -half; direction = 1f; }

        transform.position = origin + axis * offset;

        // The spin angle matches the distance travelled.
        float degrees = direction * speed * Time.deltaTime / Mathf.Max(radius, 0.01f) * Mathf.Rad2Deg;
        transform.Rotate(spinAxis, degrees, Space.World);

        PushOutCharacters();
    }

    /// <summary>Moves any character the log has rolled into out of the solid body.</summary>
    private void PushOutCharacters()
    {
        if (body == null) return;

        Transform t = body.transform;
        Vector3 scale = t.lossyScale;
        Vector3 along = body.direction == 0 ? t.right : body.direction == 1 ? t.up : t.forward;
        float lengthScale = Mathf.Abs(body.direction == 0 ? scale.x : body.direction == 1 ? scale.y : scale.z);
        float radius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        float half = Mathf.Max(body.height * lengthScale * 0.5f - radius, 0f);
        Vector3 center = t.TransformPoint(body.center);

        int count = Physics.OverlapCapsuleNonAlloc(center - along * half, center + along * half, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!(overlaps[i] is CharacterController character) || !character.enabled) continue;
            Transform other = character.transform;
            if (Physics.ComputePenetration(body, t.position, t.rotation, character, other.position, other.rotation, out Vector3 push, out float distance))
            {
                // ComputePenetration gives the direction to move the log out; the character goes the other way.
                character.Move(-push * distance);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? origin : transform.position;
        Vector3 along = Application.isPlaying ? axis : transform.right;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center - along * travel * 0.5f, center + along * travel * 0.5f);
    }
}
