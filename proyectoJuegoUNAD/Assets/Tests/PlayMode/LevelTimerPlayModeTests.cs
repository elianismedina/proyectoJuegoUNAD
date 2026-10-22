using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The 2-minute clock in Level01 (GDD §5.3, §10.2): it starts full and counts down, stops in the pause, keeps running
/// after a fall, warns once in the last 30 s, stops on a win (which shows the time) and, at zero, loses the game with
/// "¡Se acabó el tiempo!". Restarting puts it back to 2:00.
/// </summary>
public class LevelTimerPlayModeTests
{
    private const string LevelScene = "Level01";

    private GameObject player;
    private CharacterController characterController;
    private PlayerController controller;
    private GameScreens screens;
    private LevelTimer timer;

    private GameSession Session => GameManager.Instance.Session;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        characterController = player.GetComponent<CharacterController>();
        controller = player.GetComponent<PlayerController>();
        screens = Object.FindFirstObjectByType<GameScreens>();
        timer = Object.FindFirstObjectByType<LevelTimer>(FindObjectsInactive.Include);
        Assert.IsNotNull(screens, "Level01 needs the GameScreens canvas.");
        Assert.IsNotNull(timer, "Level01 needs the clock on the HUD (Forest Guardian > UI > Build Level Timer).");
    }

    private void Teleport(Vector3 position)
    {
        characterController.enabled = false;
        player.transform.position = position;
        characterController.enabled = true;
    }

    [UnityTest]
    public IEnumerator Clock_StartsAtTwoMinutesAndCountsDown()
    {
        Assert.IsTrue(Session.HasTimeLimit);
        Assert.AreEqual(120f, Session.TimeLimit);
        Assert.IsTrue(timer.Shown);
        StringAssert.Contains("Tiempo: ", timer.Text);

        float before = Session.TimeRemaining;
        yield return new WaitForSeconds(1.2f);

        Assert.Less(Session.TimeRemaining, before - 1f);
        Assert.AreEqual(LevelTimer.Format(Session.TimeRemaining), timer.Text);
        StringAssert.Contains("en 2:00", screens.ObjectiveText, "The objective banner names the time limit.");
    }

    [UnityTest]
    public IEnumerator Pause_StopsTheClock()
    {
        GameManager.Instance.TogglePause();
        yield return null;
        float paused = Session.TimeRemaining;
        yield return new WaitForSecondsRealtime(0.5f);

        Assert.AreEqual(paused, Session.TimeRemaining, "The clock must not run in the pause.");
        Assert.IsTrue(timer.Shown);

        GameManager.Instance.Resume();
        yield return new WaitForSeconds(0.3f);
        Assert.Less(Session.TimeRemaining, paused, "Continuar must start the clock again.");
    }

    [UnityTest]
    public IEnumerator LastSeconds_WarnOnce()
    {
        Assert.IsFalse(timer.WarningShown);

        Session.Tick(Session.TimeRemaining - 31f);
        yield return null;
        Assert.IsFalse(timer.WarningShown, "No warning before the last 30 s.");

        Session.Tick(2f);
        yield return null;
        Assert.IsTrue(timer.WarningShown);
        Assert.AreEqual(1, timer.WarningSoundsPlayed);

        Session.Tick(5f);
        yield return null;
        Assert.AreEqual(1, timer.WarningSoundsPlayed, "The warning plays once.");
        Assert.AreEqual(GameState.Playing, Session.State);
    }

    [UnityTest]
    public IEnumerator RunningOutOfTime_ShowsTheLoseScreenWithTheReason()
    {
        GameManager.Instance.RunOutOfTime();
        yield return null;

        Assert.AreEqual(GameState.Lost, Session.State);
        Assert.AreEqual(LoseReason.TimeUp, Session.LoseReason);
        Assert.IsTrue(screens.LoseShown, "¡Inténtalo de nuevo! must show when time runs out.");
        Assert.AreEqual(GameScreens.TimeUpDetail, screens.LoseDetailText);
        Assert.IsFalse(screens.WinShown);
        Assert.IsFalse(timer.Shown, "The clock hides on the lose screen.");
        Assert.IsFalse(controller.InputEnabled, "The player stops once the game is lost.");
    }

    [UnityTest]
    public IEnumerator Falling_DoesNotResetTheClock()
    {
        Session.Tick(50f);
        float before = Session.TimeRemaining;

        Teleport(new Vector3(0f, GameManager.Instance.Config.killPlaneY - 1f, 0f));
        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(GameState.Playing, Session.State);
        Assert.LessOrEqual(Session.TimeRemaining, before, "A fall costs time; it does not give it back.");
    }

    [UnityTest]
    public IEnumerator Winning_StopsTheClockAndShowsTheTime()
    {
        Session.Tick(42f);
        for (int i = 0; i < Session.TargetWaste; i++) Session.AddWaste();
        Session.ReachGoal();
        float left = Session.TimeRemaining;
        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(GameState.Won, Session.State);
        Assert.AreEqual(left, Session.TimeRemaining, "The clock stops on a win.");
        StringAssert.Contains(" en 0:4", screens.WinDetailText, "The win screen says how long it took.");
        Assert.IsFalse(timer.Shown);
    }

    [UnityTest]
    public IEnumerator Restart_PutsTheClockBackToTwoMinutes()
    {
        Session.Tick(60f);
        GameManager.Instance.Restart();
        yield return null;
        yield return null;

        Assert.AreEqual(GameState.Playing, GameManager.Instance.Session.State);
        Assert.Greater(GameManager.Instance.Session.TimeRemaining, 119f);
    }
}
