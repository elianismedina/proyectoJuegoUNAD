using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Level01 sound: the ambience starts with the level and picking up waste plays the pickup sound once per item.
/// Stumbling, reaching the goal with waste missing, winning and losing each play their own sound.
/// </summary>
public class LevelAudioPlayModeTests
{
    private const string LevelScene = "Level01";

    private GameObject player;
    private CharacterController characterController;
    private LevelAudio levelAudio;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        characterController = player.GetComponent<CharacterController>();
        levelAudio = Object.FindFirstObjectByType<LevelAudio>();
        Assert.IsNotNull(levelAudio, "Level01 needs a LevelAudio object.");
    }

    private void Teleport(Vector3 position)
    {
        characterController.enabled = false;
        player.transform.position = position;
        characterController.enabled = true;
    }

    [Test]
    public void Ambience_StartsWithTheLevel()
    {
        Assert.IsNotNull(levelAudio.CurrentAmbientClip, "LevelAudio needs at least one ambient clip.");
        Assert.IsNotNull(levelAudio.PickupClip, "LevelAudio needs the pickup clip.");
        Assert.IsNotNull(levelAudio.StumbleClip, "LevelAudio needs the stumble clip.");
        Assert.IsNotNull(levelAudio.GoalMissingClip, "LevelAudio needs the goal-with-waste-missing clip.");
        Assert.IsNotNull(levelAudio.WinClip, "LevelAudio needs the win clip.");
        Assert.IsNotNull(levelAudio.LoseClip, "LevelAudio needs the lose clip.");
    }

    [UnityTest]
    public IEnumerator Stumbling_PlaysTheStumbleSoundOncePerHit()
    {
        var controller = player.GetComponent<PlayerController>();
        Assert.IsTrue(controller.Stumble(Vector3.back));
        Assert.IsFalse(controller.Stumble(Vector3.back), "A second hit while immune is ignored.");
        yield return null;

        Assert.AreEqual(1, levelAudio.EventSoundsPlayed, "Only the hit that knocked the player back makes a sound.");
        Assert.AreEqual(levelAudio.StumbleClip, levelAudio.LastEventClip);
    }

    [UnityTest]
    public IEnumerator ReachingTheGoalWithWasteMissing_PlaysTheGoalMissingSound()
    {
        GameManager.Instance.ReachGoal();
        yield return null;

        Assert.AreEqual(GameState.Playing, GameManager.Instance.Session.State);
        Assert.AreEqual(levelAudio.GoalMissingClip, levelAudio.LastEventClip);
    }

    [UnityTest]
    public IEnumerator Winning_PlaysTheWinSound()
    {
        var session = GameManager.Instance.Session;
        for (int i = 0; i < session.TargetWaste; i++) session.AddWaste();
        GameManager.Instance.ReachGoal();
        yield return null;

        Assert.AreEqual(GameState.Won, session.State);
        Assert.AreEqual(levelAudio.WinClip, levelAudio.LastEventClip);
        Assert.AreEqual(1, levelAudio.EventSoundsPlayed, "Winning must not also play the goal-missing sound.");
    }

    [UnityTest]
    public IEnumerator Losing_PlaysTheLoseSound()
    {
        Teleport(new Vector3(0f, GameManager.Instance.Config.killPlaneY - 1f, 0f));
        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(GameState.Lost, GameManager.Instance.Session.State);
        Assert.AreEqual(levelAudio.LoseClip, levelAudio.LastEventClip);
    }

    [UnityTest]
    public IEnumerator PickingUpWaste_PlaysThePickupSound()
    {
        var items = Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None);
        Assert.GreaterOrEqual(items.Length, 1, "Level01 needs waste to pick up.");

        Teleport(items[0].transform.position);
        yield return new WaitForSeconds(0.15f);

        Assert.IsTrue(items[0].IsCollected);
        Assert.AreEqual(1, levelAudio.PickupSoundsPlayed);
    }
}
