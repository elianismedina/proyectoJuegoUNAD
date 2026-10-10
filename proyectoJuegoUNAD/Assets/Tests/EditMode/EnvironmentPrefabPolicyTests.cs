using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Guards the collider policy for the environment prefabs (see Docs/NaturePackIntegrationPlan.md, phase B):
// scenery and terrain never collide, obstacles are solid on the Ground layer and stay jumpable.
public class EnvironmentPrefabPolicyTests
{
    const string Root = "Assets/Prefabs/Environment";

    // The player jumps 1.7 m; obstacles must leave a clear margin below that.
    const float MaxObstacleHeight = 1.2f;

    static GameObject[] Load(string folder)
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/" + folder });
        var result = new GameObject[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
        return result;
    }

    static float TopAboveGround(GameObject prefab)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            instance.transform.position = Vector3.zero;
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds.max.y;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void Scenery_HasNoColliders()
    {
        var prefabs = Load("Scenery");
        Assert.IsNotEmpty(prefabs);
        foreach (var p in prefabs)
            Assert.IsEmpty(p.GetComponentsInChildren<Collider>(true), p.name + " must not have colliders");
    }

    [Test]
    public void Terrain_HasNoColliders()
    {
        var prefabs = Load("Terrain");
        Assert.IsNotEmpty(prefabs);
        foreach (var p in prefabs)
            Assert.IsEmpty(p.GetComponentsInChildren<Collider>(true), p.name + " must not have colliders");
    }

    [Test]
    public void Obstacles_AreSolidOnGroundLayer()
    {
        var prefabs = Load("Obstacles");
        Assert.IsNotEmpty(prefabs);
        int ground = LayerMask.NameToLayer("Ground");
        foreach (var p in prefabs)
        {
            var colliders = p.GetComponentsInChildren<Collider>(true);
            Assert.IsNotEmpty(colliders, p.name + " needs a collider");
            foreach (var c in colliders)
            {
                Assert.IsFalse(c.isTrigger, p.name + " collider must be solid");
                Assert.AreEqual(ground, c.gameObject.layer, p.name + " must be on the Ground layer");
            }
        }
    }

    [Test]
    public void Obstacles_AreJumpable()
    {
        foreach (var p in Load("Obstacles"))
            Assert.LessOrEqual(TopAboveGround(p), MaxObstacleHeight, p.name + " is too tall to jump over");
    }

    [Test]
    public void Obstacles_UseTheDarkMaterialNotTheSceneryOne()
    {
        // "Dark means avoid": obstacles must never share the scenery rocks' and stumps' look (GDD 8.6).
        foreach (var p in Load("Obstacles"))
            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    Assert.AreEqual("Obstacle_Atlas", m.name, p.name + " must use Obstacle_Atlas");
    }

    [Test]
    public void Hazards_AreTriggersOnTheHazardLayer()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Hazards" });
        Assert.IsNotEmpty(guids);
        int hazard = LayerMask.NameToLayer("Hazard");
        foreach (var guid in guids)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var colliders = p.GetComponentsInChildren<Collider>(true);
            Assert.IsNotEmpty(colliders, p.name + " needs a trigger collider");
            Assert.IsTrue(System.Array.Exists(colliders, c => c.isTrigger), p.name + " needs a trigger collider");

            // Only the rolling log's own solid body may block: the log is solid, mud and the rock's warning area are not.
            var log = p.GetComponent<RollingLog>();
            foreach (var c in colliders)
            {
                bool logBody = log != null && c.transform.parent == p.transform && c.name == "Body";
                if (!logBody)
                    Assert.IsTrue(c.isTrigger, p.name + "/" + c.name + " must be a trigger (hazards stumble or slow, they do not block)");
                Assert.AreEqual(hazard, c.gameObject.layer, p.name + "/" + c.name + " must be on the Hazard layer");
            }
        }
    }

    [Test]
    public void RollingLog_HasASolidBodyInsideItsStumbleTrigger()
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/RollingLog_Hazard.prefab");
        Assert.IsNotNull(p);
        var body = p.transform.Find("Body")?.GetComponent<CapsuleCollider>();
        Assert.IsNotNull(body, "RollingLog_Hazard needs a solid capsule child named Body.");
        Assert.IsFalse(body.isTrigger, "The log body must be solid so the player cannot walk through it.");

        var trigger = p.GetComponent<CapsuleCollider>();
        Assert.IsNotNull(trigger, "RollingLog_Hazard needs a capsule trigger on its root.");
        Assert.IsTrue(trigger.isTrigger);
        Assert.AreEqual(trigger.direction, body.direction, "Trigger and body must lie along the same axis.");
        Assert.Greater(trigger.radius, body.radius, "The trigger must reach past the body, or the player is blocked before stumbling.");
        Assert.Greater(trigger.height, body.height, "The trigger must reach past both ends of the body.");

        // The body must cover the whole visible log, not just its middle.
        var renderer = p.GetComponentInChildren<MeshRenderer>();
        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        Vector3 size = renderer.transform.TransformVector(mesh.bounds.size);
        float logLength = Mathf.Abs(size.z);
        Assert.AreEqual(2, body.direction, "The log lies along its local Z.");
        Assert.GreaterOrEqual(body.height, logLength - 0.1f, "The body must cover the length of the log (" + logLength + " m).");
    }
}
