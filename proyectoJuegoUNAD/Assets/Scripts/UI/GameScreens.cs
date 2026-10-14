using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// End-of-game screens from GDD §5.3: "¡Bosque limpio!" on a win and "¡Inténtalo de nuevo!" on a loss, each with a
/// button that restarts the level, plus a short notice when the player reaches the goal with waste still missing.
/// Also the pause menu from GDD §6.6 (Esc): "Continuar" resumes, "Reiniciar" restarts the level and "Salir" goes back
/// to the main menu (GDD §10.3). When the level starts, a banner states the goal from GDD §5.1 ("Recoge 10 residuos y
/// llega a la meta") for a few seconds and then fades out.
/// Listens to the <see cref="GameSession"/> owned by the <see cref="GameManager"/>; holds no game rules itself.
/// </summary>
public class GameScreens : MonoBehaviour
{
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Text winDetail;
    [SerializeField] private Button winRestartButton;

    [SerializeField] private GameObject losePanel;
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

    private GameSession session;
    private float noticeHideTime;
    private float objectiveHideTime;

    public bool WinShown => winPanel.activeSelf;
    public bool LoseShown => losePanel.activeSelf;
    public bool PauseShown => pausePanel.activeSelf;
    public bool NoticeShown => missingNotice.activeSelf;
    public string NoticeText => missingText.text;
    public bool ObjectiveShown => objectiveBanner.gameObject.activeSelf;
    public string ObjectiveText => objectiveText.text;

    /// <summary>The goal line the banner shows when the level starts, e.g. "Recoge 10 residuos y llega a la meta".</summary>
    public static string FormatObjective(int targetWaste) =>
        "Recoge " + targetWaste + (targetWaste == 1 ? " residuo" : " residuos") + " y llega a la meta";

    private void Awake()
    {
        winPanel.SetActive(false);
        losePanel.SetActive(false);
        pausePanel.SetActive(false);
        missingNotice.SetActive(false);
        objectiveBanner.gameObject.SetActive(false);
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
        ShowObjective(session.TargetWaste);
    }

    private void OnDestroy()
    {
        if (session == null) return;

        session.StateChanged -= OnStateChanged;
        session.GoalReachedWithMissingWaste -= OnGoalReachedWithMissingWaste;
    }

    private void Update()
    {
        if (missingNotice.activeSelf && Time.unscaledTime >= noticeHideTime) missingNotice.SetActive(false);
        if (objectiveBanner.gameObject.activeSelf) UpdateObjective();
    }

    private void ShowObjective(int targetWaste)
    {
        objectiveText.text = FormatObjective(targetWaste);
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
            missingNotice.SetActive(false);
            winDetail.text = "Recogiste " + session.CollectedWaste + " residuos.";
            Show(winPanel, winRestartButton);
        }
        else if (state == GameState.Lost)
        {
            objectiveBanner.gameObject.SetActive(false);
            missingNotice.SetActive(false);
            Show(losePanel, loseRestartButton);
        }
    }

    private void OnGoalReachedWithMissingWaste(int missing)
    {
        missingText.text = missing == 1
            ? "Te falta 1 residuo. ¡Vuelve a buscarlo!"
            : "Te faltan " + missing + " residuos. ¡Vuelve a buscarlos!";
        objectiveBanner.gameObject.SetActive(false); // The notice says what is left, so the goal line would only clutter.
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
