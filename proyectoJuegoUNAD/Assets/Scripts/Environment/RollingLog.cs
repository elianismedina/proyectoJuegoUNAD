using UnityEngine;

/// <summary>
/// A log that rolls back and forth along its own local X axis at a fixed speed, so its timing is predictable.
/// It starts at its placed position, which is the middle of its run, and travels <see cref="travel"/> metres
/// in total; turn the object to point the run along or across the trail.
/// Add a trigger collider with <see cref="PlayerHazard"/> to make it stumble the player. Rolling stops
/// with the game (it follows <see cref="Time.deltaTime"/>, which is zero while paused).
/// </summary>
public class RollingLog : MonoBehaviour
{
    [Tooltip("Total length of the run in metres, centred on the placed position, along the object's local X axis.")]
    [SerializeField, Min(0.5f)] private float travel = 14f;

    [SerializeField] private float speed = 3f;

    [Tooltip("Log radius in meters; used to spin the mesh so it appears to roll without slipping.")]
    [SerializeField] private float radius = 0.5f;

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
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? origin : transform.position;
        Vector3 along = Application.isPlaying ? axis : transform.right;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center - along * travel * 0.5f, center + along * travel * 0.5f);
    }
}
