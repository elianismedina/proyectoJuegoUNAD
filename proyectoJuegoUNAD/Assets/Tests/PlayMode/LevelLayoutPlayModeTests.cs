using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Structural checks on the built Level01 environment (see Docs/NaturePackIntegrationPlan.md, phase C):
/// a continuous lane, colliders only where they belong, and a camera confiner that covers the whole course.
/// </summary>
public class LevelLayoutPlayModeTests
{
    private const string LevelScene = "Level01";

    // Must match LevelEnvironmentBuilder.
    private const float CourseStart = -12f;
    private const float CourseEnd = 168f;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;
    }

    [Test]
    public void Lane_IsContinuousAcrossTheWholeCourse()
    {
        for (float x = CourseStart + 0.5f; x < CourseEnd; x += 1f)
        {
            bool hasLane = false;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 10f, 0f), Vector3.down, 20f))
            {
                if (!hit.collider.name.StartsWith("Lane_")) continue;
                hasLane = true;
                Assert.AreEqual(0f, hit.point.y, 0.01f, "Lane surface must be flat at x=" + x);
            }
            Assert.IsTrue(hasLane, "No lane under x=" + x);
        }
    }

    [Test]
    public void Environment_HasCollidersOnlyOnLaneBarriersAndObstacles()
    {
        var environment = GameObject.Find("Environment");
        Assert.IsNotNull(environment, "Level01 needs the Environment root (run Forest Guardian > Level > Build Level01 Environment).");

        foreach (var collider in environment.GetComponentsInChildren<Collider>(true))
        {
            bool allowed = false;
            for (var t = collider.transform; t != null && t != environment.transform; t = t.parent)
            {
                if (t.name == "Lane" || t.name == "Obstacles") { allowed = true; break; }
            }
            Assert.IsTrue(allowed, "Unexpected collider on " + collider.transform.name + " (scenery and terrain must not collide).");
        }
    }

    [Test]
    public void PlayerSpawn_IsOnTheLaneNearTheStart()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        Assert.IsNotNull(player);
        Assert.Greater(player.transform.position.x, CourseStart);
        Assert.Less(player.transform.position.x, CourseStart + 10f, "Player should start near the beginning of Zone 1.");
        Assert.AreEqual(0f, player.transform.position.z, 0.01f);
    }

    [Test]
    public void CameraBounds_CoverTheWholeCourse()
    {
        var bounds = GameObject.Find("CameraBounds");
        Assert.IsNotNull(bounds);
        var box = bounds.GetComponent<BoxCollider>().bounds;
        Assert.LessOrEqual(box.min.x, CourseStart - 2f + 0.01f, "Camera confiner must start 2 m before the course.");
        Assert.GreaterOrEqual(box.max.x, CourseEnd + 2f - 0.01f, "Camera confiner must end 2 m after the course.");
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
}
