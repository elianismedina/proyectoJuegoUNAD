using UnityEngine;

/// <summary>Trigger volume that arms a <see cref="FallingRock"/> when the player enters it.</summary>
[RequireComponent(typeof(Collider))]
public class FallingRockTrigger : MonoBehaviour
{
    [SerializeField] private FallingRock rock;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (rock != null && other.TryGetComponent(out SideScrollerController _))
            rock.Arm();
    }
}
