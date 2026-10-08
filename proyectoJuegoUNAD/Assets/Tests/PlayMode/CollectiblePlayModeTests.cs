using System.Collections;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Places a waste prefab in Level01 next to the player and checks the pickup: walking into it counts one waste
/// in the session and hides it, and nothing is counted once the game is over.
/// </summary>
public class CollectiblePlayModeTests
{
    private const string LevelScene = "Level01";
    private const string PrefabPath = "Assets/Prefabs/Collectibles/Collectible_Bottle.prefab";

    private GameObject player;
    private StarterAssetsInputs inputs;
    private CharacterController characterController;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        inputs = player.GetComponent<StarterAssetsInputs>();
        characterController = player.GetComponent<CharacterController>();
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

    private static Collectible Spawn(Vector3 position)
    {
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, "Missing " + PrefabPath);
        return Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<Collectible>();
#else
        Assert.Ignore("Needs the AssetDatabase to load the prefab.");
        return null;
#endif
    }

    [UnityTest]
    public IEnumerator WalkingIntoWaste_CountsItOnceAndHidesIt()
    {
        var session = GameManager.Instance.Session;
        var item = Spawn(player.transform.position + Vector3.right * 2.5f);
        Collectible raised = null;
        void OnCollected(Collectible c) => raised = c;
        Collectible.Collected += OnCollected;

        try
        {
            inputs.move = Vector2.right;
            float end = Time.time + 3f;
            while (Time.time < end && !item.IsCollected) yield return null;
            inputs.move = Vector2.zero;
            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(item.IsCollected, "Walking into the waste must collect it.");
            Assert.AreEqual(1, session.CollectedWaste, "One item counts exactly once.");
            Assert.IsFalse(item.gameObject.activeSelf, "Collected waste disappears.");
            Assert.AreEqual(item, raised, "The Collected event reports the item.");
        }
        finally
        {
            Collectible.Collected -= OnCollected;
        }
    }

    [UnityTest]
    public IEnumerator WasteIsNotCountedOnceTheGameIsOver()
    {
        var session = GameManager.Instance.Session;
        session.Lose();
        var item = Spawn(player.transform.position);

        yield return new WaitForSeconds(0.3f);

        Assert.IsFalse(item.IsCollected, "Nothing is collected after the game ends.");
        Assert.AreEqual(0, session.CollectedWaste);
        Assert.IsTrue(item.gameObject.activeSelf);
    }
}
