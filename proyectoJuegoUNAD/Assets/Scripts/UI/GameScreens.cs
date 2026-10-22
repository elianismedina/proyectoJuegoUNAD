using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// End-of-game screens from GDD §5.3: "¡Bosque limpio!" on a win and "¡Inténtalo de nuevo!" on a loss, each with a
/// button that restarts the level, plus a short notice when the player reaches the goal with waste still missing or
/// falls into a stream and is put back at the start of the zone (<see cref="KillPlane.PlayerRespawned"/>).
/// Also the pause menu from GDD §6.6 (Esc): "Continuar" resumes, "Reiniciar" restarts the level and "Salir" goes back
/// to the main menu (GDD §10.3). When the level starts, a banner states the goal from GDD §5.1 ("Recoge 10 residuos y
/// llega a la meta") for a few seconds and then fades out. When a zone of the forest is recovered
/// (<see cref="ForestRecovery.ZoneRecovered"/>), "¡Zona recuperada!" shows for about 2 s with a short chime, below the
/// goal arrow and above the objective banner and the other notices, so it never covers them.
/// With a time limit (<see cref="LevelTimer"/>), the banner names it ("… en 2:00"), the lose screen says
/// "¡Se acabó el tiempo!" when the clock runs out, and the win screen says how long the player took.
/// Listens to the <see cref="GameSession"/> owned by the <see cref="GameManager"/>; holds no game rules itself.
/// </summary>
public class GameScreens : MonoBehaviour
{
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Text winDetail;
    [SerializeField] private Button winRestartButton;

    [SerializeField] private GameObject losePanel;
    [SerializeField] private Text loseDetail;
    [SerializeField] private Button loseRestartButton;

    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button pauseContinueButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseQuitButton;

    [SerializeField] private GameObject missingNotice;
    [SerializeField] private Text missingText;

    [SerializeField, Min(0.5f), Tooltip("Seconds the 'waste missing' notice stays on screen.")]
    private float noticeSeconds = 3f;

    [SerializeField] private CanvasGroup objectiveBanner;
    [SerializeField] private Text objectiveText;

    [SerializeField, Min(1f), Tooltip("Seconds the objective banner stays on screen when the level starts, fade included.")]
    private float objectiveSeconds = 5f;

    [SerializeField, Min(0f), Tooltip("Seconds the objective banner takes to fade out at the end.")]
    private float objectiveFadeSeconds = 0.6f;

    [SerializeField] private CanvasGroup zoneNotice;
    [SerializeField] private AudioSource zoneNoticeAudio;
    [SerializeField] private AudioClip zoneNoticeClip;

    [SerializeField, Min(0.5f), Tooltip("Seconds '¡Zona recuperada!' stays on screen, fade included.")]
    private float zoneNoticeSeconds = 2f;

    [SerializeField, Min(0f), Tooltip("Seconds '¡Zona recuperada!' takes to fade out at the end.")]
    private float zoneNoticeFadeSeconds = 0.4f;

    [SerializeField, Range(0f, 1f)] private float zoneNoticeVolume = 0.8f;

    private GameSession session;
    private float noticeHideTime;
    private float objectiveHideTime;
    private float zoneNoticeHideTime;

    public bool WinShown => winPanel.activeSelf;
    public bool LoseShown => losePanel.activeSelf;
    public bool PauseShown => pausePanel.activeSelf;
    public bool NoticeShown => missingNotice.activeSelf;
    public string NoticeText => missingText.text;
    public bool ObjectiveShown => objectiveBanner.gameObject.activeSelf;
    public string ObjectiveText => objectiveText.text;
    public bool ZoneNoticeShown => zoneNotice != null && zoneNotice.gameObject.activeSelf;
    public string WinDetailText => winDetail.text;
    public string LoseDetailText => loseDetail != null ? loseDetail.text : null;

    /// <summary>How many times the zone-recovered chime played since the level loaded.</summary>
    public int ZoneNoticeSoundsPlayed { get; private set; }

    /// <summary>What the HUD says when a zone of the forest is recovered.</summary>
    public const string ZoneRecoveredMessage = "¡Zona recuperada!";

    /// <summary>The notice shown when the player falls and reappears at the start of the zone with their waste.</summary>
    public const string RespawnNotice = "¡Te caíste! Vuelves al inicio de la zona con tus residuos.";

    /// <summary>What the lose screen says when the clock runs out.</summary>
    public const string TimeUpDetail = "¡Se acabó el tiempo!";

    /// <summary>What the lose screen says for any other loss (a fall in a level without checkpoints).</summary>
    public const string FallDetail = "Te caíste del recorrido.";

    /// <summary>The goal line the banner shows when the level starts, e.g. "Recoge 10 residuos y llega a la meta".</summary>
    public static string FormatObjective(int targetWaste) =>
        "Recoge " + targetWaste + (targetWaste == 1 ? " residuo" : " residuos") + " y llega a la meta";

    /// <summary>The goal line with the time limit, e.g. "Recoge 10 residuos y llega a la meta en 2:00" (no limit: none).</summary>
    public static string FormatObjective(int targetWaste, float timeLimitSeconds) =>
        timeLimitSeconds > 0f
            ? FormatObjective(targetWaste) + " en " + LevelTimer.FormatTime(timeLimitSeconds)
            : FormatObjective(targetWaste);

    /// <summary>The win screen's detail, e.g. "Recogiste 10 residuos en 1:42." (without a time limit, no time).</summary>
    public static string FormatWinDetail(int collected, float elapsedSeconds, bool timed) =>
        "Recogiste " + collected + (collected == 1 ? " residuo" : " residuos")
        + (timed ? " en " + LevelTimer.FormatTime(elapsedSeconds) : "") + ".";

    private void Awake()
    {
        winPanel.SetActive(false);
        losePanel.SetActive(false);
        pausePanel.SetActive(false);
        missingNotice.SetActive(false);
        objectiveBanner.gameObject.SetActive(false);
        if (zoneNotice != null) zoneNotice.gameObject.SetActive(false);
        winRestartButton.onClick.AddListener(Restart);
        loseRestartButton.onClick.AddListener(Restart);
        pauseContinueButton.onClick.AddListener(Continue);
        pauseRestartButton.onClick.AddListener(Restart);
        pauseQuitButton.onClick.AddListener(QuitToMenu);
    }

    private void Start()
    {
        if (GameManager.Instance == null) return;

        session = GameManager.Instance.Session;
        session.StateChanged += OnStateChanged;
        session.GoalReachedWithMissingWaste += OnGoalReachedWithMissingWaste;
        KillPlane.PlayerRespawned += OnPlayerRespawned;
        ForestRecovery.ZoneRecovered += OnZoneRecovered;
        ShowObjective(session.TargetWaste, session.TimeLimit);
    }

    private void OnDestroy()
    {
        KillPlane.PlayerRespawned -= OnPlayerRespawned;
        ForestRecovery.ZoneRecovered -= OnZoneRecovered;
        if (session == null) return;

        session.StateChanged -= OnStateChanged;
        session.GoalReachedWithMissingWaste -= OnGoalReachedWithMissingWaste;
    }

    private void Update()
    {
        if (missingNotice.activeSelf && Time.unscaledTime >= noticeHideTime) missingNotice.SetActive(false);
        if (objectiveBanner.gameObject.activeSelf) UpdateObjective();
        if (ZoneNoticeShown) UpdateZoneNotice();
    }

    // Only while playing: winning recovers every zone at once, and the win screen says it better.
    private void OnZoneRecovered(int zone)
    {
        if (zoneNotice == null || session == null || session.State != GameState.Playing) return;

        zoneNotice.alpha = 1f;
        zoneNotice.gameObject.SetActive(true);
        zoneNoticeHideTime = Time.unscaledTime + zoneNoticeSeconds;
        if (zoneNoticeAudio != null && zoneNoticeClip != null)
        {
            zoneNoticeAudio.PlayOneShot(zoneNoticeClip, zoneNoticeVolume);
            ZoneNoticeSoundsPlayed++;
        }
    }

    private void UpdateZoneNotice()
    {
        float left = zoneNoticeHideTime - Time.unscaledTime;
        if (left <= 0f)
        {
            zoneNotice.gameObject.SetActive(false);
            return;
        }
        zoneNotice.alpha = zoneNoticeFadeSeconds > 0f ? Mathf.Clamp01(left / zoneNoticeFadeSeconds) : 1f;
    }

    private void ShowObjective(int targetWaste, float timeLimitSeconds)
    {
        objectiveText.text = FormatObjective(targetWaste, timeLimitSeconds);
        objectiveBanner.alpha = 1f;
        objectiveBanner.gameObject.SetActive(true);
        objectiveHideTime = Time.time + objectiveSeconds;
    }

    // Game time, not unscaled: pausing (timeScale 0) holds the banner so it is still there after "Continuar".
    private void UpdateObjective()
    {
        float left = objectiveHideTime - Time.time;
        if (left <= 0f)
        {
            objectiveBanner.gameObject.SetActive(false);
            return;
        }
        objectiveBanner.alpha = objectiveFadeSeconds > 0f ? Mathf.Clamp01(left / objectiveFadeSeconds) : 1f;
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Paused)
        {
            Show(pausePanel, pauseContinueButton);
            return;
        }

        pausePanel.SetActive(false);
        if (state == GameState.Won)
        {
            objectiveBanner.gameObject.SetActive(false);
            if (zoneNotice != null) zoneNotice.gameObject.SetActive(false);
            missingNotice.SetActive(false);
            winDetail.text = FormatWinDetail(session.CollectedWaste, session.ElapsedTime, session.HasTimeLimit);
            Show(winPanel, winRestartButton);
        }
        else if (state == GameState.Lost)
        {
            objectiveBanner.gameObject.SetActive(false);
            if (zoneNotice != null) zoneNotice.gameObject.SetActive(false);
            missingNotice.SetActive(false);
            if (loseDetail != null) loseDetail.text = session.LoseReason == LoseReason.TimeUp ? TimeUpDetail : FallDetail;
            Show(losePanel, loseRestartButton);
        }
    }

    private void OnGoalReachedWithMissingWaste(int missing)
    {
        missingText.text = missing == 1
            ? "Te falta 1 residuo. ¡Vuelve a buscarlo!"
            : "Te faltan " + missing + " residuos. ¡Vuelve a buscarlos!";
        objectiveBanner.gameObject.SetActive(false); // The notice says what is left, so the goal line would only clutter.
        ShowNotice();
    }

    private void OnPlayerRespawned(Checkpoint checkpoint)
    {
        missingText.text = RespawnNotice;
        ShowNotice();
    }

    private void ShowNotice()
    {
        missingNotice.SetActive(true);
        noticeHideTime = Time.unscaledTime + noticeSeconds;
    }

    private static void Show(GameObject panel, Button focus)
    {
        panel.SetActive(true);
        // Select the button so Enter / Space / gamepad confirm restart without a mouse.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
    }

    private static void Continue()
    {
        if (GameManager.Instance != null) GameManager.Instance.Resume();
    }

    private static void QuitToMenu()
    {
        if (GameManager.Instance != null) GameManager.Instance.QuitToMenu();
    }

    private static void Restart()
    {
        if (GameManager.Instance != null) GameManager.Instance.Restart();
    }
}
