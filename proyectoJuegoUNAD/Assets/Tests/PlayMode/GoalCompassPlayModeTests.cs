using System.Collections;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The HUD arrow toward the goal in Level01: it points down the trail from where the player is, warns when the player
/// keeps running back toward the start, stops warning once they turn round, and hides while the game is paused.
/// </summary>
public class GoalCompassPlayModeTests
{
    private const string LevelScene = "Level01";
    private const float TestS = 52f; // Zone 1, flat path between the log fence (s 61) and the log across the path (s 35)

    private GameObject player;
    private PlayerController controller;
    private StarterAssetsInputs inputs;
    private ForestTrail trail;
    private GoalCompass compass;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        controller = player.GetComponent<PlayerController>();
        inputs = player.GetComponent<StarterAssetsInputs>();
        trail = Object.FindFirstObjectByType<ForestTrail>();
        compass = Object.FindFirstObjectByType<GoalCompass>();
        Assert.IsNotNull(compass, "Level01 needs the goal compass on the GameScreens canvas.");

        controller.Respawn(trail.PointAt(TestS) + Vector3.up * 0.05f, Quaternion.LookRotation(trail.TangentAt(TestS)));
        yield return null;
    }

    [TearDown]
    public void StopInput()
    {
        if (inputs != null) inputs.move = Vector2.zero;
    }

    [UnityTest]
    public IEnumerator Arrow_PointsDownTheTrailAndDoesNotWarnAStandingPlayer()
    {
        yield return new WaitForSeconds(0.8f); // the arrow glides to its heading

        float s = trail.Project(player.transform.position, out _);
        Vector3 ahead = trail.PointAt(s + 10f) - player.transform.position;
        float expected = GoalCompass.AngleOnScreen(Camera.main.transform.forward, ahead);
        Assert.AreEqual(0f, Mathf.DeltaAngle(expected, compass.ArrowAngle), 10f, "The arrow must point down the trail, as seen from the camera.");
        Assert.IsFalse(compass.WrongWayShown);
    }

    [UnityTest]
    public IEnumerator RunningBackTowardTheStart_WarnsAndTurningRoundClearsIt()
    {
        PlayerTestRig.Face(player, -trail.TangentAt(TestS));
        inputs.move = Vector2.up;
        yield return new WaitForSeconds(1.8f);
        Assert.IsTrue(compass.WrongWayShown, "Running back along the trail must show the warning.");

        PlayerTestRig.Face(player, trail.TangentAt(trail.Project(player.transform.position, out _)));
        yield return new WaitForSeconds(0.6f);
        Assert.IsFalse(compass.WrongWayShown, "Heading for the goal again must clear the warning.");
    }

    [UnityTest]
    public IEnumerator Pausing_HidesTheArrow()
    {
        GameManager.Instance.TogglePause();
        yield return null;
        Assert.IsFalse(compass.transform.Find("Arrow").gameObject.activeSelf, "The arrow must not show over the pause menu.");

        GameManager.Instance.Resume();
        yield return null;
        Assert.IsTrue(compass.transform.Find("Arrow").gameObject.activeSelf);
    }
}
