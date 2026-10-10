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

    // The player currently slowed by this zone. Remembering it keeps the modifier from stacking when Unity
    // reports an enter without a matching exit (for example after the CharacterController is toggled).
    private PlayerController slowedPlayer;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out PlayerController player) || player == slowedPlayer) return;

        slowedPlayer = player;
        player.AddSpeedModifier(speedMultiplier);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out PlayerController player) || player != slowedPlayer) return;

        Release();
    }

    private void OnDisable()
    {
        Release();
    }

    private void Release()
    {
        if (slowedPlayer == null) return;

        slowedPlayer.RemoveSpeedModifier(speedMultiplier);
        slowedPlayer = null;
    }
}
