using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The HUD counter in Level01: it starts at "Residuos: 0/N" with N from <see cref="LevelConfig.targetWaste"/>
/// and goes up by one each time the player picks up waste.
/// </summary>
public class WasteCounterPlayModeTests
{
    private const string LevelScene = "Level01";

    private GameObject player;
    private CharacterController characterController;
    private WasteCounter counter;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        characterController = player.GetComponent<CharacterController>();
        counter = Object.FindFirstObjectByType<WasteCounter>();
        Assert.IsNotNull(counter, "Level01 needs the waste counter on the HUD.");
    }

    private void Teleport(Vector3 position)
    {
        characterController.enabled = false;
        player.transform.position = position;
        characterController.enabled = true;
    }

    [Test]
    public void Counter_StartsAtZeroOverTheConfiguredTarget()
    {
        int target = GameManager.Instance.Config.targetWaste;
        Assert.AreEqual("Residuos: 0/" + target, counter.Text);
    }

    [UnityTest]
    public IEnumerator PickingUpWaste_RaisesTheCounter()
    {
        var items = Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None);
        Assert.GreaterOrEqual(items.Length, 2, "Level01 needs waste to pick up.");
        int target = GameManager.Instance.Session.TargetWaste;

        Teleport(items[0].transform.position);
        yield return new WaitForSeconds(0.15f);
        Assert.IsTrue(items[0].IsCollected);
        Assert.AreEqual("Residuos: 1/" + target, counter.Text);

        Teleport(items[1].transform.position);
        yield return new WaitForSeconds(0.15f);
        Assert.IsTrue(items[1].IsCollected);
        Assert.AreEqual("Residuos: 2/" + target, counter.Text);
    }
}
