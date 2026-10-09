using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD counter from GDD §10.2: "Residuos: X/10", where 10 is <see cref="LevelConfig.targetWaste"/>.
/// Listens to <see cref="GameSession.WasteCollected"/> on the session owned by the <see cref="GameManager"/>;
/// holds no game rules itself. Spare items past the target keep counting (for example 11/10).
/// </summary>
public class WasteCounter : MonoBehaviour
{
    [SerializeField] private Text label;

    private GameSession session;

    public string Text => label.text;

    /// <summary>The text the counter shows for a given count, e.g. "Residuos: 3/10".</summary>
    public static string Format(int collected, int target) => "Residuos: " + collected + "/" + target;

    private void Start()
    {
        if (GameManager.Instance == null) return;

        session = GameManager.Instance.Session;
        session.WasteCollected += OnWasteCollected;
        OnWasteCollected(session.CollectedWaste, session.TargetWaste);
    }

    private void OnDestroy()
    {
        if (session != null) session.WasteCollected -= OnWasteCollected;
    }

    private void OnWasteCollected(int collected, int target)
    {
        label.text = Format(collected, target);
    }
}
