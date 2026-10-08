using UnityEngine;

/// <summary>
/// Ends the game as Lost when the tagged player falls below <see cref="killY"/>.
/// The threshold comes from the <see cref="LevelConfig"/> held by the <see cref="GameManager"/> when available.
/// </summary>
public class KillPlane : MonoBehaviour
{
    [Tooltip("Used when no GameManager/LevelConfig is present.")]
    [SerializeField] private float killY = -10f;

    private Transform player;

    private void Start()
    {
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
    }

    private void Update()
    {
        if (player == null) return;

        var manager = GameManager.Instance;
        float threshold = manager != null && manager.Config != null ? manager.Config.killPlaneY : killY;

        if (player.position.y < threshold && manager != null)
            manager.Lose();
    }
}
