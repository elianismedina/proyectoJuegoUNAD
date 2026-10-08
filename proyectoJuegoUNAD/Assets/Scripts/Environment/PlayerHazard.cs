using UnityEngine;

/// <summary>
/// Makes a trigger collider stumble the player on contact (knockback away from the hazard, no damage).
/// Used by rolling logs and falling rocks; static obstacles are plain solid colliders instead.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerHazard : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out SideScrollerController player)) return;

        float direction = player.transform.position.x - transform.position.x;
        player.Stumble(direction >= 0f ? 1f : -1f);
    }
}
