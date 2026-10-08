using UnityEngine;

/// <summary>
/// Trigger at the end of the course. Touching it calls <see cref="GameSession.ReachGoal"/>: the level is won if enough
/// waste was collected, otherwise the session reports how much is missing and the player can go back for it.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelGoal : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out SideScrollerController _)) return;
        if (GameManager.Instance != null) GameManager.Instance.ReachGoal();
    }
}
