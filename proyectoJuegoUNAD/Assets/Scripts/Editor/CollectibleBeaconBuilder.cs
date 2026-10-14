using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives every waste item a beacon that can be seen from far away (playtest request: "haz de luz o partículas sobre
/// cada residuo, visible de lejos"): a soft vertical light beam that fades out upward, plus a few sparkles drifting up
/// it. Both go on a <c>Beacon</c> child of the base prefab <c>Assets/Prefabs/Collectibles/Collectible.prefab</c>, so all
/// eight variants inherit it, and both disappear with the item when it is collected (the item deactivates itself).
/// The beacon is not under <c>Visual</c>, so it neither bobs nor spins, and it has no collider.
/// Menu: Forest Guardian > Collectibles > Build Waste Beacon. Safe to run again: it regenerates the beacon and its assets.
/// </summary>
public static class CollectibleBeaconBuilder
{
    private const string BasePrefabPath = "Assets/Prefabs/Collectibles/Collectible.prefab";
    private const string BeaconName = "Beacon";
    private const string GeneratedFolder = "Assets/Generated";
    private const string BeamMeshPath = GeneratedFolder + "/Collectible_BeamMesh.asset";
    private const string BeamTexturePath = GeneratedFolder + "/Collectible_BeamGradient.png";
    private const string SparkleTexturePath = GeneratedFolder + "/Collectible_Sparkle.png";
    private const string BeamMaterialPath = "Assets/Materials/Waste/Collectible_Beam.mat";
    private const string SparkleMaterialPath = "Assets/Materials/Waste/Collectible_Sparkle.mat";

    // Beam: a thin open tube standing on the item. Tall enough to clear bushes and ledges, thin enough not to hide the
    // trail. Additive, so it reads as light; warm yellow-green so it stands out from the green forest and the blue fog.
    private const float BeamHeight = 7f;
    private const float BeamRadius = 0.22f;
    private const int BeamSegments = 16;
    // Starts just above the model: a beam through the model tinted it yellow and hid which waste it is.
    private const float BeamBaseY = 0.55f;
    private static readonly Color BeamColor = new Color(1f, 0.92f, 0.45f, 0.55f);

    // Sparkles: slow, few and soft (no flashing). They rise a third of the beam and fade in and out.
    private static readonly Color SparkleColor = new Color(1f, 0.95f, 0.6f, 0.9f);
    private const float SparkleRate = 5f;
    private const float SparkleLifetime = 3f;
    private const float SparkleSpeed = 0.8f;
    private const float SparkleSize = 0.18f;

    [MenuItem("Forest Guardian/Collectibles/Build Waste Beacon")]
    public static void Build()
    {
        Directory.CreateDirectory(GeneratedFolder);
        var beamMesh = BuildBeamMesh();
        var beamTexture = WriteTexture(BeamTexturePath, 8, 64, BeamTexel, TextureWrapMode.Clamp);
        var sparkleTexture = WriteTexture(SparkleTexturePath, 32, 32, SparkleTexel, TextureWrapMode.Clamp);
        var beamMaterial = BuildAdditiveMaterial(BeamMaterialPath, beamTexture, BeamColor, doubleSided: true);
        var sparkleMaterial = BuildAdditiveMaterial(SparkleMaterialPath, sparkleTexture, Color.white, doubleSided: false);

        var root = PrefabUtility.LoadPrefabContents(BasePrefabPath);
        try
        {
            var old = root.transform.Find(BeaconName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var beacon = new GameObject(BeaconName);
            beacon.layer = root.layer;
            beacon.transform.SetParent(root.transform, false);

            var beam = new GameObject("Beam", typeof(MeshFilter), typeof(MeshRenderer));
            beam.layer = root.layer;
            beam.transform.SetParent(beacon.transform, false);
            beam.transform.localPosition = new Vector3(0f, BeamBaseY, 0f);
            beam.GetComponent<MeshFilter>().sharedMesh = beamMesh;
            ConfigureUnlitRenderer(beam.GetComponent<MeshRenderer>(), beamMaterial);

            var sparkles = new GameObject("Sparkles", typeof(ParticleSystem));
            sparkles.layer = root.layer;
            sparkles.transform.SetParent(beacon.transform, false);
            sparkles.transform.localPosition = new Vector3(0f, BeamBaseY, 0f);
            sparkles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // emit upward
            ConfigureSparkles(sparkles.GetComponent<ParticleSystem>(), sparkleMaterial);

            PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("CollectibleBeaconBuilder: beacon added to " + BasePrefabPath + " (all waste variants inherit it).");
    }

    // An open tube (no caps), UV v = 0 at the bottom and 1 at the top, so the gradient texture fades it upward.
    private static Mesh BuildBeamMesh()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BeamMeshPath);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "Collectible_BeamMesh" };
        mesh.Clear();

        var vertices = new Vector3[(BeamSegments + 1) * 2];
        var uvs = new Vector2[vertices.Length];
        var normals = new Vector3[vertices.Length];
        var triangles = new int[BeamSegments * 6];
        for (int i = 0; i <= BeamSegments; i++)
        {
            float u = (float)i / BeamSegments;
            float angle = u * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i * 2] = dir * BeamRadius;
            vertices[i * 2 + 1] = dir * BeamRadius + Vector3.up * BeamHeight;
            uvs[i * 2] = new Vector2(u, 0f);
            uvs[i * 2 + 1] = new Vector2(u, 1f);
            normals[i * 2] = normals[i * 2 + 1] = dir;
        }
        for (int i = 0; i < BeamSegments; i++)
        {
            int b = i * 2, t = i * 6;
            triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 2;
            triangles[t + 3] = b + 2; triangles[t + 4] = b + 1; triangles[t + 5] = b + 3;
        }
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        if (isNew) AssetDatabase.CreateAsset(mesh, BeamMeshPath);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // White, alpha only: fades in over the first 8 % (so the foot does not cut off hard) and out toward the top.
    private static Color BeamTexel(float x, float y)
    {
        float fadeIn = Mathf.SmoothStep(0f, 1f, y / 0.08f);
        float fadeOut = Mathf.Pow(1f - y, 1.6f);
        return new Color(1f, 1f, 1f, fadeIn * fadeOut);
    }

    // A soft round dot.
    private static Color SparkleTexel(float x, float y)
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) * 2f;
        float a = Mathf.Clamp01(1f - d);
        return new Color(1f, 1f, 1f, a * a);
    }

    private static Texture2D WriteTexture(string path, int width, int height, System.Func<float, float, Color> texel, TextureWrapMode wrap)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            texture.SetPixel(x, y, texel((x + 0.5f) / width, (y + 0.5f) / height));
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = wrap;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material BuildAdditiveMaterial(string path, Texture2D texture, Color color, bool doubleSided)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f); // Transparent
        material.SetFloat("_Blend", 2f);   // Additive: light only adds, so overlapping beams and sparkles just glow
        material.SetFloat("_Cull", doubleSided ? 0f : 2f);
        BaseShaderGUI.SetupMaterialBlendMode(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureUnlitRenderer(Renderer renderer, Material material)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private static void ConfigureSparkles(ParticleSystem system, Material material)
    {
        var main = system.main;
        main.loop = true;
        main.prewarm = true; // already sparkling when the level starts
        main.startLifetime = new ParticleSystem.MinMaxCurve(SparkleLifetime * 0.8f, SparkleLifetime * 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(SparkleSpeed * 0.6f, SparkleSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(SparkleSize * 0.6f, SparkleSize);
        main.startColor = SparkleColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;
        main.gravityModifier = 0f;

        var emission = system.emission;
        emission.rateOverTime = SparkleRate;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone; // along local +Z, which the -90° X rotation turns upward
        shape.angle = 4f;
        shape.radius = BeamRadius * 1.5f;
        shape.radiusThickness = 1f;

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ConfigureUnlitRenderer(renderer, material);
    }
}
