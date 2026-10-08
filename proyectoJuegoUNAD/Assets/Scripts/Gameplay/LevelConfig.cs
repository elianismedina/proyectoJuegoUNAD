using UnityEngine;

/// <summary>Designer-tunable values for one level; the GDD's "adjustable parameters" live here.</summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Forest Guardian/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Min(1)]
    [Tooltip("Waste items the player must collect to win (GDD §5.1: 10).")]
    public int targetWaste = 10;

    [Tooltip("World Y below which the player counts as having fallen off the course.")]
    public float killPlaneY = -10f;
}
