using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Level01 sound: the ambience starts with the level and picking up waste plays the pickup sound once per item.
/// Stumbling, reaching the goal with waste missing, winning and losing each play their own sound.
/// A polluted zone sounds polluted (bosque_contaminado_sound); recovering it fades to the living forest.
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
    public IEnumerator FallingIntoAStream_PlaysTheLoseSoundAndTheGameGoesOn()
    {
        Teleport(new Vector3(0f, GameManager.Instance.Config.killPlaneY - 1f, 0f));
        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(GameState.Playing, GameManager.Instance.Session.State);
        Assert.AreEqual(levelAudio.LoseClip, levelAudio.LastEventClip);
    }

    [UnityTest]
    public IEnumerator Losing_PlaysTheLoseSound()
    {
        GameManager.Instance.Lose();
        yield return null;

        Assert.AreEqual(GameState.Lost, GameManager.Instance.Session.State);
        Assert.AreEqual(levelAudio.LoseClip, levelAudio.LastEventClip);
    }

    [UnityTest]
    public IEnumerator PollutedZone_SoundsPollutedUntilItIsRecoveredThenFadesToTheLivingForest()
    {
        Assert.IsNotNull(levelAudio.PollutedClip, "LevelAudio needs the polluted ambience.");
        Assert.AreEqual("bosque_contaminado_sound", levelAudio.PollutedClip.name);
        yield return null;

        // Zone 1 starts polluted.
        Assert.AreEqual(1f, levelAudio.PollutedMix, 0.001f, "A polluted zone plays the polluted ambience.");
        Assert.Greater(levelAudio.PollutedAmbienceVolume, 0f);
        Assert.AreEqual(0f, levelAudio.LivingAmbienceVolume, 0.001f, "The living forest is silent in a polluted zone.");

        // Recover Zone 1: collect all of its waste, then stand at its checkpoint.
        var trail = Object.FindFirstObjectByType<ForestTrail>();
        foreach (var c in Object.FindObjectsByType<Collectible>())
        {
            if (trail.ZoneAt(trail.Project(c.transform.position, out _)) != 0) continue;
            Teleport(c.transform.position);
            yield return new WaitForSeconds(0.15f);
        }
        var zone1 = Checkpoint.ForZone(0);
        player.GetComponent<PlayerController>().Respawn(zone1.transform.position, zone1.transform.rotation);
        Assert.IsTrue(ForestRecovery.Current.Tracker.IsRecovered(0));

        yield return new WaitForSecondsRealtime(1f);
        Assert.That(levelAudio.PollutedMix, Is.InRange(0.05f, 0.95f), "The switch is a fade, not a cut.");

        yield return new WaitForSecondsRealtime(2.5f);
        Assert.AreEqual(0f, levelAudio.PollutedMix, 0.001f, "Once recovered, the zone sounds like a living forest.");
        Assert.Greater(levelAudio.LivingAmbienceVolume, 0f);
        Assert.AreEqual(0f, levelAudio.PollutedAmbienceVolume, 0.001f);

        // Walking on into Zone 2, still polluted, fades back.
        var zone2 = Checkpoint.ForZone(1);
        player.GetComponent<PlayerController>().Respawn(zone2.transform.position, zone2.transform.rotation);
        yield return new WaitForSecondsRealtime(3.5f);
        Assert.AreEqual(1f, levelAudio.PollutedMix, 0.001f, "Zone 2 is still polluted.");
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
