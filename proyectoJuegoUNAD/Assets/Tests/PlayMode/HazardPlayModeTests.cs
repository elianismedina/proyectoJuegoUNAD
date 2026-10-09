using System.Collections;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Drives the hazards placed in Level01 (mud, rolling log, falling rock) and checks their gameplay effect.
/// Positions are read from the scene, so moving a hazard does not break the tests.
/// </summary>
public class HazardPlayModeTests
{
    private const string LevelScene = "Level01";

    private GameObject player;
    private PlayerController controller;
    private StarterAssetsInputs inputs;
    private CharacterController characterController;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        controller = player.GetComponent<PlayerController>();
        inputs = player.GetComponent<StarterAssetsInputs>();
        characterController = player.GetComponent<CharacterController>();
        PlayerTestRig.FaceCourse(player);
        Teleport(new Vector3(-3f, 0.1f, 0f));
        yield return new WaitForSeconds(0.5f);
    }

    [TearDown]
    public void Cleanup()
    {
        inputs.move = Vector2.zero;
    }

    private void Teleport(Vector3 position)
    {
        characterController.enabled = false;
        player.transform.position = position;
        characterController.enabled = true;
    }

    [UnityTest]
    public IEnumerator Mud_SlowsThePlayerWhileInsideAndReleasesOnExit()
    {
        var mud = Object.FindFirstObjectByType<MudZone>();
        Assert.IsNotNull(mud, "Level01 needs a MudZone.");
        var box = mud.GetComponent<BoxCollider>().bounds;

        Teleport(new Vector3(box.center.x, 0.1f, 0f));
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(0.5f, controller.SpeedMultiplier, 0.01f, "Mud should halve the speed.");

        // Walk out of the right edge.
        Teleport(new Vector3(box.max.x - 0.5f, 0.1f, 0f));
        inputs.move = Vector2.up; // Forward, toward +X and out of the mud.
        yield return new WaitForSeconds(1f);
        Assert.AreEqual(1f, controller.SpeedMultiplier, 0.01f, "Speed must return to normal after leaving the mud (player x=" + player.transform.position.x + ", mud max x=" + box.max.x + ").");
    }

    [UnityTest]
    public IEnumerator RollingLog_StumblesThePlayerOnContact()
    {
        var log = Object.FindFirstObjectByType<RollingLog>();
        Assert.IsNotNull(log, "Level01 needs a RollingLog.");

        Teleport(new Vector3(log.transform.position.x, 0.1f, 0f));
        bool stumbled = false;
        float end = Time.time + 0.6f;
        while (Time.time < end && !stumbled)
        {
            stumbled = controller.IsStumbling;
            yield return null;
        }

        Assert.IsTrue(stumbled, "Touching the rolling log must stumble the player.");
    }

    [UnityTest]
    public IEnumerator FallingRock_WarnsThenDropsThenDisappears()
    {
        var rock = Object.FindFirstObjectByType<FallingRock>(FindObjectsInactive.Include);
        Assert.IsNotNull(rock, "Level01 needs a FallingRock.");
        var container = rock.transform.parent;
        var trigger = container.Find("Trigger");
        var marker = container.Find("WarningMarker");
        var rockRenderer = rock.GetComponentInChildren<MeshRenderer>(true);
        Assert.IsNotNull(trigger);
        Assert.IsNotNull(marker);

        Assert.IsFalse(rockRenderer.enabled, "The rock starts hidden.");
        Assert.IsFalse(marker.gameObject.activeSelf, "The warning starts hidden.");

        // Step into the trigger, then leave straight away so the rock does not hit the player.
        Teleport(new Vector3(trigger.position.x, 0.1f, 0f));
        yield return new WaitForSeconds(0.2f);
        Teleport(new Vector3(trigger.position.x - 12f, 0.1f, 0f));

        yield return new WaitForSeconds(0.2f);
        Assert.IsTrue(marker.gameObject.activeSelf, "The ground warning must show before the rock falls.");
        Assert.IsFalse(rockRenderer.enabled, "The rock must not be visible during the warning.");

        yield return new WaitForSeconds(1.4f); // warning (1 s) has ended; the rock is falling or landed.
        Assert.IsTrue(rockRenderer.enabled, "The rock must be visible once it drops.");

        yield return new WaitForSeconds(2.5f);
        Assert.IsFalse(rockRenderer.enabled, "The rock disappears shortly after landing.");
        Assert.IsFalse(marker.gameObject.activeSelf, "The warning is hidden after impact.");
    }
}
