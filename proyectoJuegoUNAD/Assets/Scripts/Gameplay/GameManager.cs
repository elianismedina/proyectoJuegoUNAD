using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-level owner of the <see cref="GameSession"/>. Other systems (player, UI, audio, hazards)
/// talk to the session through <see cref="Instance"/> and subscribe to its events.
/// Pausing freezes <see cref="Time.timeScale"/>; leaving the scene always restores it.
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] private LevelConfig config;

    public static GameManager Instance { get; private set; }

    public GameSession Session { get; private set; }
    public LevelConfig Config => config;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Session = new GameSession(config != null ? config.targetWaste : 10);
        Session.StateChanged += OnStateChanged;
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        Session.StateChanged -= OnStateChanged;
        Instance = null;
        Time.timeScale = 1f;
    }

    public void AddWaste() => Session.AddWaste();
    public void Win() => Session.Win();
    public void Lose() => Session.Lose();

    public void TogglePause()
    {
        if (!Session.Pause()) Session.Resume();
    }

    /// <summary>Reloads the active scene, which starts a fresh session.</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnStateChanged(GameState state)
    {
        Time.timeScale = state == GameState.Paused ? 0f : 1f;
    }
}
