using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The goal at the end of Level01 and the end-of-game screens: reaching the goal early shows how much waste is
/// missing and keeps the game going, reaching it with enough waste shows "¡Bosque limpio!", and falling shows
/// "¡Inténtalo de nuevo!". Esc opens the pause menu, whose buttons resume or restart.
/// </summary>
public class GoalAndScreensPlayModeTests
{
    private const string LevelScene = "Level01";

    private GameObject player;
    private CharacterController characterController;
    private PlayerController controller;
    private LevelGoal goal;
    private GameScreens screens;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        characterController = player.GetComponent<CharacterController>();
        controller = player.GetComponent<PlayerController>();
        goal = Object.FindFirstObjectByType<LevelGoal>();
        screens = Object.FindFirstObjectByType<GameScreens>();
        Assert.IsNotNull(goal, "Level01 needs a LevelGoal.");
        Assert.IsNotNull(screens, "Level01 needs the GameScreens canvas.");
    }

    private void Teleport(Vector3 position)
    {
        characterController.enabled = false;
        player.transform.position = position;
        characterController.enabled = true;
    }

    private IEnumerator WalkIntoTheGoal()
    {
        Teleport(goal.transform.position + Vector3.up * 0.1f);
        yield return new WaitForSeconds(0.2f);
    }

    [Test]
    public void Goal_IsATriggerAtTheEndOfTheTrailThatCannotBeWalkedAround()
    {
        var trail = Object.FindFirstObjectByType<ForestTrail>();
        Assert.IsNotNull(trail, "Level01 needs the forest trail.");
        Assert.IsTrue(goal.GetComponent<Collider>().isTrigger);

        float goalS = trail.Project(goal.transform.position, out float lateral);
        Assert.Greater(goalS, trail.Length - 8f, "The goal must sit at the end of the trail.");
        Assert.Less(Mathf.Abs(lateral), 0.5f, "The goal must sit on the path.");

        // Across the goal, the walls on both sides must be closer than the trigger's edges plus a player's width.
        var bounds = goal.GetComponent<BoxCollider>();
        float halfDepth = bounds.size.z * goal.transform.lossyScale.z / 2f;
        Vector3 origin = goal.transform.position + Vector3.up * 1f;
        foreach (int side in new[] { -1, 1 })
        {
            Assert.IsTrue(Physics.Raycast(origin, goal.transform.forward * side, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore),
                "No wall beside the goal.");
            Assert.Less(hit.distance, halfDepth + 0.6f, "There is room to walk around the goal.");
        }

        foreach (var c in Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None))
            Assert.Less(trail.Project(c.transform.position, out _), goalS, c.name + " lies past the goal.");
    }

    [Test]
    public void Screens_StartHidden()
    {
        Assert.IsFalse(screens.WinShown);
        Assert.IsFalse(screens.LoseShown);
        Assert.IsFalse(screens.NoticeShown);
    }

    [UnityTest]
    public IEnumerator ReachingTheGoalWithWasteMissing_SaysHowManyAndKeepsPlaying()
    {
        var session = GameManager.Instance.Session;
        for (int i = 0; i < session.TargetWaste - 3; i++) session.AddWaste();

        yield return WalkIntoTheGoal();

        Assert.AreEqual(GameState.Playing, session.State);
        Assert.IsTrue(controller.InputEnabled, "The player must be able to go back for the missing waste.");
        Assert.IsTrue(screens.NoticeShown);
        StringAssert.Contains("Te faltan 3 residuos", screens.NoticeText);
        Assert.IsFalse(screens.WinShown);
    }

    [UnityTest]
    public IEnumerator ReachingTheGoalWithEnoughWaste_ShowsTheWinScreen()
    {
        var session = GameManager.Instance.Session;
        for (int i = 0; i < session.TargetWaste; i++) session.AddWaste();

        yield return WalkIntoTheGoal();

        Assert.AreEqual(GameState.Won, session.State);
        Assert.IsTrue(screens.WinShown, "¡Bosque limpio! must show on a win.");
        Assert.IsFalse(screens.LoseShown);
    }

    private Button PauseButton(string name)
    {
        var button = screens.transform.Find("PausePanel/Box/" + name);
        Assert.IsNotNull(button, "The pause menu needs a " + name + ".");
        return button.GetComponent<Button>();
    }

    [UnityTest]
    public IEnumerator Pause_ShowsTheMenuAndContinueResumes()
    {
        Assert.IsFalse(screens.PauseShown, "The pause menu must start hidden.");

        GameManager.Instance.TogglePause(); // Same as pressing Esc.
        yield return null;
        Assert.IsTrue(screens.PauseShown, "Pausing must show the pause menu.");
        Assert.IsFalse(screens.WinShown);
        Assert.IsFalse(screens.LoseShown);

        PauseButton("ContinueButton").onClick.Invoke();
        yield return null;
        Assert.AreEqual(GameState.Playing, GameManager.Instance.Session.State);
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsFalse(screens.PauseShown, "Continuing must hide the pause menu.");
        Assert.IsTrue(controller.InputEnabled);
    }

    [UnityTest]
    public IEnumerator Pause_EscAgainAlsoClosesTheMenu()
    {
        GameManager.Instance.TogglePause();
        yield return null;
        GameManager.Instance.TogglePause();
        yield return null;

        Assert.AreEqual(GameState.Playing, GameManager.Instance.Session.State);
        Assert.IsFalse(screens.PauseShown);
    }

    [UnityTest]
    public IEnumerator Pause_RestartStartsTheLevelAgain()
    {
        GameManager.Instance.Session.AddWaste();
        GameManager.Instance.TogglePause();
        yield return null;

        PauseButton("RestartButton").onClick.Invoke();
        yield return null; // The scene reloads.
        yield return null;

        Assert.AreEqual(1f, Time.timeScale, "Restarting from the pause menu must unfreeze time.");
        Assert.AreEqual(GameState.Playing, GameManager.Instance.Session.State);
        Assert.AreEqual(0, GameManager.Instance.Session.CollectedWaste, "Restart must start a fresh session.");
    }

    [UnityTest]
    public IEnumerator FallingOffTheCourse_ShowsTheLoseScreen()
    {
        Teleport(new Vector3(0f, GameManager.Instance.Config.killPlaneY - 1f, 0f));
        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(GameState.Lost, GameManager.Instance.Session.State);
        Assert.IsTrue(screens.LoseShown, "¡Inténtalo de nuevo! must show on a loss.");
        Assert.IsFalse(screens.WinShown);
    }
}
