using UnityEngine;

/// <summary>
/// Trigger volume that slows the player while inside it (GDD §7.4). The slowdown is registered as a speed
/// modifier on entry and removed on exit, so overlapping zones multiply correctly.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MudZone : MonoBehaviour
{
    [Range(0.1f, 1f)]
    [Tooltip("Fraction of the normal move speed while in the mud.")]
    [SerializeField] private float speedMultiplier = 0.5f;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out SideScrollerController player))
            player.AddSpeedModifier(speedMultiplier);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out SideScrollerController player))
            player.RemoveSpeedModifier(speedMultiplier);
    }
}
