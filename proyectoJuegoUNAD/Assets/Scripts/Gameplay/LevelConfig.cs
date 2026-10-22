using UnityEngine;

/// <summary>Designer-tunable values for one level; the GDD's "adjustable parameters" live here.</summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Forest Guardian/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Min(1)]
    [Tooltip("Waste items the player must collect to win (GDD §5.1: 10).")]
    public int targetWaste = 10;

    [Tooltip("World Y below which the player counts as having fallen off the course.")]
    public float killPlaneY = -2f;

    [Min(0f)]
    [Tooltip("Seconds the player has to win; reaching zero loses the game (GDD §5.3: 2 minutes). 0 turns the clock off.")]
    public float timeLimitSeconds = 120f;

    [Min(0f)]
    [Tooltip("Seconds left when the HUD clock turns orange and the warning sounds.")]
    public float timeWarningSeconds = 30f;
}
