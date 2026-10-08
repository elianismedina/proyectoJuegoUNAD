using UnityEngine;

/// <summary>
/// A log that rolls back and forth between two X positions at a fixed speed, so its timing is predictable.
/// Add a trigger collider with <see cref="PlayerHazard"/> to make it stumble the player. Rolling stops
/// with the game (it follows <see cref="Time.deltaTime"/>, which is zero while paused).
/// </summary>
public class RollingLog : MonoBehaviour
{
    [SerializeField] private float leftX = -5f;
    [SerializeField] private float rightX = 5f;
    [SerializeField] private float speed = 3f;

    [Tooltip("Log radius in meters; used to spin the mesh so it appears to roll without slipping.")]
    [SerializeField] private float radius = 0.5f;

    private float direction = 1f;

    private void Update()
    {
        Vector3 position = transform.position;
        position.x += direction * speed * Time.deltaTime;

        if (position.x >= rightX) { position.x = rightX; direction = -1f; }
        else if (position.x <= leftX) { position.x = leftX; direction = 1f; }

        transform.position = position;

        // Rolling along +X spins around -Z (right-hand rule); the angle matches the distance travelled.
        float degrees = direction * speed * Time.deltaTime / Mathf.Max(radius, 0.01f) * Mathf.Rad2Deg;
        transform.Rotate(0f, 0f, -degrees, Space.World);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 p = transform.position;
        Gizmos.DrawLine(new Vector3(leftX, p.y, p.z), new Vector3(rightX, p.y, p.z));
    }
}
