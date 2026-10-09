using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// End-of-game screens from GDD §5.3: "¡Bosque limpio!" on a win and "¡Inténtalo de nuevo!" on a loss, each with a
/// button that restarts the level, plus a short notice when the player reaches the goal with waste still missing.
/// Listens to the <see cref="GameSession"/> owned by the <see cref="GameManager"/>; holds no game rules itself.
/// </summary>
public class GameScreens : MonoBehaviour
{
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Text winDetail;
    [SerializeField] private Button winRestartButton;

    [SerializeField] private GameObject losePanel;
    [SerializeField] private Button loseRestartButton;

    [SerializeField] private GameObject missingNotice;
    [SerializeField] private Text missingText;

    [SerializeField, Min(0.5f), Tooltip("Seconds the 'waste missing' notice stays on screen.")]
    private float noticeSeconds = 3f;

    private GameSession session;
    private float noticeHideTime;

    public bool WinShown => winPanel.activeSelf;
    public bool LoseShown => losePanel.activeSelf;
    public bool NoticeShown => missingNotice.activeSelf;
    public string NoticeText => missingText.text;

    private void Awake()
    {
        winPanel.SetActive(false);
        losePanel.SetActive(false);
        missingNotice.SetActive(false);
        winRestartButton.onClick.AddListener(Restart);
        loseRestartButton.onClick.AddListener(Restart);
    }

    private void Start()
    {
        if (GameManager.Instance == null) return;

        session = GameManager.Instance.Session;
        session.StateChanged += OnStateChanged;
        session.GoalReachedWithMissingWaste += OnGoalReachedWithMissingWaste;
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
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Won)
        {
            missingNotice.SetActive(false);
            winDetail.text = "Recogiste " + session.CollectedWaste + " residuos.";
            Show(winPanel, winRestartButton);
        }
        else if (state == GameState.Lost)
        {
            missingNotice.SetActive(false);
            Show(losePanel, loseRestartButton);
        }
    }

    private void OnGoalReachedWithMissingWaste(int missing)
    {
        missingText.text = missing == 1
            ? "Te falta 1 residuo. ¡Vuelve a buscarlo!"
            : "Te faltan " + missing + " residuos. ¡Vuelve a buscarlos!";
        missingNotice.SetActive(true);
        noticeHideTime = Time.unscaledTime + noticeSeconds;
    }

    private static void Show(GameObject panel, Button focus)
    {
        panel.SetActive(true);
        // Select the button so Enter / Space / gamepad confirm restart without a mouse.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
    }

    private static void Restart()
    {
        if (GameManager.Instance != null) GameManager.Instance.Restart();
    }
}
