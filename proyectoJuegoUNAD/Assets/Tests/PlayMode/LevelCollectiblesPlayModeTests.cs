using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Checks the waste placed under the <c>Collectibles</c> root of Level01: enough to reach the target, more of it
/// in each zone (GDD §8.2), all on the lane and clear of hazards, the elevated ones reachable with a jump,
/// and collecting all of it wins the level.
/// </summary>
public class LevelCollectiblesPlayModeTests
{
    private const string LevelScene = "Level01";

    // Must match LevelEnvironmentBuilder: three contiguous 60 m zones.
    private static readonly float[] ZoneStarts = { -12f, 48f, 108f, 168f };
    private const float LaneHalfDepth = 1.5f;

    private GameObject player;
    private CharacterController characterController;
    private SideScrollerController controller;
    private List<Collectible> items;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        characterController = player.GetComponent<CharacterController>();
        controller = player.GetComponent<SideScrollerController>();
        items = new List<Collectible>(Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None));
        items.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
    }

    private void Teleport(Vector3 position)
    {
        characterController.enabled = false;
        player.transform.position = position;
        characterController.enabled = true;
    }

    private static Bounds TriggerBounds(Collectible c) => c.GetComponent<Collider>().bounds;

    [Test]
    public void Level_HasEnoughWasteToWin()
    {
        Assert.GreaterOrEqual(items.Count, GameManager.Instance.Config.targetWaste,
            "Level01 must hold at least the target amount of waste.");
    }

    [Test]
    public void EveryZone_HasWasteAndItsCountDoesNotShrink()
    {
        int previous = 0;
        for (int zone = 0; zone < 3; zone++)
        {
            int count = 0;
            foreach (var c in items)
            {
                float x = c.transform.position.x;
                if (x >= ZoneStarts[zone] && x < ZoneStarts[zone + 1]) count++;
            }
            Assert.GreaterOrEqual(count, 3, "Zone " + (zone + 1) + " needs at least 3 waste items.");
            Assert.GreaterOrEqual(count, previous, "Zone " + (zone + 1) + " must not hold less waste than the one before.");
            previous = count;
        }
    }

    [Test]
    public void Waste_IsOnTheLaneAndClearOfHazards()
    {
        var hazards = new List<Collider>();
        int hazardLayer = LayerMask.NameToLayer("Hazard");
        foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (col.gameObject.layer == hazardLayer) hazards.Add(col);

        foreach (var c in items)
        {
            var p = c.transform.position;
            Assert.That(p.x, Is.InRange(ZoneStarts[0], ZoneStarts[3]), c.name + " is off the course");
            Assert.That(p.z, Is.InRange(-LaneHalfDepth, LaneHalfDepth), c.name + " is off the lane");
            Assert.GreaterOrEqual(p.y, 0f, c.name + " is below the lane");

            var b = TriggerBounds(c);
            foreach (var h in hazards)
                Assert.IsFalse(b.Intersects(h.bounds), c.name + " overlaps the hazard " + h.name);
        }
    }

    [Test]
    public void ElevatedWaste_IsReachableWithAJump()
    {
        float reach = characterController.height + controller.JumpHeight;
        foreach (var c in items)
            Assert.Less(TriggerBounds(c).min.y, reach, c.name + " is out of jumping reach");
    }

    [UnityTest]
    public IEnumerator CollectingAllPlacedWaste_WinsTheLevel()
    {
        var session = GameManager.Instance.Session;
        foreach (var c in items)
        {
            if (session.State != GameState.Playing) break;
            Teleport(c.transform.position);
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(c.IsCollected, c.name + " was not collected when the player stood on it.");
        }

        Assert.AreEqual(GameState.Won, session.State, "Collecting the placed waste must win the level.");
        Assert.AreEqual(session.TargetWaste, session.CollectedWaste);
    }
}
