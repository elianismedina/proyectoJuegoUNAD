using System.Collections;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Drives the hazards placed in Level01 (mud, rolling log, falling rock) and checks their gameplay effect.
/// Positions and directions are read from the scene, so moving or turning a hazard does not break the tests.
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
        var box = mud.GetComponent<BoxCollider>();
        Vector3 center = mud.transform.TransformPoint(box.center);
        Vector3 along = mud.transform.right; // The mud is laid along the trail.
        float halfLength = box.size.x * mud.transform.lossyScale.x / 2f;

        Teleport(new Vector3(center.x, mud.transform.position.y + 0.1f, center.z));
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(0.5f, controller.SpeedMultiplier, 0.01f, "Mud should halve the speed.");

        // Walk out of the far edge, along the trail.
        Vector3 nearEdge = center + along * (halfLength - 0.5f);
        Teleport(new Vector3(nearEdge.x, mud.transform.position.y + 0.1f, nearEdge.z));
        PlayerTestRig.Face(player, along);
        inputs.move = Vector2.up;
        yield return new WaitForSeconds(1f);
        Assert.AreEqual(1f, controller.SpeedMultiplier, 0.01f, "Speed must return to normal after leaving the mud (player at " + player.transform.position + ").");
    }

    [UnityTest]
    public IEnumerator RollingLog_StumblesThePlayerOnContact()
    {
        var log = Object.FindFirstObjectByType<RollingLog>();
        Assert.IsNotNull(log, "Level01 needs a RollingLog.");

        Teleport(log.transform.position - Vector3.up * 0.4f); // The log's centre is 0.5 m above the ground.
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
        Vector3 ground = new Vector3(trigger.position.x, container.position.y + 0.1f, trigger.position.z);
        Teleport(ground);
        yield return new WaitForSeconds(0.2f);
        Teleport(ground + container.forward * 4.5f); // Step aside, off the path (local Z is the trail's left).

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
