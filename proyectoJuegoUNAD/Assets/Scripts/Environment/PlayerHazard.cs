
using UnityEngine;

/// <summary>
/// Handles player contact with hazards.
/// Lethal hazards end the game; non-lethal hazards cause a stumble.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerHazard : MonoBehaviour
{
    [Header("Hazard Settings")]
    [SerializeField] private bool isLethal = false;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out PlayerController player))
            return;

        if (isLethal)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.Lose();

            return;
        }

        Vector3 position = player.transform.position;
        Vector3 away = position - GetComponent<Collider>().ClosestPoint(position);

        if (away.sqrMagnitude < 0.0001f)
            away = position - transform.position;

        player.Stumble(away);
    }
}
