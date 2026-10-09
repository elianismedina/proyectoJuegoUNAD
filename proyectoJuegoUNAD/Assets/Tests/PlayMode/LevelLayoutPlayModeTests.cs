using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Structural checks on the Level01 forest trail built by <c>LevelEnvironmentBuilder</c>
/// (Docs/ThirdPersonConversionPlan.md, phase 2): ground along the whole trail except the streams, jumpable banks
/// and streams, a closed play area, colliders only where they belong, and falling into a stream loses.
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

    [Test]
    public void Trail_HasGroundEverywhereExceptTheStreams()
    {
        for (float s = 0.5f; s < trail.Length; s += 0.5f)
        {
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

    [UnityTest]
    public IEnumerator FallingIntoAStream_LosesTheLevel()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        var characterController = player.GetComponent<CharacterController>();
        var span = trail.StreamSpans[0];

        characterController.enabled = false;
        player.transform.position = trail.PointAt((span.x + span.y) / 2f) + Vector3.up * 0.2f;
        characterController.enabled = true;

        yield return new WaitForSeconds(1.5f);
        Assert.AreEqual(GameState.Lost, GameManager.Instance.Session.State, "Falling into a stream must lose the level.");
    }
}
