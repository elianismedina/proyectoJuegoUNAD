using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD counter from GDD §10.2: "Residuos: X/10", where 10 is <see cref="LevelConfig.targetWaste"/>.
/// Listens to <see cref="GameSession.WasteCollected"/> on the session owned by the <see cref="GameManager"/>;
/// holds no game rules itself. Spare items past the target keep counting (for example 11/10).
/// It also fills the optional "Recuperación del bosque" bar (<see cref="progressBarFill"/>) with the forest's recovery
/// from <see cref="ForestRecovery"/> (full once every zone is recovered); in a scene without one it falls back to
/// collected / target.
/// </summary>
public class WasteCounter : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private UnityEngine.UI.Image progressBarFill;

    private GameSession session;

    public string Text => label.text;

    /// <summary>How full the recovery bar is, 0 to 1 (0 without a bar).</summary>
    public float BarFill => progressBarFill != null ? progressBarFill.fillAmount : 0f;

    /// <summary>The text the counter shows for a given count, e.g. "Residuos: 3/10".</summary>
    public static string Format(int collected, int target) => "Residuos: " + collected + "/" + target;

    private void OnEnable() => ForestRecovery.ProgressChanged += OnRecoveryProgress;

    private void OnDisable() => ForestRecovery.ProgressChanged -= OnRecoveryProgress;

    private void Start()
    {
        if (GameManager.Instance == null) return;

        session = GameManager.Instance.Session;
        session.WasteCollected += OnWasteCollected;
        OnWasteCollected(session.CollectedWaste, session.TargetWaste);
        if (ForestRecovery.Current != null && ForestRecovery.Current.Tracker != null)
            OnRecoveryProgress(ForestRecovery.Current.Progress);
    }

    private void OnDestroy()
    {
        if (session != null) session.WasteCollected -= OnWasteCollected;
    }

    // Updates the counter text, and the bar too when there is no forest to follow.
    private void OnWasteCollected(int collected, int target)
    {
        label.text = Format(collected, target);

        if (progressBarFill != null && ForestRecovery.Current == null)
            progressBarFill.fillAmount = target > 0 ? Mathf.Clamp01((float)collected / target) : 0f;
    }

    private void OnRecoveryProgress(float progress)
    {
        if (progressBarFill != null) progressBarFill.fillAmount = Mathf.Clamp01(progress);
    }
}
