using UnityEngine;

/// <summary>
/// Makes a trigger collider stumble the player on contact (knockback away from the hazard on the ground plane, no damage).
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
        if (!other.TryGetComponent(out PlayerController player)) return;

        // Push the player straight away from the hazard (Stumble keeps only the ground-plane part).
        Vector3 away = player.transform.position - transform.position;
        player.Stumble(away);
    }
}
