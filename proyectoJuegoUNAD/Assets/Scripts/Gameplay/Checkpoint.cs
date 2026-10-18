using UnityEngine;

/// <summary>
/// Respawn point at the start of a zone. When the player falls into a stream (below the kill plane), <see cref="KillPlane"/>
/// puts them back at the checkpoint of the zone they fell in, facing down the trail, with the waste they already hold.
/// <c>LevelEnvironmentBuilder</c> places one per zone under <c>Environment/Checkpoints</c> (Zone 1's is the spawn point).
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [SerializeField, Range(0, ForestTrail.ZoneCount - 1), Tooltip("Zone index: 0 Learning, 1 Development, 2 Challenge.")]
    private int zone;

    public int Zone
    {
        get => zone;
        set => zone = Mathf.Clamp(value, 0, ForestTrail.ZoneCount - 1);
    }

    /// <summary>
    /// The checkpoint of <paramref name="zone"/>, or of the nearest zone before it that has one; the first checkpoint if
    /// none comes before it; null when the scene has no checkpoints.
    /// </summary>
    public static Checkpoint ForZone(int zone)
    {
        Checkpoint best = null, first = null;
        foreach (var checkpoint in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
        {
            if (first == null || checkpoint.Zone < first.Zone) first = checkpoint;
            if (checkpoint.Zone <= zone && (best == null || checkpoint.Zone > best.Zone)) best = checkpoint;
        }
        return best != null ? best : first;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.8f, 0.5f);
        Gizmos.DrawRay(transform.position + Vector3.up * 0.8f, transform.forward * 1.5f);
    }
}
