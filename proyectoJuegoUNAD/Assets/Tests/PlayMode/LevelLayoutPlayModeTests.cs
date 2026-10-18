using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Structural checks on the Level01 forest trail built by <c>LevelEnvironmentBuilder</c>
/// (Docs/ThirdPersonConversionPlan.md, phase 2): ground along the whole trail except the streams, jumpable banks
/// and streams, a closed play area, colliders only where they belong, a checkpoint at the start of every zone, and
/// falling into a stream putting the player back at that checkpoint with their waste.
/// </summary>
public class LevelLayoutPlayModeTests
{
    private const string LevelScene = "Level01";

    // Jump limits for a 10–14 year old audience: banks no higher than the obstacles, streams well inside a running jump.
    private const float MaxStepHeight = 1.2f;
    private const float MaxStreamWidth = 2.6f;

    private ForestTrail trail;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        trail = Object.FindFirstObjectByType<ForestTrail>();
        Assert.IsNotNull(trail, "Level01 needs the forest trail (run Forest Guardian > Level > Build Level01 Environment).");
    }

    private static bool GroundHeight(Vector3 point, out float height)
    {
        height = 0f;
        if (!Physics.Raycast(point + Vector3.up * 10f, Vector3.down, out var hit, 20f, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
            return false;
        height = hit.point.y;
        return true;
    }

    private const float StreamEdgeMargin = 0.15f;

    private bool NearStreamEdge(float s)
    {
        foreach (var span in trail.StreamSpans)
            if (Mathf.Abs(s - span.x) < StreamEdgeMargin || Mathf.Abs(s - span.y) < StreamEdgeMargin) return true;
        return false;
    }

    [Test]
    public void Trail_HasGroundEverywhereExceptTheStreams()
    {
        for (float s = 0.5f; s < trail.Length; s += 0.5f)
        {
            // The stream's bank edge sits exactly on the span's ends; a ray there can graze it either way.
            if (NearStreamEdge(s)) continue;

            bool hasGround = GroundHeight(trail.PointAt(s), out float height);
            if (trail.IsOverStream(s))
            {
                Assert.IsFalse(hasGround, "Stream at s=" + s + " must be open: falling in is the risk.");
                continue;
            }

            Assert.IsTrue(hasGround, "No ground on the trail at s=" + s);
            Assert.That(height, Is.InRange(-0.01f, MaxStepHeight + 0.01f), "Trail height out of range at s=" + s);
        }
    }

    [Test]
    public void Steps_AreJumpableAndStreams_AreNarrowAndNotInZone1()
    {
        float previous = 0f;
        for (float s = 0.5f; s < trail.Length; s += 0.25f)
        {
            if (trail.IsOverStream(s) || !GroundHeight(trail.PointAt(s), out float height)) continue;
            Assert.LessOrEqual(height - previous, MaxStepHeight + 0.01f, "Step up at s=" + s + " is too high to jump.");
            previous = height;
        }

        Assert.IsNotEmpty(trail.StreamSpans, "The trail needs streams to make falling a real risk.");
        foreach (var span in trail.StreamSpans)
        {
            Assert.LessOrEqual(span.y - span.x, MaxStreamWidth, "Stream at s=" + span.x + " is too wide to jump.");
            Assert.AreNotEqual(0, trail.ZoneAt(span.x), "Zone 1 teaches the controls and must not have streams.");
        }
    }

    [Test]
    public void PlayArea_IsClosedOnBothSides()
    {
        for (float s = 2f; s < trail.Length - 2f; s += 7f)
        {
            if (trail.IsOverStream(s)) continue;
            // 2.5 m up: above every bank (≤ 1.2 m) and obstacle, below the top of the walls.
            Vector3 origin = trail.PointAt(s) + Vector3.up * 2.5f;
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 direction = trail.LeftAt(s) * side;
                Assert.IsTrue(Physics.Raycast(origin, direction, 30f, ~0, QueryTriggerInteraction.Ignore),
                    "Nothing stops the player leaving the trail at s=" + s + " on the " + (side > 0 ? "left" : "right") + ".");
            }
        }
    }

    [Test]
    public void Environment_HasCollidersOnlyOnGroundWallsObstaclesAndTrunks()
    {
        var environment = GameObject.Find("Environment");
        Assert.IsNotNull(environment);

        foreach (var collider in environment.GetComponentsInChildren<Collider>(true))
        {
            bool allowed = false;
            for (var t = collider.transform; t != null && t != environment.transform; t = t.parent)
            {
                if (t.name == "ForestFloor" || t.name == "PlayAreaBounds" || t.name == "Obstacles" || t.name == "Ledges" || t.name == "Trunk")
                {
                    allowed = true;
                    break;
                }
            }
            Assert.IsTrue(allowed, "Unexpected collider on " + collider.transform.name + " (scenery and far terrain must not collide).");
        }
    }

    [Test]
    public void PlayerSpawn_IsOnTheTrailNearTheStartFacingAlongIt()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        Assert.IsNotNull(player);

        float s = trail.Project(player.transform.position, out float lateral);
        Assert.Less(s, 10f, "Player should start near the beginning of Zone 1.");
        Assert.Less(Mathf.Abs(lateral), 1f, "Player should start on the path.");
        Assert.Greater(Vector3.Dot(player.transform.forward, trail.TangentAt(s)), 0.9f, "Player should face down the trail.");
    }

    [Test]
    public void EveryZone_HasObstaclesAndTheirCountGrows()
    {
        var environment = GameObject.Find("Environment");
        int previous = 0;
        foreach (var zone in new[] { "Zone1_Learning", "Zone2_Development", "Zone3_Challenge" })
        {
            var obstacles = environment.transform.Find(zone + "/Obstacles");
            Assert.IsNotNull(obstacles, zone + " needs an Obstacles group.");
            Assert.Greater(obstacles.childCount, previous, "Difficulty should rise: " + zone + " needs more obstacles than the previous zone.");
            previous = obstacles.childCount;
        }
    }

    [Test]
    public void EveryZone_HasACheckpointOnClearGroundAtItsStart()
    {
        var checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        Assert.AreEqual(ForestTrail.ZoneCount, checkpoints.Length, "One checkpoint per zone (Forest Guardian > Level > Build Level01 Environment).");
        for (int zone = 0; zone < ForestTrail.ZoneCount; zone++)
        {
            var c = Checkpoint.ForZone(zone);
            Assert.AreEqual(zone, c.Zone, "Zone " + (zone + 1) + " has no checkpoint.");
            Vector3 p = c.transform.position;
            float s = trail.Project(p, out float lateral);
            Assert.AreEqual(zone, trail.ZoneAt(s), c.name + " must lie in its own zone.");
            Assert.Less(s - zone * trail.Length / ForestTrail.ZoneCount, 10f, c.name + " must be at the start of its zone.");
            Assert.LessOrEqual(Mathf.Abs(lateral), 2f, c.name + " must be on the path.");
            Assert.IsFalse(trail.IsOverStream(s), c.name + " must not be over a stream.");
            Assert.IsTrue(GroundHeight(p, out float ground) && Mathf.Abs(p.y - ground) < 0.2f, c.name + " must stand on the ground.");
            // Room for the player's capsule (1.6 m tall, 0.3 m radius): no obstacle, trunk or wall in the way.
            // (Zone 1's checkpoint is the spawn point, where the player is standing, so the player is ignored.)
            foreach (var hit in Physics.OverlapCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.3f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                Assert.IsTrue(hit.CompareTag("Player"), c.name + " is blocked by " + hit.name + ": the player would reappear inside it.");
            Assert.Greater(Vector3.Dot(c.transform.forward, trail.TangentAt(s)), 0.9f, c.name + " must face down the trail.");
        }
    }

    // GDD 5.2: falling into a stream puts the player back at the start of the zone with the waste already collected.
    [UnityTest]
    public IEnumerator FallingIntoAStream_PutsThePlayerBackAtTheStartOfTheZoneWithTheirWaste()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        var characterController = player.GetComponent<CharacterController>();
        var session = GameManager.Instance.Session;
        var killPlane = Object.FindFirstObjectByType<KillPlane>();
        Assert.IsNotNull(killPlane, "Level01 needs a KillPlane.");
        session.AddWaste();
        session.AddWaste();
        session.AddWaste();

        var span = trail.StreamSpans[0];
        int zone = trail.ZoneAt(span.x);
        Assert.AreEqual(1, zone, "The first stream is in Zone 2.");

        characterController.enabled = false;
        player.transform.position = trail.PointAt((span.x + span.y) / 2f) + Vector3.up * 0.2f;
        characterController.enabled = true;
        yield return new WaitForSeconds(1.5f);

        Assert.AreEqual(GameState.Playing, session.State, "Falling into a stream no longer loses the level.");
        Assert.AreEqual(3, session.CollectedWaste, "The waste already collected must be kept.");
        Assert.AreEqual(1, killPlane.Respawns);
        float s = trail.Project(player.transform.position, out _);
        Assert.AreEqual(zone, trail.ZoneAt(s), "The player must reappear in the zone they fell in, not back at the level start.");
        Vector3 offset = player.transform.position - Checkpoint.ForZone(zone).transform.position;
        Assert.Less(new Vector2(offset.x, offset.z).magnitude, 0.5f, "The player must reappear at the Zone 2 checkpoint.");
        Assert.Less(s, span.x, "The checkpoint is before the stream.");
    }
}
