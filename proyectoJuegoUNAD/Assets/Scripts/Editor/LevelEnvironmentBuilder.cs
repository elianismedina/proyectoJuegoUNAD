using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// Builds the environment of Level01 from the prefab variants in Assets/Prefabs/Environment:
/// the walkable lane, the hill terrain around it, the static obstacles and the seeded scenery of the three zones.
/// Everything is generated under one root called "Environment", so running it again replaces the previous layout
/// (hand edits inside "Environment" are lost; edit the tables below instead, or stop using the tool once the layout is final).
/// See Docs/NaturePackIntegrationPlan.md, phase C.
/// </summary>
public static class LevelEnvironmentBuilder
{
    private const string PrefabRoot = "Assets/Prefabs/Environment/";
    private const string LaneMaterialPath = "Assets/Materials/Lane_Path.mat";
    private const string BaseGroundMaterialPath = "Assets/Materials/Terrain_Base.mat";

    // Course layout, in world X. The lane is three contiguous 60 m sections, one per zone.
    private const float CourseStart = -12f;
    private const float ZoneLength = 60f;
    private const int ZoneCount = 3;
    private const float LaneWidthZ = 3f;   // Player Z is locked, so a shallow lane keeps the camera view uncluttered.
    private const float LaneThickness = 4f; // Deep enough that no void shows under the lane edge.
    private const float SpawnOffset = 4f; // Player starts this far from the start barrier.

    // How far past each end of the course the scenery and terrain continue, so the camera never sees a void.
    private const float EndMargin = 30f;

    private static readonly string[] ZoneNames = { "Zone1_Learning", "Zone2_Development", "Zone3_Challenge" };

    private struct ZoneStyle
    {
        public float MidTrees, MidBushes, BackTrees, Rocks, Foreground; // objects per meter of course
        public ZoneStyle(float midTrees, float midBushes, float backTrees, float rocks, float foreground)
        {
            MidTrees = midTrees; MidBushes = midBushes; BackTrees = backTrees; Rocks = rocks; Foreground = foreground;
        }
    }

    // Denser every zone, as the GDD asks for a progressively harder and more crowded course (section 8.2).
    private static readonly ZoneStyle[] Styles =
    {
        new ZoneStyle(0.25f, 0.35f, 0.20f, 0.08f, 0.6f),
        new ZoneStyle(0.33f, 0.40f, 0.25f, 0.12f, 0.6f),
        new ZoneStyle(0.42f, 0.50f, 0.32f, 0.18f, 0.6f),
    };

    // Static obstacles on the lane: prefab name and world X. Wide ones (Rock_01, Rock_05) stay out of the tight spots.
    private static readonly (string prefab, float x)[][] Obstacles =
    {
        // Zone 1: few obstacles, wide gaps, simple jumps.
        new[] { ("Obstacle_Stump_01", 14f), ("Obstacle_Rock_04", 26f), ("Obstacle_Rock_01", 38f) },
        // Zone 2: more obstacles, shorter gaps.
        new[] { ("Obstacle_Rock_04", 55f), ("Obstacle_Log_01", 62f), ("Obstacle_Stump_01", 80f), ("Obstacle_Rock_01", 106f) },
        // Zone 3: crowded, mixed obstacles close together.
        new[] { ("Obstacle_Rock_04", 113f), ("Obstacle_Stump_01", 119f), ("Obstacle_Log_01", 136f), ("Obstacle_Rock_05", 144f),
                ("Obstacle_Stump_01", 150f), ("Obstacle_Rock_04", 155f), ("Obstacle_Log_01", 161f) },
    };

    private static readonly string[] TreeNames = { "Tree_01", "Tree_02", "Tree_03", "Tree_04", "Tree_05" };
    private static readonly string[] BushNames = { "Bush_01", "Bush_02", "Bush_03" };
    private static readonly string[] RockNames = { "Rock_01", "Rock_04", "Rock_05" };
    private static readonly string[] SmallFoliage = { "Grass_01", "Grass_02", "Flowers_01", "Flowers_02", "Mushroom_01", "Mushroom_02", "Rock_02", "Rock_03" };

    private static float CourseEnd => CourseStart + ZoneLength * ZoneCount;

    [MenuItem("Forest Guardian/Level/Build Level01 Environment")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Level01")
        {
            Debug.LogError("LevelEnvironmentBuilder: open the Level01 scene first (active scene is '" + scene.name + "').");
            return;
        }

        RemoveExisting();

        var root = new GameObject("Environment");
        var laneRoot = Child(root.transform, "Lane");
        var terrainRoot = Child(root.transform, "Terrain");

        BuildLane(laneRoot);
        var raycastSet = new HashSet<Collider>();
        var tileColliders = new List<Collider>();
        BuildTerrain(terrainRoot, tileColliders);
        foreach (var c in tileColliders) raycastSet.Add(c);
        Physics.SyncTransforms();

        for (int zone = 0; zone < ZoneCount; zone++)
            BuildZone(root.transform, zone, raycastSet);

        // The tile colliders only existed so scenery could be placed on the hills; they must not ship.
        foreach (var c in tileColliders) Object.DestroyImmediate(c);

        PlaceSpawnAndCamera();
        ResizeCameraBounds();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("LevelEnvironmentBuilder: built Level01 environment (course x " + CourseStart + " to " + CourseEnd + ").");
    }

    private static void RemoveExisting()
    {
        foreach (var name in new[] { "Environment", "Ground_Provisional" })
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);
        }
    }

    private static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject Prefab(string name, string folder)
    {
        var path = PrefabRoot + folder + "/" + name + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new System.InvalidOperationException("Missing prefab " + path);
        return prefab;
    }

    private static GameObject Spawn(GameObject prefab, Transform parent)
    {
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    private static void MakeStatic(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
    }

    private static Bounds RendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    // ---------------------------------------------------------------- lane

    private static Material LaneMaterial()
    {
        return FlatMaterial(LaneMaterialPath, "Lane_Path", new Color(0.50f, 0.38f, 0.25f));
    }

    private static Material FlatMaterial(string path, string name, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void BuildLane(Transform laneRoot)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        var material = LaneMaterial();

        // The walkable lane is flat on purpose: bumpy terrain would break the grounded check and coyote time.
        for (int zone = 0; zone < ZoneCount; zone++)
        {
            var lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lane.name = "Lane_" + ZoneNames[zone];
            lane.transform.SetParent(laneRoot, false);
            lane.transform.position = new Vector3(CourseStart + ZoneLength * (zone + 0.5f), -LaneThickness / 2f, 0f);
            lane.transform.localScale = new Vector3(ZoneLength, LaneThickness, LaneWidthZ);
            lane.layer = groundLayer;
            lane.GetComponent<MeshRenderer>().sharedMaterial = material;
            MakeStatic(lane);
        }

        // Invisible walls keep the player from walking off either end of the course.
        AddBarrier(laneRoot, "Barrier_Start", CourseStart - 0.5f);
        AddBarrier(laneRoot, "Barrier_End", CourseEnd + 0.5f);
    }

    private static void AddBarrier(Transform parent, string name, float x)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, 5f, 0f);
        go.AddComponent<BoxCollider>().size = new Vector3(1f, 12f, LaneWidthZ);
    }

    // ---------------------------------------------------------------- terrain

    private static void BuildTerrain(Transform terrainRoot, List<Collider> tileColliders)
    {
        // The hill tiles are open surfaces with no skirt, so seams between them show the sky through.
        // A flat green slab underneath hides every seam.
        var baseGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseGround.name = "BaseGround";
        baseGround.transform.SetParent(terrainRoot, false);
        float baseX0 = CourseStart - EndMargin - 15f, baseX1 = CourseEnd + EndMargin + 15f;
        baseGround.transform.position = new Vector3((baseX0 + baseX1) / 2f, -2.1f, 40f);
        baseGround.transform.localScale = new Vector3(baseX1 - baseX0, 1f, 150f);
        Object.DestroyImmediate(baseGround.GetComponent<BoxCollider>());
        baseGround.GetComponent<MeshRenderer>().sharedMaterial = FlatMaterial(BaseGroundMaterialPath, "Terrain_Base", new Color(0.37f, 0.55f, 0.32f));
        MakeStatic(baseGround);

        var tiles = new[] { Prefab("Terrain_Ground_01", "Terrain"), Prefab("Terrain_Ground_03", "Terrain") };
        var mountain = Prefab("Terrain_Ground_02", "Terrain");
        var rng = new Random(7);

        float x0 = CourseStart - EndMargin;
        float x1 = CourseEnd + EndMargin;
        const float tile = 30f;

        // Front row sits below the lane so the camera never looks at a void, and never hides the lane.
        BuildTileRow(Child(terrainRoot, "Front"), tiles, rng, x0, x1, tile, -LaneWidthZ / 2f - tile / 2f, -1.5f, tileColliders);
        // Two rows of hills behind the lane carry the midground and background scenery.
        BuildTileRow(Child(terrainRoot, "HillsNear"), tiles, rng, x0, x1, tile, LaneWidthZ / 2f + tile / 2f, -0.5f, tileColliders);
        BuildTileRow(Child(terrainRoot, "HillsFar"), tiles, rng, x0, x1, tile, LaneWidthZ / 2f + tile * 1.5f, -0.5f, tileColliders);

        // Both ends of the course need ground in the lane band too, or a trench shows between the front and back hills.
        var caps = Child(terrainRoot, "EndCaps");
        foreach (float cx in new[] { CourseStart - tile / 2f, CourseEnd + tile / 2f })
        {
            var go = Spawn(tiles[rng.Next(tiles.Length)], caps);
            go.transform.localScale = new Vector3(1f, 1f, 0.27f);
            go.transform.position = Vector3.zero;
            var b = RendererBounds(go);
            go.transform.position = new Vector3(cx - b.center.x, -0.5f - b.min.y, -b.center.z);
            MakeStatic(go);
        }

        // Mountains: the tall hill tile stretched wide and high, far behind everything else.
        var mountains = Child(terrainRoot, "Mountains");
        const float spacing = 45f;
        for (float x = x0; x < x1 + spacing; x += spacing)
        {
            var go = Spawn(mountain, mountains);
            go.transform.rotation = Quaternion.Euler(0f, 90f * rng.Next(4), 0f);
            go.transform.localScale = new Vector3(1.8f, 2.2f, 1.8f);
            go.transform.position = Vector3.zero;
            var b = RendererBounds(go);
            go.transform.position = new Vector3(x - b.center.x, -0.5f - b.min.y, 92f - b.center.z);
            MakeStatic(go);
        }
    }

    private static void BuildTileRow(Transform row, GameObject[] tiles, Random rng, float x0, float x1, float tile, float centerZ, float baseY, List<Collider> tileColliders)
    {
        for (float x = x0 + tile / 2f; x < x1 + tile / 2f; x += tile)
        {
            var go = Spawn(tiles[rng.Next(tiles.Length)], row);
            go.transform.rotation = Quaternion.Euler(0f, 90f * rng.Next(4), 0f);
            go.transform.position = Vector3.zero;
            var b = RendererBounds(go);
            go.transform.position = new Vector3(x - b.center.x, baseY - b.min.y, centerZ - b.center.z);
            MakeStatic(go);

            // Temporary colliders so scenery can be dropped onto the hills with a raycast.
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                tileColliders.Add(mc);
            }
        }
    }

    // ---------------------------------------------------------------- zones

    private static void BuildZone(Transform environment, int zone, HashSet<Collider> raycastSet)
    {
        var zoneRoot = Child(environment, ZoneNames[zone]);
        var style = Styles[zone];
        var rng = new Random(1000 + zone);

        float zoneStart = CourseStart + ZoneLength * zone;
        float zoneEnd = zoneStart + ZoneLength;
        float xMin = zone == 0 ? CourseStart - EndMargin : zoneStart;
        float xMax = zone == ZoneCount - 1 ? CourseEnd + EndMargin : zoneEnd;
        float length = xMax - xMin;

        var obstacles = Child(zoneRoot, "Obstacles");
        foreach (var (prefab, x) in Obstacles[zone])
        {
            var go = Spawn(Prefab(prefab, "Obstacles"), obstacles);
            go.transform.position = new Vector3(x, 0f, 0f);
            MakeStatic(go);
        }

        var midground = Child(zoneRoot, "Midground");
        var background = Child(zoneRoot, "Background");
        var foreground = Child(zoneRoot, "Foreground");

        var placed = new List<Vector2>();
        Scatter(rng, midground, TreeNames, Mathf.RoundToInt(style.MidTrees * length), xMin, xMax, 3.5f, 11f, 0.9f, 1.4f, 2.4f, placed, raycastSet);
        Scatter(rng, midground, BushNames, Mathf.RoundToInt(style.MidBushes * length), xMin, xMax, 3.2f, 10f, 0.8f, 1.5f, 1.2f, placed, raycastSet);
        Scatter(rng, midground, RockNames, Mathf.RoundToInt(style.Rocks * length), xMin, xMax, 3.4f, 9f, 1f, 2f, 2.5f, placed, raycastSet);

        var backPlaced = new List<Vector2>();
        Scatter(rng, background, TreeNames, Mathf.RoundToInt(style.BackTrees * length), xMin, xMax, 13f, 45f, 1.6f, 2.6f, 4f, backPlaced, raycastSet);

        // Foreground is only low plants and pebbles, so it can never hide the lane or the player's feet.
        var frontPlaced = new List<Vector2>();
        Scatter(rng, foreground, SmallFoliage, Mathf.RoundToInt(style.Foreground * length), xMin, xMax, -4.6f, -2.4f, 0.9f, 1.4f, 0.8f, frontPlaced, raycastSet);
    }

    private static void Scatter(Random rng, Transform parent, string[] names, int count, float xMin, float xMax, float zMin, float zMax,
        float scaleMin, float scaleMax, float minDistance, List<Vector2> placed, HashSet<Collider> raycastSet)
    {
        int attempts = 0;
        int made = 0;
        while (made < count && attempts < count * 20)
        {
            attempts++;
            float x = Mathf.Lerp(xMin, xMax, (float)rng.NextDouble());
            float z = Mathf.Lerp(zMin, zMax, (float)rng.NextDouble());
            var point = new Vector2(x, z);

            bool tooClose = false;
            foreach (var other in placed)
                if ((other - point).sqrMagnitude < minDistance * minDistance) { tooClose = true; break; }
            if (tooClose) continue;
            if (!TryGroundY(x, z, raycastSet, out float y)) continue;

            var name = names[rng.Next(names.Length)];
            var go = Spawn(Prefab("Scenery_" + name, "Scenery"), parent);
            go.transform.position = new Vector3(x, y - 0.03f, z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            go.transform.localScale = Vector3.one * Mathf.Lerp(scaleMin, scaleMax, (float)rng.NextDouble());
            MakeStatic(go);

            placed.Add(point);
            made++;
        }
    }

    private static bool TryGroundY(float x, float z, HashSet<Collider> raycastSet, out float y)
    {
        y = float.MinValue;
        bool found = false;
        foreach (var hit in Physics.RaycastAll(new Vector3(x, 30f, z), Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!raycastSet.Contains(hit.collider) || hit.point.y <= y) continue;
            y = hit.point.y;
            found = true;
        }
        return found;
    }

    // ---------------------------------------------------------------- spawn and camera

    private static void PlaceSpawnAndCamera()
    {
        float spawnX = CourseStart + SpawnOffset;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            player.transform.position = new Vector3(spawnX, 0.05f, 0f);
            if (controller != null) controller.enabled = wasEnabled;
        }

        // Start both cameras where the follow camera will put them, so the first frame does not jump.
        foreach (var name in new[] { "Main Camera", "CM Side Camera" })
        {
            var cam = GameObject.Find(name);
            if (cam == null) continue;
            var p = cam.transform.position;
            cam.transform.position = new Vector3(spawnX, p.y, p.z);
        }
    }

    private static void ResizeCameraBounds()
    {
        var bounds = GameObject.Find("CameraBounds");
        if (bounds == null)
        {
            Debug.LogWarning("LevelEnvironmentBuilder: no CameraBounds object found; the camera confiner was not resized.");
            return;
        }

        // Course length plus 2 m at each end, as the level design rule says.
        var box = bounds.GetComponent<BoxCollider>();
        var size = box.size;
        size.x = ZoneLength * ZoneCount + 4f;
        box.size = size;
        var pos = bounds.transform.position;
        bounds.transform.position = new Vector3((CourseStart + CourseEnd) / 2f, pos.y, pos.z);
    }
}
