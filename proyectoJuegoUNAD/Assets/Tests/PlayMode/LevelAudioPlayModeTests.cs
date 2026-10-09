using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Level01 sound: the ambience starts with the level and picking up waste plays the pickup sound once per item.
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
