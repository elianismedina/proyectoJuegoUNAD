using System.Collections;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Checks that the environment prefabs behave as the level design needs (see Docs/NaturePackIntegrationPlan.md, phase B):
/// scenery never blocks the player, solid obstacles do, and every obstacle can be jumped over.
/// </summary>
public class EnvironmentPlayModeTests
{
    private const string LevelScene = "Level01";
    private const string PrefabRoot = "Assets/Prefabs/Environment/";

    private GameObject player;
    private StarterAssetsInputs inputs;
    private SideScrollerController controller;
    private CharacterController characterController;
    private GameObject spawned;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        controller = player.GetComponent<SideScrollerController>();
        inputs = player.GetComponent<StarterAssetsInputs>();
        characterController = player.GetComponent<CharacterController>();

        characterController.enabled = false;
        player.transform.position = new Vector3(-3f, 0.1f, 0f);
        characterController.enabled = true;
        yield return new WaitForSeconds(0.6f);
    }

    [TearDown]
    public void Cleanup()
    {
        inputs.move = Vector2.zero;
        if (spawned != null) Object.Destroy(spawned);
    }

    private GameObject Spawn(string relativePath, Vector3 position)
    {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + relativePath + ".prefab");
        Assert.IsNotNull(prefab, "Missing prefab " + relativePath);
        var instance = Object.Instantiate(prefab, position, prefab.transform.rotation);
        spawned = instance;
        return instance;
#else
        Assert.Ignore("Prefab loading by path needs the Editor.");
        return null;
#endif
    }

    [UnityTest]
    public IEnumerator Scenery_DoesNotBlockThePlayer()
    {
        var root = new GameObject("SceneryRoot");
        spawned = root;
        // Placed right in the lane (z = 0), in the way of the player.
        foreach (var (path, x) in new[] { ("Scenery/Scenery_Tree_01", -2.2f), ("Scenery/Scenery_Bush_01", -1.6f), ("Scenery/Scenery_Rock_04", -1.0f) })
        {
            var item = Spawn(path, new Vector3(x, 0f, 0f));
            item.transform.SetParent(root.transform);
        }
        spawned = root;

        inputs.move = Vector2.right;
        yield return new WaitForSeconds(1f);

        Assert.Greater(player.transform.position.x, 0f, "Scenery has no colliders, so the player must run straight through it.");
    }

    [UnityTest]
    public IEnumerator SolidObstacle_BlocksTheWalkingPlayer()
    {
        Spawn("Obstacles/Obstacle_Rock_04", new Vector3(-1f, 0f, 0f));

        inputs.move = Vector2.right;
        yield return new WaitForSeconds(1.5f);

        Assert.Less(player.transform.position.x, -1f, "A solid obstacle must stop a player who does not jump.");
    }

    [UnityTest]
    public IEnumerator EveryObstacle_CanBeJumpedOver(
        [ValueSource(nameof(ObstaclePaths))] string path)
    {
        var obstacle = Spawn(path, new Vector3(0f, 0f, 0f));
        var bounds = obstacle.GetComponentInChildren<Collider>().bounds;
        float radius = characterController.radius;

        float end = Time.time + 4f;
        while (Time.time < end)
        {
            inputs.move = Vector2.right;
            // Take off about 1 m before the obstacle's near edge, like a player would.
            float gap = bounds.min.x - (player.transform.position.x + radius);
            if (controller.Grounded && gap < 1f && player.transform.position.x < bounds.max.x)
                inputs.jump = true;
            yield return null;
        }

        Assert.Greater(player.transform.position.x, bounds.max.x + radius, path + " should be jumpable.");
    }

    private static readonly string[] ObstaclePaths =
    {
        "Obstacles/Obstacle_Rock_01",
        "Obstacles/Obstacle_Rock_04",
        "Obstacles/Obstacle_Rock_05",
        "Obstacles/Obstacle_Stump_01",
        "Obstacles/Obstacle_Log_01",
    };
}
