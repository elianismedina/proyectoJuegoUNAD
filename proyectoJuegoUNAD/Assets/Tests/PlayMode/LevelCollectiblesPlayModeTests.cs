using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Checks the waste placed under the <c>Collectibles</c> root of Level01: enough to reach the target, more of it
/// in each zone (GDD §8.2), all inside the play area and reachable with a jump, few of them lying on the path at foot
/// height (the rest need a detour, a jump or a risk), and collecting all of it, then reaching the goal, wins the level.
/// </summary>
public class LevelCollectiblesPlayModeTests
{
    private const string LevelScene = "Level01";

    // Waste lying on the path at foot height is "free": only Zone 1, which teaches collecting, may have it.
    private const int MaxFreeWaste = 2;
    private const float PathHalfWidth = 2f;
    private const float HazardReach = 4f; // Waste this close to a hazard is a risk, not a freebie.

    private GameObject player;
    private CharacterController characterController;
    private PlayerController controller;
    private ForestTrail trail;
    private List<Collectible> items;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        player = GameObject.FindGameObjectWithTag("Player");
        characterController = player.GetComponent<CharacterController>();
        controller = player.GetComponent<PlayerController>();
        trail = Object.FindFirstObjectByType<ForestTrail>();
        Assert.IsNotNull(trail, "Level01 needs the forest trail.");
        items = new List<Collectible>(Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None));
        items.Sort((a, b) => trail.Project(a.transform.position, out _).CompareTo(trail.Project(b.transform.position, out _)));
    }

    private void Teleport(Vector3 position)
    {
        characterController.enabled = false;
        player.transform.position = position;
        characterController.enabled = true;
    }

    private static Bounds TriggerBounds(Collectible c) => c.GetComponent<Collider>().bounds;

    // Height of whatever the player could stand on under the item, or 0 (the trail level) over a stream.
    private static bool SurfaceBelow(Collectible c, out float height)
    {
        height = 0f;
        var origin = c.transform.position + Vector3.up * 0.2f;
        if (!Physics.Raycast(origin, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore)) return false;
        height = hit.point.y;
        return true;
    }

    private static bool NearAHazard(Vector3 position)
    {
        foreach (var hazard in Object.FindObjectsByType<RollingLog>(FindObjectsSortMode.None))
            if (Vector3.Distance(hazard.transform.position, position) <= HazardReach) return true;
        foreach (var hazard in Object.FindObjectsByType<FallingRock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Vector3.Distance(hazard.transform.position, position) <= HazardReach) return true;
        return false;
    }

    [Test]
    public void Level_HasEnoughWasteToWin()
    {
        Assert.GreaterOrEqual(items.Count, GameManager.Instance.Config.targetWaste,
            "Level01 must hold at least the target amount of waste.");
    }

    [Test]
    public void EveryZone_HasWasteAndItsCountDoesNotShrink()
    {
        var counts = new int[ForestTrail.ZoneCount];
        foreach (var c in items) counts[trail.ZoneAt(trail.Project(c.transform.position, out _))]++;

        int previous = 0;
        for (int zone = 0; zone < counts.Length; zone++)
        {
            Assert.GreaterOrEqual(counts[zone], 3, "Zone " + (zone + 1) + " needs at least 3 waste items.");
            Assert.GreaterOrEqual(counts[zone], previous, "Zone " + (zone + 1) + " must not hold less waste than the one before.");
            previous = counts[zone];
        }
    }

    [Test]
    public void Waste_IsInsideThePlayAreaAndClearOfStaticHazards()
    {
        var mud = new List<Collider>();
        foreach (var zone in Object.FindObjectsByType<MudZone>(FindObjectsSortMode.None)) mud.Add(zone.GetComponent<Collider>());

        foreach (var c in items)
        {
            float s = trail.Project(c.transform.position, out float lateral);
            Assert.That(s, Is.InRange(0.5f, trail.Length - 0.5f), c.name + " is off the course");

            // Nothing solid may stand between the path and the item at chest height: the bounds walls would.
            Vector3 onPath = trail.PointAt(s) + Vector3.up * 1.5f;
            Vector3 target = new Vector3(c.transform.position.x, onPath.y, c.transform.position.z);
            Vector3 toItem = target - onPath;
            if (toItem.magnitude > 0.1f)
            {
                foreach (var hit in Physics.RaycastAll(onPath, toItem.normalized, toItem.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    Assert.AreNotEqual("PlayAreaBounds", hit.collider.name, c.name + " is outside the play area.");
            }

            var b = TriggerBounds(c);
            foreach (var m in mud) Assert.IsFalse(b.Intersects(m.bounds), c.name + " sits in the mud.");
        }
    }

    [Test]
    public void Waste_IsReachableWithAJumpFromTheSurfaceBelow()
    {
        float reach = characterController.height + controller.JumpHeight;
        foreach (var c in items)
        {
            SurfaceBelow(c, out float surface);
            Assert.Less(TriggerBounds(c).min.y - surface, reach, c.name + " is out of jumping reach");
        }
    }

    [Test]
    public void FewWaste_LieFreeOnThePath()
    {
        int free = 0;
        foreach (var c in items)
        {
            float s = trail.Project(c.transform.position, out float lateral);
            bool onPath = Mathf.Abs(lateral) <= PathHalfWidth;
            bool atFootHeight = SurfaceBelow(c, out float surface) && TriggerBounds(c).min.y - surface < characterController.height;
            if (!onPath || !atFootHeight || NearAHazard(c.transform.position)) continue;

            free++;
            Assert.AreEqual(0, trail.ZoneAt(s), c.name + " lies free on the path outside Zone 1; move it off the path, up high or next to a hazard.");
        }
        Assert.LessOrEqual(free, MaxFreeWaste, "Too much waste lies free on the path.");
    }

    [UnityTest]
    public IEnumerator CollectingAllPlacedWasteThenReachingTheGoal_WinsTheLevel()
    {
        var session = GameManager.Instance.Session;
        foreach (var c in items)
        {
            Teleport(c.transform.position);
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(c.IsCollected, c.name + " was not collected when the player stood on it.");
        }

        Assert.AreEqual(items.Count, session.CollectedWaste);
        Assert.AreEqual(GameState.Playing, session.State, "Collecting alone must not win; the goal is still ahead.");

        var goal = Object.FindFirstObjectByType<LevelGoal>();
        Assert.IsNotNull(goal, "Level01 needs a LevelGoal.");
        Teleport(goal.transform.position + Vector3.up * 0.1f);
        yield return new WaitForSeconds(0.2f);

        Assert.AreEqual(GameState.Won, session.State, "Reaching the goal with the waste collected must win the level.");
    }
}
