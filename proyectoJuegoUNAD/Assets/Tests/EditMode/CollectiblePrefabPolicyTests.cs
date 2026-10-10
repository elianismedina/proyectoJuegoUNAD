using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Guards the waste prefabs in Assets/Prefabs/Collectibles: one base prefab and one variant per waste type,
// all with an enlarged trigger on the Collectible layer and a glowing rim (Docs/NaturePackIntegrationPlan.md, waste palette rule).
public class CollectiblePrefabPolicyTests
{
    const string Root = "Assets/Prefabs/Collectibles";
    const string BasePath = Root + "/Collectible.prefab";

    // GDD §11: the pickup trigger is generous so the player does not need precision.
    const float MinTriggerRadius = 0.6f;

    static GameObject[] LoadVariants()
    {
        var guids = AssetDatabase.FindAssets("Collectible_ t:Prefab", new[] { Root });
        var result = new GameObject[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
        return result;
    }

    [Test]
    public void EveryWasteTypeHasAVariantOfTheBasePrefab()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
        Assert.IsNotNull(basePrefab, "Missing " + BasePath);

        string[] kinds = { "Bottle", "Can", "Paper", "Bag", "JuiceBox", "Cup", "Jar", "Battery" };
        foreach (var k in kinds)
        {
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Collectible_" + k + ".prefab");
            Assert.IsNotNull(variant, "Missing Collectible_" + k);
            Assert.AreEqual(basePrefab, PrefabUtility.GetCorrespondingObjectFromSource(variant), variant.name + " must be a variant of Collectible");
        }
    }

    [Test]
    public void Variants_HaveAnEnlargedTriggerOnTheCollectibleLayer()
    {
        int layer = LayerMask.NameToLayer("Collectible");
        var variants = LoadVariants();
        Assert.IsNotEmpty(variants);
        foreach (var p in variants)
        {
            Assert.IsNotNull(p.GetComponent<Collectible>(), p.name + " needs a Collectible");
            var colliders = p.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length, p.name + " must have exactly one collider, on its root");
            var sphere = colliders[0] as SphereCollider;
            Assert.IsNotNull(sphere, p.name + " uses a sphere trigger");
            Assert.IsTrue(sphere.isTrigger, p.name + " collider must be a trigger");
            Assert.GreaterOrEqual(sphere.radius, MinTriggerRadius, p.name + " trigger is too small");
            foreach (var t in p.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(layer, t.gameObject.layer, t.name + " in " + p.name + " must be on the Collectible layer");
        }
    }

    [Test]
    public void Variants_ShowAModelWithAGlowingRim()
    {
        foreach (var p in LoadVariants())
        {
            var visual = p.transform.Find("Visual");
            Assert.IsNotNull(visual, p.name + " needs a Visual child");
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers, p.name + " has no model under Visual");

            bool glows = false;
            foreach (var r in renderers)
            foreach (var m in r.sharedMaterials)
            {
                Assert.IsNotNull(m, p.name + " has an empty material slot");
                Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name, m.name + " must use URP/Lit");
                // Bloom threshold is 1, so the rim must emit above it to glow.
                if (m.IsKeywordEnabled("_EMISSION") && m.GetColor("_EmissionColor").maxColorComponent > 1f) glows = true;
            }
            Assert.IsTrue(glows, p.name + " needs an emissive rim material above the bloom threshold");
        }
    }
}
