using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The forest's recovery in Level01: every zone starts polluted; collecting all of a zone's waste recovers that zone
/// (its scenery goes back to the healthy material) and fills Jhon's "Recuperación del bosque" bar; winning recovers all.
/// </summary>
public class ForestRecoveryPlayModeTests
{
    private const string LevelScene = "Level01";

    private ForestRecovery forest;
    private ForestTrail trail;
    private WasteCounter counter;
    private GameObject player;
    private CharacterController characterController;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        forest = Object.FindFirstObjectByType<ForestRecovery>();
        trail = Object.FindFirstObjectByType<ForestTrail>();
        counter = Object.FindFirstObjectByType<WasteCounter>();
        player = GameObject.FindGameObjectWithTag("Player");
        characterController = player.GetComponent<CharacterController>();
        Assert.IsNotNull(forest, "Level01 needs a ForestRecovery (Forest Guardian > Level > Build Level01 Environment).");
        Assert.IsNotNull(forest.Tracker, "The forest must be set up once the level has started.");
    }

    private List<Collectible> WasteInZone(int zone)
    {
        var items = new List<Collectible>();
        foreach (var c in Object.FindObjectsByType<Collectible>())
            if (trail.ZoneAt(trail.Project(c.transform.position, out _)) == zone) items.Add(c);
        return items;
    }

    [Test]
    public void EveryZone_HasWasteAndSceneryAndStartsPolluted()
    {
        Assert.IsNotNull(forest.HealthyMaterial);
        Assert.IsNotNull(forest.PollutedMaterial);
        Assert.AreNotEqual(forest.HealthyMaterial, forest.PollutedMaterial);
        for (int zone = 0; zone < ForestTrail.ZoneCount; zone++)
        {
            Assert.Greater(forest.Tracker.WasteInZone(zone), 0, "Zone " + (zone + 1) + " needs waste to recover.");
            Assert.Greater(forest.RenderersInZone(zone).Count, 0, "Zone " + (zone + 1) + " needs scenery that changes.");
            Assert.IsTrue(forest.LooksPolluted(zone), "Zone " + (zone + 1) + " must start polluted.");
            Assert.IsFalse(forest.Tracker.IsRecovered(zone));
        }
        Assert.AreEqual(0f, counter.BarFill, 0.001f, "The recovery bar starts empty.");
    }

    [UnityTest]
    public IEnumerator CollectingAllOfAZonesWaste_RecoversThatZoneOnlyAndFillsTheBar()
    {
        var recovered = new List<int>();
        System.Action<int> record = recovered.Add;
        ForestRecovery.ZoneRecovered += record;
        try
        {
            var zone1 = WasteInZone(0);
            Assert.AreEqual(forest.Tracker.WasteInZone(0), zone1.Count);
            for (int i = 0; i < zone1.Count; i++)
            {
                characterController.enabled = false;
                player.transform.position = zone1[i].transform.position;
                characterController.enabled = true;
                yield return new WaitForSeconds(0.15f);
                Assert.IsTrue(zone1[i].IsCollected);
                if (i < zone1.Count - 1) Assert.IsTrue(forest.LooksPolluted(0), "Zone 1 recovers only with its last item.");
            }

            Assert.IsTrue(forest.Tracker.IsRecovered(0));
            Assert.IsFalse(forest.LooksPolluted(0), "Zone 1's scenery must be healthy again.");
            Assert.IsTrue(forest.LooksPolluted(1) && forest.LooksPolluted(2), "The other zones stay polluted.");
            CollectionAssert.AreEqual(new[] { 0 }, recovered);
            Assert.AreEqual(forest.Progress, counter.BarFill, 0.001f, "Jhon's bar shows the forest's recovery.");
            Assert.AreEqual(1f / 3f, counter.BarFill, 0.001f, "One zone of three recovered.");
        }
        finally
        {
            ForestRecovery.ZoneRecovered -= record;
        }
    }

    [UnityTest]
    public IEnumerator Winning_RecoversTheWholeForest()
    {
        var session = GameManager.Instance.Session;
        for (int i = 0; i < session.TargetWaste; i++) session.AddWaste();
        GameManager.Instance.ReachGoal();
        yield return null;

        Assert.AreEqual(GameState.Won, session.State);
        for (int zone = 0; zone < ForestTrail.ZoneCount; zone++)
            Assert.IsFalse(forest.LooksPolluted(zone), "¡Bosque limpio! Zone " + (zone + 1) + " must be healthy.");
        Assert.AreEqual(1f, counter.BarFill, 0.001f, "The bar is full when the forest is clean.");
    }
}
