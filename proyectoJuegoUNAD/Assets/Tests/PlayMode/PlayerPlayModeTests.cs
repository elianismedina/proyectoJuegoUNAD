using System.Collections;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Loads Level01 and drives the player through <see cref="StarterAssetsInputs"/> (the same values
/// PlayerInput writes from the keyboard), so no physical input device is needed. Movement is relative to the
/// camera, so the tests pin it to a known reference with <see cref="PlayerTestRig.FaceCourse"/>.
/// </summary>
public class PlayerPlayModeTests
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
        yield return null; // Let Awake/Start run.

        player = GameObject.FindGameObjectWithTag("Player");
        Assert.IsNotNull(player, "Level01 needs a GameObject tagged Player.");
        controller = player.GetComponent<PlayerController>();
        inputs = player.GetComponent<StarterAssetsInputs>();
        characterController = player.GetComponent<CharacterController>();

        // Start every test from a known spot on the flat part of the ground, away from the provisional hazards.
        Teleport(new Vector3(-3f, 0.1f, 0f));
        yield return new WaitForSeconds(0.6f);
    }

    [TearDown]
    public void ResetTimeScale()
    {
        Time.timeScale = 1f;
    }

    private void Teleport(Vector3 position)
    {
        characterController.enabled = false;
        player.transform.position = position;
        characterController.enabled = true;
    }

    [UnityTest]
    public IEnumerator Player_IsGroundedOnSpawn()
    {
        Assert.IsTrue(controller.Grounded, "Player should be standing on the ground after spawning.");
        Assert.Less(player.transform.position.y, 0.3f);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Jump_LeavesTheGroundAndLandsAgain()
    {
        float startY = player.transform.position.y;
        float maxY = startY;

        inputs.jump = true; // Same as pressing Space.
        float end = Time.time + 0.5f;
        while (Time.time < end)
        {
            maxY = Mathf.Max(maxY, player.transform.position.y);
            yield return null;
        }

        Assert.Greater(maxY - startY, controller.JumpHeight * 0.8f, "Jump should reach close to the configured height.");

        yield return new WaitForSeconds(1.2f);
        Assert.IsTrue(controller.Grounded, "Player should land again.");
    }

    [UnityTest]
    public IEnumerator JumpShadow_StaysOnTheGroundBelowThePlayerAndShrinksInTheAir()
    {
        var jumpShadow = player.GetComponent<JumpShadow>();
        Assert.IsNotNull(jumpShadow, "The player prefab needs a JumpShadow so landings can be judged in 3D.");
        Assert.IsTrue(jumpShadow.IsVisible, "The shadow should show while standing on the ground.");

        float groundY = jumpShadow.Shadow.position.y;
        float groundSize = jumpShadow.Shadow.lossyScale.x;
        Assert.Less(Mathf.Abs(groundY - player.transform.position.y), 0.1f, "On the ground the shadow sits at the feet.");

        inputs.jump = true;
        yield return new WaitForSeconds(0.35f); // Close to the top of the jump.

        Vector3 offset = jumpShadow.Shadow.position - player.transform.position;
        Assert.Greater(player.transform.position.y - groundY, 1f, "The player should be well up in the air.");
        Assert.Less(new Vector2(offset.x, offset.z).magnitude, 0.05f, "The shadow should be straight below the player.");
        Assert.Less(Mathf.Abs(jumpShadow.Shadow.position.y - groundY), 0.05f, "The shadow should stay on the ground.");
        Assert.Less(jumpShadow.Shadow.lossyScale.x, groundSize, "The shadow should shrink as the player rises.");
    }

    [UnityTest]
    public IEnumerator JumpShadow_DisappearsOverAStream()
    {
        var trail = Object.FindFirstObjectByType<ForestTrail>();
        Assert.IsNotNull(trail, "Level01 needs the forest trail.");
        var span = trail.StreamSpans[0];

        Teleport(trail.PointAt((span.x + span.y) / 2f) + Vector3.up * 1.5f);
        yield return null; // One LateUpdate.

        Assert.IsFalse(player.GetComponent<JumpShadow>().IsVisible, "With no ground below, there should be no shadow.");
    }

    // Sideways runs are kept short so the player stays on the flat start of the trail.
    private const float SideRunSeconds = 0.25f;

    [UnityTest]
    public IEnumerator ForwardInput_MovesAlongTheCameraForwardAndFacesIt()
    {
        var reference = PlayerTestRig.FaceCourse(player); // Camera looking toward +X.
        Vector3 start = player.transform.position;

        inputs.move = Vector2.up;
        yield return new WaitForSeconds(0.5f);
        inputs.move = Vector2.zero;

        Vector3 moved = player.transform.position - start;
        Assert.Greater(moved.x, 1f, "Forward input should move along the camera's forward (+X here).");
        Assert.AreEqual(0f, moved.z, 0.1f, "Forward input must not drift sideways.");
        Assert.AreEqual(reference.eulerAngles.y, player.transform.eulerAngles.y, 5f, "Player should face where it runs.");
    }

    [UnityTest]
    public IEnumerator SideInput_MovesAlongTheCameraRightAndFacesIt()
    {
        PlayerTestRig.FaceCourse(player); // Camera looking toward +X, so its right is -Z.
        Vector3 start = player.transform.position;

        inputs.move = Vector2.right;
        yield return new WaitForSeconds(SideRunSeconds);
        inputs.move = Vector2.zero;
        yield return new WaitForSeconds(0.3f); // Let the turn finish.

        Vector3 moved = player.transform.position - start;
        Assert.Less(moved.z, -0.5f, "Right input should move along the camera's right (-Z here).");
        Assert.AreEqual(0f, moved.x, 0.15f, "Right input must not move forward.");
        Assert.AreEqual(180f, player.transform.eulerAngles.y, 5f, "Player should face -Z.");
    }

    [UnityTest]
    public IEnumerator Movement_FollowsTheCameraWhenItTurns()
    {
        var reference = PlayerTestRig.FaceCourse(player);
        reference.rotation = Quaternion.Euler(0f, 0f, 0f); // Camera now looks toward +Z.
        Vector3 start = player.transform.position;

        inputs.move = Vector2.up;
        yield return new WaitForSeconds(SideRunSeconds);
        inputs.move = Vector2.zero;

        Vector3 moved = player.transform.position - start;
        Assert.Greater(moved.z, 0.5f, "Forward input should follow the camera's new forward (+Z).");
        Assert.AreEqual(0f, moved.x, 0.15f);
    }

    [UnityTest]
    public IEnumerator ReleasingInput_StopsThePlayer()
    {
        PlayerTestRig.FaceCourse(player);
        inputs.move = Vector2.up;
        yield return new WaitForSeconds(0.4f);
        inputs.move = Vector2.zero;
        yield return new WaitForSeconds(0.6f);

        Assert.Less(controller.HorizontalVelocity.magnitude, 0.05f, "The player should come to a stop without input.");
    }

    [UnityTest]
    public IEnumerator Stumble_PushesThePlayerAlongTheGivenDirection()
    {
        Vector3 start = player.transform.position;

        Assert.IsTrue(controller.Stumble(new Vector3(0f, 5f, -1f)), "A first hit must stumble the player.");
        Assert.IsTrue(controller.IsStumbling);
        yield return new WaitForSeconds(0.3f);

        Vector3 moved = player.transform.position - start;
        Assert.Less(moved.z, -0.2f, "The knockback should follow the hit direction on the ground plane (-Z).");
        Assert.AreEqual(0f, moved.x, 0.05f);
        Assert.IsFalse(controller.Stumble(Vector3.back), "Hits during the immunity window are ignored.");
    }

    [UnityTest]
    public IEnumerator FallingBelowKillPlane_RaisesLost()
    {
        Assert.IsNotNull(GameManager.Instance, "Level01 needs a GameManager.");
        Assert.AreEqual(GameState.Playing, GameManager.Instance.Session.State);

        Teleport(new Vector3(0f, GameManager.Instance.Config.killPlaneY - 1f, 0f));
        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(GameState.Lost, GameManager.Instance.Session.State);
        Assert.IsFalse(controller.InputEnabled, "Input must be disabled after losing.");
    }

    [UnityTest]
    public IEnumerator Pause_FreezesTimeAndDisablesInput()
    {
        GameManager.Instance.TogglePause();
        yield return null;
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsFalse(controller.InputEnabled);

        GameManager.Instance.TogglePause();
        yield return null;
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsTrue(controller.InputEnabled);
    }

    [UnityTest]
    public IEnumerator CollectingTargetWaste_WinsTheLevel()
    {
        var session = GameManager.Instance.Session;
        for (int i = 0; i < session.TargetWaste; i++) GameManager.Instance.AddWaste();
        GameManager.Instance.ReachGoal();
        yield return null;

        Assert.AreEqual(GameState.Won, session.State);
        Assert.IsFalse(controller.InputEnabled, "Input must be disabled after winning.");
    }
}
