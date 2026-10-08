using System.Collections;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Loads Level01 and drives the player through <see cref="StarterAssetsInputs"/> (the same values
/// PlayerInput writes from the keyboard), so no physical input device is needed.
/// </summary>
public class PlayerPlayModeTests
{
    private const string LevelScene = "Level01";

    private GameObject player;
    private SideScrollerController controller;
    private StarterAssetsInputs inputs;
    private CharacterController characterController;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null; // Let Awake/Start run.

        player = GameObject.FindGameObjectWithTag("Player");
        Assert.IsNotNull(player, "Level01 needs a GameObject tagged Player.");
        controller = player.GetComponent<SideScrollerController>();
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
    public IEnumerator Running_KeepsZConstant()
    {
        float z = player.transform.position.z;
        inputs.move = Vector2.right;
        yield return new WaitForSeconds(1f);
        inputs.move = Vector2.left;
        yield return new WaitForSeconds(2f);
        inputs.move = Vector2.zero;

        Assert.AreEqual(z, player.transform.position.z, 0.01f, "Z must stay on the course plane.");
    }

    [UnityTest]
    public IEnumerator Running_MovesAlongXAndFacesTravelDirection()
    {
        float startX = player.transform.position.x;
        inputs.move = Vector2.right;
        yield return new WaitForSeconds(0.5f);
        Assert.Greater(player.transform.position.x, startX + 1f, "Right input should move toward +X.");
        Assert.AreEqual(90f, player.transform.eulerAngles.y, 5f, "Player should face +X.");

        inputs.move = Vector2.left;
        yield return new WaitForSeconds(0.6f);
        inputs.move = Vector2.zero;
        Assert.AreEqual(270f, player.transform.eulerAngles.y, 5f, "Player should face -X.");
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
