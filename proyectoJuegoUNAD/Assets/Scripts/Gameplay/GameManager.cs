using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-level owner of the <see cref="GameSession"/>. Other systems (player, UI, audio, hazards)
/// talk to the session through <see cref="Instance"/> and subscribe to its events.
/// Pausing freezes <see cref="Time.timeScale"/>; leaving the scene always restores it.
/// It also runs the session's clock (<see cref="LevelConfig.timeLimitSeconds"/>) with game time, so a pause stops it.
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
        Session = config != null ? new GameSession(config.targetWaste, config.timeLimitSeconds) : new GameSession(10);
        Session.StateChanged += OnStateChanged;
        Time.timeScale = 1f;
    }

    private void Update() => Session.Tick(Time.deltaTime);

    private void OnDestroy()
    {
        if (Instance != this) return;

        Session.StateChanged -= OnStateChanged;
        Instance = null;
        Time.timeScale = 1f;
    }

    public void AddWaste() => Session.AddWaste();
    public void ReachGoal() => Session.ReachGoal();
    public void Win() => Session.Win();
    public void Lose() => Session.Lose();

    /// <summary>Runs the clock out at once, as if the time limit had passed (for tests).</summary>
    public void RunOutOfTime() => Session.Tick(Session.TimeRemaining);

    public void TogglePause()
    {
        if (!Session.Pause()) Session.Resume();
    }

    /// <summary>Leaves the pause (the pause menu's "Continuar"); does nothing unless paused.</summary>
    public void Resume() => Session.Resume();

    /// <summary>Reloads the active scene, which starts a fresh session.</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Leaves the level for the title screen (the pause menu's "Salir").</summary>
    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenu.SceneName);
    }

    private void OnStateChanged(GameState state)
    {
        Time.timeScale = state == GameState.Paused ? 0f : 1f;
    }
}
