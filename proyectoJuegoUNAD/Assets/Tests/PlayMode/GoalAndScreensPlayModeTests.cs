using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The goal at the end of Level01 and the end-of-game screens: reaching the goal early shows how much waste is
/// missing and keeps the game going, reaching it with enough waste shows "¡Bosque limpio!", and falling shows
/// "¡Inténtalo de nuevo!".
/// </summary>
public class GoalAndScreensPlayModeTests
{
    private const string LevelScene = "Level01";

    // Must match LevelEnvironmentBuilder.
    private const float CourseEnd = 168f;

    private GameObject player;
    private CharacterController characterController;
    private SideScrollerController controller;
    private LevelGoal goal;
    private GameScreens screens;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        characterController = player.GetComponent<CharacterController>();
        controller = player.GetComponent<SideScrollerController>();
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
        Teleport(new Vector3(goal.transform.position.x, 0.1f, 0f));
        yield return new WaitForSeconds(0.2f);
    }

    [Test]
    public void Goal_IsATriggerAtTheEndOfTheCourse()
    {
        var bounds = goal.GetComponent<Collider>().bounds;
        Assert.IsTrue(goal.GetComponent<Collider>().isTrigger);
        Assert.That(bounds.max.x, Is.GreaterThan(CourseEnd - 1f), "The goal must reach the end of the lane.");
        Assert.That(bounds.min.x, Is.GreaterThan(CourseEnd - 8f), "The goal must sit at the end of the course.");
        foreach (var c in Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None))
            Assert.Less(c.transform.position.x, bounds.min.x, c.name + " lies past the goal.");
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
