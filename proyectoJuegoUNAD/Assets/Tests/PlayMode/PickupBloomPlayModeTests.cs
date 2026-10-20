using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Flowers sprouting where waste is picked up in Level01: a few grow in with a short animation, beside the path for an
/// item above it and on the bank for one over a stream, never on the path, never solid.
/// </summary>
public class PickupBloomPlayModeTests
{
    private const string LevelScene = "Level01";

    private PickupBloom bloom;
    private ForestTrail trail;
    private GameObject player;
    private CharacterController characterController;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        bloom = Object.FindFirstObjectByType<PickupBloom>();
        trail = Object.FindFirstObjectByType<ForestTrail>();
        player = GameObject.FindGameObjectWithTag("Player");
        characterController = player.GetComponent<CharacterController>();
        Assert.IsNotNull(bloom, "Level01 needs a PickupBloom (Forest Guardian > Level > Build Level01 Environment).");
    }

    private Collectible FindWaste(System.Func<float, float, bool> where)
    {
        foreach (var c in Object.FindObjectsByType<Collectible>())
        {
            float s = trail.Project(c.transform.position, out float lateral);
            if (where(s, lateral)) return c;
        }
        return null;
    }

    private void Teleport(Vector3 position)
    {
        characterController.enabled = false;
        player.transform.position = position;
        characterController.enabled = true;
    }

    private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

    [UnityTest]
    public IEnumerator PickingUpWasteAboveThePath_SproutsFlowersOnBothEdgesWithAGrowAnimation()
    {
        var item = FindWaste((s, lateral) => Mathf.Abs(lateral) < 1f && !trail.IsOverStream(s));
        Assert.IsNotNull(item, "Level01 has waste above the path.");
        Vector3 at = item.transform.position;

        Teleport(at);
        yield return new WaitForSeconds(0.1f);
        Assert.IsTrue(item.IsCollected);
        Assert.GreaterOrEqual(bloom.Spawned.Count, 3, "Picking up waste must sprout some flowers.");
        Assert.IsTrue(bloom.IsGrowing, "The flowers grow in, they do not just appear.");

        yield return new WaitForSeconds(1.2f);
        Assert.IsFalse(bloom.IsGrowing, "The grow animation is short.");

        bool left = false, right = false;
        foreach (var flower in bloom.Spawned)
        {
            float s = trail.Project(flower.position, out float lateral);
            Assert.GreaterOrEqual(Mathf.Abs(lateral), PickupBloom.PathClearance - 0.01f, flower.name + " stands on the path.");
            Assert.Less(Vector2.Distance(Flat(flower.position), Flat(at)), 4.5f, flower.name + " sprouted far from the waste.");
            Assert.IsEmpty(flower.GetComponentsInChildren<Collider>(true), flower.name + " must not block anything.");
            Assert.Greater(flower.localScale.x, 0.1f, flower.name + " did not grow.");
            if (lateral > 0f) left = true; else right = true;
        }
        Assert.IsTrue(left && right, "For waste above the path, flowers sprout on both edges and leave the path clear.");
    }

    [UnityTest]
    public IEnumerator PickingUpWasteOverAStream_SproutsFlowersOnTheBank()
    {
        var item = FindWaste((s, lateral) => trail.IsOverStream(s));
        Assert.IsNotNull(item, "Level01 has waste over a stream.");

        Teleport(item.transform.position);
        yield return new WaitForSeconds(1.3f);

        Assert.IsTrue(item.IsCollected);
        Assert.GreaterOrEqual(bloom.Spawned.Count, 3);
        foreach (var flower in bloom.Spawned)
        {
            float s = trail.Project(flower.position, out _);
            Assert.IsFalse(trail.IsOverStream(s), flower.name + " sprouted over the water.");
            Assert.Greater(flower.position.y, -0.2f, flower.name + " sank into the stream bed.");
        }
    }
}
