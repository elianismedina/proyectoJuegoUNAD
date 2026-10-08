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
}
