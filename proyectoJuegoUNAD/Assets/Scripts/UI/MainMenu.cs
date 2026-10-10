using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The title screen in the MainMenu scene: "Jugar" loads Level01 and "Salir" closes the game.
/// The scene itself (canvas, texts, buttons) is built by the Editor tool
/// <c>Forest Guardian &gt; UI &gt; Build Main Menu</c> (<c>Scripts/Editor/MainMenuBuilder.cs</c>).
/// </summary>
public class MainMenu : MonoBehaviour
{
    public const string SceneName = "MainMenu";
    public const string LevelSceneName = "Level01";

    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    public Button PlayButton => playButton;
    public Button QuitButton => quitButton;

    private void Awake()
    {
        playButton.onClick.AddListener(Play);
        quitButton.onClick.AddListener(Quit);
    }

    private void Start()
    {
        // The level locks the cursor while playing; coming back here from the pause menu it must be free again.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        // Select "Jugar" so Enter, Space or the gamepad start the game without a mouse.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(playButton.gameObject);
    }

    private static void Play()
    {
        SceneManager.LoadScene(LevelSceneName);
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
