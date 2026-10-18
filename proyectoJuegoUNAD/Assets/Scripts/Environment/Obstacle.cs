using UnityEngine;

/// <summary>
/// Marks a solid obstacle (log, stump, rock). Running into it head-on makes the player stumble, like a hazard does
/// (see <see cref="PlayerController"/>, which detects the hit). Standing on top of it or brushing along it does not.
/// Put it on the root of each prefab in <c>Assets/Prefabs/Environment/Obstacles</c>; the colliders may be on children.
/// </summary>
[DisallowMultipleComponent]
public class Obstacle : MonoBehaviour
{
}
