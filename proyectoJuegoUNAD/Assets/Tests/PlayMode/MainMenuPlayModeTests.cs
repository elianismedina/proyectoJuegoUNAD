using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The title screen (MainMenu scene, built by Forest Guardian &gt; UI &gt; Build Main Menu): it is the first scene of
/// the build, tells the player the goal, frees the cursor, has "Jugar" selected and "Jugar" starts Level01.
/// </summary>
public class MainMenuPlayModeTests
{
    private MainMenu menu;

    [UnitySetUp]
    public IEnumerator LoadMenu()
    {
        yield return SceneManager.LoadSceneAsync(MainMenu.SceneName, LoadSceneMode.Single);
        yield return null; // Let Awake/Start run.

        menu = Object.FindFirstObjectByType<MainMenu>();
        Assert.IsNotNull(menu, "The MainMenu scene needs the menu: run Forest Guardian > UI > Build Main Menu.");
    }

    [UnityTest]
    public IEnumerator MainMenu_IsTheFirstSceneOfTheBuild()
    {
        Assert.AreEqual(0, SceneManager.GetActiveScene().buildIndex, "The game must start on the main menu.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Menu_ShowsTheTitleTheGoalAndBothButtons()
    {
        var texts = menu.GetComponentsInChildren<Text>();
        Assert.IsTrue(System.Array.Exists(texts, t => t.text.Contains("Guardianes del Bosque")), "The menu should show the game's name.");
        Assert.IsTrue(System.Array.Exists(texts, t => t.text.Contains("residuos")), "The menu should say what the goal is.");
        Assert.IsTrue(menu.PlayButton.gameObject.activeInHierarchy);
        Assert.IsTrue(menu.QuitButton.gameObject.activeInHierarchy);
        Assert.IsNotNull(EventSystem.current, "Buttons need an EventSystem to be clicked.");
        Assert.AreEqual(menu.PlayButton.gameObject, EventSystem.current.currentSelectedGameObject,
            "\"Jugar\" should be selected so Enter or the gamepad start the game.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Menu_FreesTheCursorAndRunsTime()
    {
        Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "The cursor must be free to click the buttons.");
        Assert.AreEqual(1f, Time.timeScale);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Play_LoadsTheLevel()
    {
        menu.PlayButton.onClick.Invoke();
        yield return null; // The level loads.
        yield return null;

        Assert.AreEqual(MainMenu.LevelSceneName, SceneManager.GetActiveScene().name);
        Assert.IsNotNull(GameManager.Instance, "Level01 should be running.");
        Assert.AreEqual(GameState.Playing, GameManager.Instance.Session.State);
    }
}
