using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD clock from GDD §10.2: "Tiempo: 2:00", counting down the session's time limit (<see cref="LevelConfig.timeLimitSeconds"/>).
/// In the last <see cref="LevelConfig.timeWarningSeconds"/> it turns orange and plays a short warning once; nothing flashes
/// (accessibility), and the text keeps saying the time, so the colour is never the only signal.
/// Hidden on the win and lose screens, and in a level without a time limit. Holds no game rules: the clock itself runs in
/// <see cref="GameSession.Tick"/>. Built into <c>GameScreens.prefab</c> by <i>Forest Guardian &gt; UI &gt; Build Level Timer</i>.
/// </summary>
public class LevelTimer : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private AudioSource warningAudio;
    [SerializeField] private AudioClip warningClip;
    [SerializeField, Range(0f, 1f)] private float warningVolume = 0.8f;

    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.6f, 0.15f, 1f);

    private GameSession session;
    private float warningSeconds = 30f;

    public string Text => label.text;
    public bool Shown => gameObject.activeSelf;

    /// <summary>True once the clock is in its last seconds (orange).</summary>
    public bool WarningShown { get; private set; }

    /// <summary>How many times the warning played since the level loaded (once at most).</summary>
    public int WarningSoundsPlayed { get; private set; }

    /// <summary>
    /// The text the clock shows, e.g. "Tiempo: 1:05". Seconds are rounded up, so it only reads 0:00 when time is out.
    /// </summary>
    public static string Format(float secondsLeft) => "Tiempo: " + FormatTime(secondsLeft);

    /// <summary>A time as m:ss, rounded up to the whole second, e.g. 119.2 → "2:00", 0.4 → "0:01".</summary>
    public static string FormatTime(float seconds)
    {
        int whole = Mathf.Max(0, Mathf.CeilToInt(seconds - 0.0001f));
        return (whole / 60) + ":" + (whole % 60).ToString("00");
    }

    private void Start()
    {
        if (GameManager.Instance == null || !GameManager.Instance.Session.HasTimeLimit)
        {
            gameObject.SetActive(false);
            return;
        }

        session = GameManager.Instance.Session;
        if (GameManager.Instance.Config != null) warningSeconds = GameManager.Instance.Config.timeWarningSeconds;
        session.TimeChanged += OnTimeChanged;
        session.StateChanged += OnStateChanged;
        label.color = normalColor;
        OnTimeChanged(session.TimeRemaining);
    }

    private void OnDestroy()
    {
        if (session == null) return;

        session.TimeChanged -= OnTimeChanged;
        session.StateChanged -= OnStateChanged;
    }

    private void OnTimeChanged(float secondsLeft)
    {
        label.text = Format(secondsLeft);
        if (WarningShown || secondsLeft > warningSeconds || secondsLeft <= 0f) return;

        WarningShown = true;
        label.color = warningColor;
        if (warningAudio != null && warningClip != null)
        {
            warningAudio.PlayOneShot(warningClip, warningVolume);
            WarningSoundsPlayed++;
        }
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Won || state == GameState.Lost) gameObject.SetActive(false);
    }
}
