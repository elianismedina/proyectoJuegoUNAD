using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// Builds Level01 as a winding forest trail for the third person game (Docs/ThirdPersonConversionPlan.md, phases 2–4).
/// From the tables below it generates, under one root called "Environment":
/// <list type="bullet">
/// <item>the forest floor: a low-poly mesh that follows the trail, with a dirt path in the middle, earth banks the
/// player must jump up, and streams that cut across it (falling in loses);</item>
/// <item>the play area: invisible walls along both sides of the corridor, hidden behind a line of trees;</item>
/// <item>obstacles, ledges, scenery (trees inside the corridor get trunk colliders) and the far hills;</item>
/// <item>a <see cref="ForestTrail"/> with the trail's centre line, used by tests and runtime code.</item>
/// </list>
/// It also moves the scene's hazards, waste, goal, player and cameras onto the trail. Running it again replaces the
/// whole layout: hand edits under "Environment" and "Collectibles" are lost, so change the tables instead.
/// </summary>
public static class LevelEnvironmentBuilder
{
    private const string PrefabRoot = "Assets/Prefabs/Environment/";
    private const string CollectibleRoot = "Assets/Prefabs/Collectibles/";
    private const string GeneratedFolder = "Assets/Generated";
    private const string FloorMeshPath = GeneratedFolder + "/Level01_ForestFloor.asset";
    private const string BoundsMeshPath = GeneratedFolder + "/Level01_PlayAreaBounds.asset";
    private const string LaneMaterialPath = "Assets/Materials/Lane_Path.mat";
    private const string GrassMaterialPath = "Assets/Materials/Terrain_Base.mat";
    private const string WaterMaterialPath = "Assets/Materials/Stream_Water.mat";
    private const string LevelConfigPath = "Assets/Config/LevelConfig_Level01.asset";

    // ------------------------------------------------------------------ trail shape

    // The trail's centre line on the ground (x, z), from the start to the goal; smoothed with a Catmull-Rom spline.
    // The first stretch runs straight along +X from x = -12 so the start matches the old course (tests rely on it).
    private static readonly Vector2[] Waypoints =
    {
        new Vector2(-12f, 0f), new Vector2(18f, 0f), new Vector2(40f, 8f), new Vector2(62f, 18f), new Vector2(85f, 14f),
        new Vector2(105f, 0f), new Vector2(128f, -10f), new Vector2(152f, -8f), new Vector2(172f, 6f), new Vector2(192f, 18f),
        new Vector2(214f, 14f), new Vector2(232f, 4f), new Vector2(246f, 0f),
    };

    private const float TrailHalfWidth = 2f;       // Dirt path.
    private const float CorridorHalfWidth = 6f;    // Walkable forest on each side of the centre line.
    private const float EndGateLength = 8f;        // The corridor narrows to the path before the goal,
    private const float EndGateHalfWidth = 1.6f;   // so nobody can walk around the goal trigger.
    private const float FloorMargin = 25f;         // Ground beyond the walls, for the trees and the view.
    private const float FloorFalloff = 8f;         // Then the ground slopes down out of sight...
    private const float FalloffDepth = -3f;        // ...to the level of the far base ground.
    private const float StreamBottom = -2.5f;
    private const float WaterY = -1.1f;
    private const float BoundsBottom = -3f, BoundsTop = 6f;
    private const float KillPlaneY = -2f;          // Below the stream banks' top (0) and above the stream bottom.
    private const float SpawnDistance = 4f;
    private const float GoalDistanceFromEnd = 4f;

    // Clearings widen the corridor on one side (+1 left, -1 right of travel). Always on the outer side of a bend.
    private static readonly (float s, float length, int side, float extra)[] Clearings =
    {
        (78f, 18f, +1, 9f), (114f, 20f, +1, 10f), (172f, 20f, -1, 9f), (222f, 24f, +1, 10f), (252f, 18f, +1, 8f),
    };

    // Earth banks across the whole corridor: the player must jump up onto them (≤ 1.2 m, like the obstacles).
    private static readonly (float start, float end, float height)[] Banks =
    {
        (64f, 72f, 0.8f), (132f, 142f, 1f), (215f, 229.15f, 1.2f),
    };

    // Streams across the trail (none in Zone 1). Widths stay well inside the 4.9 m a running jump covers.
    // The last one starts where the highest bank ends, so the player jumps down across it.
    private static readonly (float center, float width)[] Streams =
    {
        (104f, 2f), (196f, 2.2f), (230.4f, 2.5f),
    };

    // ------------------------------------------------------------------ things on the trail

    // Solid obstacles on the path, by trail distance and sideways offset (left +). The count grows every zone.
    private static readonly (string prefab, float s, float lateral)[] Obstacles =
    {
        // Zone 1 (s 0–94). The stump at s = 26 stands at x ≈ 14, where it was on the old course (PlayMode tests run there).
        ("Obstacle_Stump_01", 26f, 0f), ("Obstacle_Rock_04", 55f, -0.6f), ("Obstacle_Rock_01", 86f, 0.5f),
        // Zone 2 (s 94–188).
        ("Obstacle_Log_01", 98f, 0f), ("Obstacle_Stump_01", 128f, 0.6f), ("Obstacle_Rock_04", 147f, -0.4f), ("Obstacle_Rock_01", 182f, 0f),
        // Zone 3 (s 188–281).
        ("Obstacle_Stump_01", 189f, -0.5f), ("Obstacle_Rock_05", 240f, 0f), ("Obstacle_Log_01", 250f, 0f),
        ("Obstacle_Stump_01", 258f, 0.8f), ("Obstacle_Rock_04", 264f, -0.5f),
    };

    // Rocks off the path that hold a waste item on top.
    private static readonly (string prefab, float s, float lateral)[] Ledges =
    {
        ("Obstacle_Rock_01", 78f, 11f),
    };

    // Hazards already in the scene (by name prefix), moved onto the trail and turned to follow it.
    private const float MudS = 121f, RollingLogS = 158f, FallingRockS = 206f;
    private const float MudWidthScale = 1.5f; // The mud slab is 3 m deep; 4.5 m covers the 4 m path.

    // Waste: type, trail distance, sideways offset, height above the ground below it (or above the ledge it sits on).
    // At most three lie on the path at foot height (Zone 1, to teach collecting); the rest need a detour, a jump or a risk.
    private static readonly (string type, float s, float lateral, float lift, bool onLedge)[] Waste =
    {
        // Zone 1: two on the path, one on a rock in the first clearing.
        ("Bottle", 16f, 0f, 0f, false), ("Can", 45f, 0.8f, 0f, false), ("Paper", 78f, 11f, 0.05f, true),
        // Zone 2: high in a clearing, low over a stream, in the rolling log's path, high in a clearing.
        ("Bag", 114f, 12f, 2.2f, false), ("Cup", 104f, 3f, 0.3f, false), ("Bottle", 161f, 0f, 0f, false), ("Battery", 172f, -11f, 2.2f, false),
        // Zone 3: low over a stream, next to the falling rock, high on the last bank, low over the last stream, high in a clearing.
        ("Jar", 196f, -2.5f, 0.3f, false), ("Can", 207.5f, 0f, 0f, false), ("Paper", 222f, 9f, 2.2f, false),
        ("Bag", 230.4f, -3f, 0.5f, false), ("Battery", 252f, 10f, 2.4f, false),
    };

    // ------------------------------------------------------------------ scenery

    private static readonly string[] TreeNames = { "Tree_01", "Tree_02", "Tree_03", "Tree_04", "Tree_05" };
    private static readonly string[] BushNames = { "Bush_01", "Bush_02", "Bush_03" };
    private static readonly string[] RockNames = { "Rock_01", "Rock_04", "Rock_05" };
    private static readonly string[] SmallFoliage = { "Grass_01", "Grass_02", "Flowers_01", "Flowers_02", "Mushroom_01", "Mushroom_02", "Rock_02", "Rock_03" };

    private static readonly string[] ZoneNames = { "Zone1_Learning", "Zone2_Development", "Zone3_Challenge" };

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

        var trail = root.AddComponent<ForestTrail>();
        trail.SetPath(SampleTrail(), StreamSpans());
        float length = trail.Length;

        var floorCollider = BuildFloor(Child(root.transform, "Terrain"), trail);
        BuildBounds(Child(root.transform, "PlayArea"), trail);
        BuildStreams(Child(root.transform, "Streams"), trail);
        Physics.SyncTransforms();

        var reserved = new List<Vector2>(); // Spots the scenery must keep clear of.
        var zoneRoots = new Transform[ForestTrail.ZoneCount];
        for (int z = 0; z < zoneRoots.Length; z++) zoneRoots[z] = Child(root.transform, ZoneNames[z]);

        foreach (var (prefab, s, lateral) in Obstacles)
            PlaceOnTrail(Prefab(prefab, "Obstacles"), ZoneChild(zoneRoots, trail, s, "Obstacles"), trail, s, lateral, reserved);

        var ledgeTops = new Dictionary<float, float>();
        foreach (var (prefab, s, lateral) in Ledges)
        {
            var go = PlaceOnTrail(Prefab(prefab, "Obstacles"), ZoneChild(zoneRoots, trail, s, "Ledges"), trail, s, lateral, reserved);
            ledgeTops[s] = RendererBounds(go).max.y;
        }

        PlaceHazards(trail, reserved);
        PlaceWaste(trail, ledgeTops, reserved);
        PlaceGoalSpawnAndCameras(trail);

        BuildScenery(Child(root.transform, "Scenery"), trail, floorCollider, reserved);
        BuildFarAway(Child(root.transform, "FarAway"), trail);
        UpdateLevelConfig();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("LevelEnvironmentBuilder: built the Level01 forest trail (" + length.ToString("0") + " m). Save the scene to keep it.");
    }

    private static void RemoveExisting()
    {
        foreach (var name in new[] { "Environment", "Ground_Provisional" })
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);
        }
    }

    // ================================================================== trail

    private static Vector3[] SampleTrail()
    {
        // Dense Catmull-Rom samples first, then resampled to one point per metre so ForestTrail stays light.
        var dense = new List<Vector3>();
        for (int i = 0; i + 1 < Waypoints.Length; i++)
        {
            Vector2 p0 = Waypoints[Mathf.Max(i - 1, 0)], p1 = Waypoints[i], p2 = Waypoints[i + 1], p3 = Waypoints[Mathf.Min(i + 2, Waypoints.Length - 1)];
            for (int k = 0; k < 40; k++)
            {
                float t = k / 40f, t2 = t * t, t3 = t2 * t;
                Vector2 p = 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
                dense.Add(new Vector3(p.x, 0f, p.y));
            }
        }
        var last = Waypoints[Waypoints.Length - 1];
        dense.Add(new Vector3(last.x, 0f, last.y));

        var result = new List<Vector3> { dense[0] };
        float carried = 0f;
        for (int i = 1; i < dense.Count; i++)
        {
            Vector3 a = dense[i - 1], b = dense[i];
            float segment = Vector3.Distance(a, b);
            float along = 1f - carried;
            while (along <= segment)
            {
                result.Add(Vector3.Lerp(a, b, along / segment));
                along += 1f;
            }
            carried = segment - (along - 1f);
        }
        if (Vector3.Distance(result[result.Count - 1], dense[dense.Count - 1]) > 0.05f) result.Add(dense[dense.Count - 1]);
        return result.ToArray();
    }

    private static Vector2[] StreamSpans()
    {
        var spans = new Vector2[Streams.Length];
        for (int i = 0; i < Streams.Length; i++)
            spans[i] = new Vector2(Streams[i].center - Streams[i].width / 2f, Streams[i].center + Streams[i].width / 2f);
        return spans;
    }

    /// <summary>Walkable half-width on one side (+1 left, -1 right) at trail distance s.</summary>
    private static float HalfWidth(float s, int side, float length)
    {
        if (s > length - EndGateLength) return EndGateHalfWidth;

        float width = CorridorHalfWidth;
        foreach (var c in Clearings)
        {
            if (c.side != side) continue;
            float half = c.length / 2f;
            float d = Mathf.Abs(s - c.s);
            if (d >= half) continue;
            // Full width in the middle, easing back to the normal corridor over the last 5 m at each end.
            width = Mathf.Max(width, CorridorHalfWidth + c.extra * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((half - d) / 5f)));
        }
        return width;
    }

    /// <summary>Ground height inside the corridor at trail distance s (banks raise it), ignoring streams.</summary>
    private static float CorridorHeight(float s)
    {
        foreach (var b in Banks)
            if (s >= b.start && s < b.end) return b.height;
        return 0f;
    }

    private static bool InStream(float s)
    {
        foreach (var st in Streams)
            if (s > st.center - st.width / 2f && s < st.center + st.width / 2f) return true;
        return false;
    }

    // Trail frame at s: local X along the travel direction, local Z to the left. On the old straight course this was
    // the identity rotation, so the prefabs keep the orientation they were designed with.
    private static Quaternion Frame(ForestTrail trail, float s) => Quaternion.LookRotation(trail.LeftAt(s), Vector3.up);

    private static Vector3 OnTrail(ForestTrail trail, float s, float lateral, float height)
        => CenterAt(trail, s) + trail.LeftAt(s) * lateral + Vector3.up * height;

    // Like ForestTrail.PointAt, but continues straight past both ends, for the ground and trees beyond the walls.
    private static Vector3 CenterAt(ForestTrail trail, float s)
    {
        float length = trail.Length;
        if (s < 0f) return trail.PointAt(0f) + trail.TangentAt(0f) * s;
        if (s > length) return trail.PointAt(length) + trail.TangentAt(length) * (s - length);
        return trail.PointAt(s);
    }

    private const float EndExtension = 15f; // Ground and trees continue this far past the start and the goal.

    // Signed curvature (radians per metre, left turn positive), measured over 4 m.
    private static float Curvature(ForestTrail trail, float s)
    {
        Vector3 a = trail.TangentAt(s - 2f), b = trail.TangentAt(s + 2f);
        return Vector3.SignedAngle(a, b, Vector3.down) * Mathf.Deg2Rad / 4f;
    }

    // ================================================================== floor

    private struct Section
    {
        public float S;
        public bool Before; // At a breakpoint: true = profile just before it, false = just after.
    }

    private static Collider BuildFloor(Transform terrainRoot, ForestTrail trail)
    {
        float length = trail.Length;

        // Cross-sections every metre, plus two at every breakpoint (bank edges, stream edges, the end gate)
        // so steps and stream banks come out as exact vertical walls.
        var breaks = new List<float> { length - EndGateLength };
        foreach (var b in Banks) { breaks.Add(b.start); breaks.Add(b.end); }
        foreach (var st in Streams) { breaks.Add(st.center - st.width / 2f); breaks.Add(st.center + st.width / 2f); }

        var sections = new List<Section>();
        for (float s = -EndExtension; s < length + EndExtension; s += 1f) sections.Add(new Section { S = s });
        sections.Add(new Section { S = length + EndExtension });
        foreach (float b in breaks)
        {
            sections.Add(new Section { S = b, Before = true });
            sections.Add(new Section { S = b, Before = false });
        }
        sections.Sort((x, y) => x.S != y.S ? x.S.CompareTo(y.S) : y.Before.CompareTo(x.Before));

        var builder = new MeshBuilder(3); // 0 grass, 1 dirt path, 2 earth walls
        Vector3[] previous = null;
        float previousS = 0f;

        foreach (var section in sections)
        {
            float s = section.S;
            float sample = s + (section.Before ? -0.01f : 0.01f);
            var profile = Profile(trail, s, sample, length, out int[] onPath);

            if (previous != null && !InStream((previousS + s) / 2f))
            {
                bool wall = Mathf.Abs(s - previousS) < 0.001f;
                for (int k = 0; k + 1 < profile.Length; k++)
                {
                    int submesh = wall ? 2 : (IsPathQuad(onPath, k) ? 1 : 0);
                    // Winding: (previous k, previous k+1, current k+1, current k) faces up on flat ground.
                    builder.Quad(previous[k], previous[k + 1], profile[k + 1], profile[k], submesh, doubleSided: false);
                }
            }

            previous = profile;
            previousS = s;
        }

        var mesh = SaveMesh(builder.ToMesh("Level01_ForestFloor"), FloorMeshPath);

        var floor = new GameObject("ForestFloor");
        floor.transform.SetParent(terrainRoot, false);
        floor.layer = LayerMask.NameToLayer("Ground");
        floor.AddComponent<MeshFilter>().sharedMesh = mesh;
        floor.AddComponent<MeshRenderer>().sharedMaterials = new[] { GrassMaterial(), LaneMaterial(), LaneMaterial() };
        var collider = floor.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        MakeStatic(floor);
        return collider;
    }

    // Lateral sample index ranges: path (|l| ≤ 2), corridor (to the wall), then the outer ground.
    private const int CorridorSteps = 6;

    /// <summary>
    /// The cross-section at trail distance s, from the far right to the far left. <paramref name="sample"/> picks the
    /// heights (just before or after a breakpoint). <paramref name="onPathFlags"/> receives, per point, 1 when the point
    /// lies on the edge or inside of the dirt path, else 0.
    /// </summary>
    private static Vector3[] Profile(ForestTrail trail, float s, float sample, float length, out int[] onPathFlags)
    {
        var laterals = new List<float>();
        var heights = new List<float>();
        var onPath = new List<int>();

        float corridorHeight = CorridorHeight(sample);
        bool stream = InStream(sample);
        float k = Curvature(trail, s);

        for (int side = -1; side <= 1; side += 2)
        {
            float hw = HalfWidth(sample, side, length);
            float outer = hw + FloorMargin;
            // On the inside of a bend the ground would fold over itself past the centre of the turn.
            if (k * side > 0.0001f) outer = Mathf.Min(outer, 0.85f / Mathf.Abs(k));
            outer = Mathf.Max(outer, hw + 3.5f);

            // At the end gate the corridor is narrower than the path; the extra points then have zero width.
            float pathEdge = Mathf.Min(TrailHalfWidth, hw);
            var sideLaterals = new List<float> { 0f, pathEdge };
            for (int i = 1; i <= CorridorSteps; i++) sideLaterals.Add(Mathf.Lerp(pathEdge, hw, i / (float)CorridorSteps));
            sideLaterals.Add(hw + 3f);
            sideLaterals.Add(outer);
            sideLaterals.Add(outer + FloorFalloff);

            var sideHeights = new List<float>();
            var sidePath = new List<int>();
            for (int i = 0; i < sideLaterals.Count; i++)
            {
                float l = sideLaterals[i];
                float h = l <= hw + 0.001f ? corridorHeight : (i == sideLaterals.Count - 1 ? FalloffDepth : 0f);
                if (stream) h = Mathf.Min(h, StreamBottom);
                sideHeights.Add(h);
                sidePath.Add(l <= TrailHalfWidth + 0.001f ? 1 : 0);
            }

            if (side < 0)
            {
                // Right side goes first, from the far edge in to the centre (centre point added by the left side).
                for (int i = sideLaterals.Count - 1; i >= 1; i--)
                {
                    laterals.Add(-sideLaterals[i]);
                    heights.Add(sideHeights[i]);
                    onPath.Add(sidePath[i]);
                }
            }
            else
            {
                for (int i = 0; i < sideLaterals.Count; i++)
                {
                    laterals.Add(sideLaterals[i]);
                    heights.Add(sideHeights[i]);
                    onPath.Add(sidePath[i]);
                }
            }
        }

        var points = new Vector3[laterals.Count];
        Vector3 center = CenterAt(trail, s), left = trail.LeftAt(s);
        for (int i = 0; i < points.Length; i++) points[i] = center + left * laterals[i] + Vector3.up * heights[i];
        onPathFlags = onPath.ToArray();
        return points;
    }

    private static bool IsPathQuad(int[] onPath, int k) => onPath[k] == 1 && onPath[k + 1] == 1;

    // ================================================================== play area

    private static void BuildBounds(Transform parent, ForestTrail trail)
    {
        float length = trail.Length;
        var builder = new MeshBuilder(1);

        var samples = new List<(float s, bool before)>();
        for (float s = 0f; s < length; s += 1f) samples.Add((s, false));
        samples.Add((length - EndGateLength, true));
        samples.Add((length - EndGateLength, false));
        samples.Add((length, false));
        samples.Sort((a, b) => a.s != b.s ? a.s.CompareTo(b.s) : b.before.CompareTo(a.before));

        Vector3 Wall(float s, bool before, int side)
        {
            float sample = s + (before ? -0.01f : 0.01f);
            return OnTrail(trail, s, side * HalfWidth(sample, side, length), 0f);
        }

        for (int i = 0; i + 1 < samples.Count; i++)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 a = Wall(samples[i].s, samples[i].before, side), b = Wall(samples[i + 1].s, samples[i + 1].before, side);
                builder.Quad(a + Vector3.up * BoundsBottom, b + Vector3.up * BoundsBottom, b + Vector3.up * BoundsTop, a + Vector3.up * BoundsTop, 0, doubleSided: true);
            }
        }

        // Caps across the start and the end of the corridor.
        foreach (float s in new[] { 0f, length })
        {
            Vector3 r = Wall(s, s > 0f, -1), l = Wall(s, s > 0f, +1);
            builder.Quad(r + Vector3.up * BoundsBottom, l + Vector3.up * BoundsBottom, l + Vector3.up * BoundsTop, r + Vector3.up * BoundsTop, 0, doubleSided: true);
        }

        var mesh = SaveMesh(builder.ToMesh("Level01_PlayAreaBounds"), BoundsMeshPath);
        var go = new GameObject("PlayAreaBounds");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshCollider>().sharedMesh = mesh; // Invisible: no renderer.
        MakeStatic(go);
    }

    // ================================================================== streams

    private static void BuildStreams(Transform parent, ForestTrail trail)
    {
        var material = FlatMaterial(WaterMaterialPath, "Stream_Water", new Color(0.30f, 0.55f, 0.70f), 0.6f);
        for (int i = 0; i < Streams.Length; i++)
        {
            var (center, width) = Streams[i];
            var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "Stream_" + (i + 1);
            water.transform.SetParent(parent, false);
            Object.DestroyImmediate(water.GetComponent<BoxCollider>()); // Falling in is the point: nothing to stand on.
            water.transform.SetPositionAndRotation(OnTrail(trail, center, 0f, WaterY), Frame(trail, center));
            water.transform.localScale = new Vector3(width, 0.1f, 2f * (CorridorHalfWidth + FloorMargin + FloorFalloff));
            water.GetComponent<MeshRenderer>().sharedMaterial = material;
            MakeStatic(water);
        }
    }

    // ================================================================== placement

    private static Transform ZoneChild(Transform[] zoneRoots, ForestTrail trail, float s, string group)
    {
        var zone = zoneRoots[trail.ZoneAt(s)];
        var existing = zone.Find(group);
        return existing != null ? existing : Child(zone, group);
    }

    private static GameObject PlaceOnTrail(GameObject prefab, Transform parent, ForestTrail trail, float s, float lateral, List<Vector2> reserved)
    {
        var go = Spawn(prefab, parent);
        go.transform.SetPositionAndRotation(OnTrail(trail, s, lateral, CorridorHeight(s)), Frame(trail, s));
        MakeStatic(go);
        Reserve(reserved, go.transform.position);
        return go;
    }

    private static void Reserve(List<Vector2> reserved, Vector3 position) => reserved.Add(new Vector2(position.x, position.z));

    private static void PlaceHazards(ForestTrail trail, List<Vector2> reserved)
    {
        var hazards = GameObject.Find("Hazards");
        if (hazards == null)
        {
            Debug.LogWarning("LevelEnvironmentBuilder: no Hazards root in Level01; hazards were not moved.");
            return;
        }

        foreach (Transform h in hazards.transform)
        {
            Undo.RecordObject(h, "Move hazard onto the trail");
            if (h.name.StartsWith("MudZone"))
            {
                h.SetPositionAndRotation(OnTrail(trail, MudS, 0f, CorridorHeight(MudS)), Frame(trail, MudS));
                h.localScale = new Vector3(1f, 1f, MudWidthScale);
            }
            else if (h.name.StartsWith("RollingLog"))
            {
                // Rolls up and down the path; its centre (y = radius) sits on the ground.
                h.SetPositionAndRotation(OnTrail(trail, RollingLogS, 0f, CorridorHeight(RollingLogS) + 0.5f), Frame(trail, RollingLogS));
                var log = h.GetComponent<RollingLog>();
                if (log != null)
                {
                    var so = new SerializedObject(log);
                    so.FindProperty("travel").floatValue = 14f;
                    so.ApplyModifiedProperties();
                }
            }
            else if (h.name.StartsWith("FallingRock"))
            {
                // The trigger sits 4 m before the rock along local X, so travel along the trail arms it.
                h.SetPositionAndRotation(OnTrail(trail, FallingRockS, 0f, CorridorHeight(FallingRockS)), Frame(trail, FallingRockS));
            }
            else continue;

            Reserve(reserved, h.position);
        }
    }

    private static void PlaceWaste(ForestTrail trail, Dictionary<float, float> ledgeTops, List<Vector2> reserved)
    {
        var root = GameObject.Find("Collectibles");
        if (root == null) root = new GameObject("Collectibles");

        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);

        foreach (var (type, s, lateral, lift, onLedge) in Waste)
        {
            var path = CollectibleRoot + "Collectible_" + type + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.InvalidOperationException("Missing prefab " + path);

            float baseHeight = onLedge && ledgeTops.TryGetValue(s, out float top) ? top : CorridorHeight(s);
            var go = Spawn(prefab, root.transform);
            go.name = "Collectible_" + type + "_s" + Mathf.RoundToInt(s);
            go.transform.SetPositionAndRotation(OnTrail(trail, s, lateral, baseHeight + lift), Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(go, "Place waste");
            Reserve(reserved, go.transform.position);
        }
    }

    private static void PlaceGoalSpawnAndCameras(ForestTrail trail)
    {
        float length = trail.Length;

        var goal = Object.FindFirstObjectByType<LevelGoal>();
        if (goal != null)
        {
            float s = length - GoalDistanceFromEnd;
            Undo.RecordObject(goal.transform, "Move goal");
            goal.transform.SetPositionAndRotation(OnTrail(trail, s, 0f, CorridorHeight(s)), Frame(trail, s));
        }

        Vector3 spawn = OnTrail(trail, SpawnDistance, 0f, 0.05f);
        Vector3 forward = trail.TangentAt(SpawnDistance);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            Undo.RecordObject(player.transform, "Move player to the trail start");
            player.transform.SetPositionAndRotation(spawn, Quaternion.LookRotation(forward, Vector3.up));
            if (controller != null) controller.enabled = wasEnabled;
        }

        // Start the cameras behind the player, where the follow camera will put them, so the first frame does not jump.
        Vector3 cameraPosition = spawn - forward * 6f + Vector3.up * 2.5f;
        Quaternion cameraRotation = Quaternion.LookRotation(spawn + Vector3.up * 1.2f - cameraPosition, Vector3.up);
        foreach (var name in new[] { "Main Camera", "CM Player Camera", "CM Side Camera" })
        {
            var cam = GameObject.Find(name);
            if (cam == null) continue;
            Undo.RecordObject(cam.transform, "Move camera");
            cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        }

        var orbit = Object.FindFirstObjectByType<CinemachineOrbitalFollow>();
        if (orbit != null)
        {
            Undo.RecordObject(orbit, "Face the trail");
            orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, Quaternion.LookRotation(forward).eulerAngles.y);
        }
    }

    private static void UpdateLevelConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelConfigPath);
        if (config == null || Mathf.Approximately(config.killPlaneY, KillPlaneY)) return;

        Undo.RecordObject(config, "Kill plane");
        config.killPlaneY = KillPlaneY;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);
    }

    // ================================================================== scenery

    private static void BuildScenery(Transform root, ForestTrail trail, Collider floor, List<Vector2> reserved)
    {
        float length = trail.Length;
        var rng = new Random(7);
        var raycastSet = new HashSet<Collider> { floor };

        var border = Child(root, "TreeLine");
        var far = Child(root, "FarForest");
        var inside = Child(root, "Corridor");
        var ground = Child(root, "GroundCover");

        // A dense line of trees just outside the walls hides them and frames the trail.
        var treePlaced = new List<Vector2>();
        Scatter(rng, border, TreeNames, Mathf.RoundToInt(length * 2f * 0.5f), trail, raycastSet, treePlaced, 2.2f, 0.9f, 1.4f,
            (s, side) => side * (HalfWidth(s, side, length) + 0.8f + Next(rng) * 6f), insideCorridor: false);
        Scatter(rng, border, BushNames, Mathf.RoundToInt(length * 2f * 0.5f), trail, raycastSet, new List<Vector2>(), 1.2f, 0.9f, 1.5f,
            (s, side) => side * (HalfWidth(s, side, length) + 0.3f + Next(rng) * 5f), insideCorridor: false);
        Scatter(rng, border, RockNames, Mathf.RoundToInt(length * 2f * 0.08f), trail, raycastSet, new List<Vector2>(treePlaced), 2.5f, 1f, 2f,
            (s, side) => side * (HalfWidth(s, side, length) + 1f + Next(rng) * 8f), insideCorridor: false);
        Scatter(rng, far, TreeNames, Mathf.RoundToInt(length * 2f * 0.35f), trail, raycastSet, treePlaced, 4f, 1.4f, 2.4f,
            (s, side) => side * (HalfWidth(s, side, length) + 7f + Next(rng) * 18f), insideCorridor: false);

        // Behind the start wall (clear of where the camera starts) and past the goal, the forest closes the view.
        foreach (var (from, to) in new[] { (-EndExtension, -8f), (length + 4f, length + EndExtension) })
            Scatter(rng, border, TreeNames, 14, trail, raycastSet, treePlaced, 2.4f, 1f, 1.6f,
                (s, side) => side * Next(rng) * (CorridorHalfWidth + 8f), insideCorridor: false, from, to);

        // Inside the corridor: a few solid trees in the clearings, bushes and low plants off the path.
        var corridorPlaced = new List<Vector2>(reserved);
        foreach (var c in Clearings)
        {
            for (int i = 0; i < 3; i++)
            {
                float s = c.s + (Next(rng) - 0.5f) * (c.length - 6f);
                float lateral = c.side * (TrailHalfWidth + 3f + Next(rng) * (HalfWidth(s, c.side, length) - TrailHalfWidth - 4f));
                var tree = PlaceScenery(rng, inside, TreeNames, trail, s, lateral, raycastSet, corridorPlaced, 3f, 0.9f, 1.3f, true);
                if (tree != null) AddTrunkCollider(tree);
            }
        }
        Scatter(rng, inside, BushNames, Mathf.RoundToInt(length * 0.25f), trail, raycastSet, corridorPlaced, 2.5f, 0.8f, 1.2f,
            (s, side) => side * (TrailHalfWidth + 1.2f + Next(rng) * (HalfWidth(s, side, length) - TrailHalfWidth - 1.6f)), insideCorridor: true);
        Scatter(rng, ground, SmallFoliage, Mathf.RoundToInt(length * 1.2f), trail, raycastSet, new List<Vector2>(reserved), 0.8f, 0.9f, 1.4f,
            (s, side) => side * (TrailHalfWidth + 0.2f + Next(rng) * (HalfWidth(s, side, length) + 2f)), insideCorridor: false);
    }

    private static float Next(Random rng) => (float)rng.NextDouble();

    private delegate float LateralPicker(float s, int side);

    private static void Scatter(Random rng, Transform parent, string[] names, int count, ForestTrail trail, HashSet<Collider> raycastSet,
        List<Vector2> placed, float minDistance, float scaleMin, float scaleMax, LateralPicker pickLateral, bool insideCorridor)
        => Scatter(rng, parent, names, count, trail, raycastSet, placed, minDistance, scaleMin, scaleMax, pickLateral, insideCorridor, 0f, trail.Length);

    private static void Scatter(Random rng, Transform parent, string[] names, int count, ForestTrail trail, HashSet<Collider> raycastSet,
        List<Vector2> placed, float minDistance, float scaleMin, float scaleMax, LateralPicker pickLateral, bool insideCorridor, float sMin, float sMax)
    {
        int made = 0, attempts = 0;
        while (made < count && attempts < count * 20)
        {
            attempts++;
            float s = Mathf.Lerp(sMin, sMax, Next(rng));
            int side = rng.Next(2) == 0 ? -1 : 1;
            float lateral = pickLateral(s, side);
            if (insideCorridor && Mathf.Abs(lateral) < TrailHalfWidth + 0.5f) continue;
            if (PlaceScenery(rng, parent, names, trail, s, lateral, raycastSet, placed, minDistance, scaleMin, scaleMax, false) != null) made++;
        }
    }

    private static GameObject PlaceScenery(Random rng, Transform parent, string[] names, ForestTrail trail, float s, float lateral,
        HashSet<Collider> raycastSet, List<Vector2> placed, float minDistance, float scaleMin, float scaleMax, bool keepOffPath)
    {
        Vector3 p = OnTrail(trail, s, lateral, 0f);
        var point = new Vector2(p.x, p.z);
        foreach (var other in placed)
            if ((other - point).sqrMagnitude < minDistance * minDistance) return null;

        // Trees and bushes must never stand on the dirt path, wherever another stretch of trail passes nearby.
        trail.Project(p, out float fromCentre);
        if (Mathf.Abs(fromCentre) < TrailHalfWidth + (keepOffPath ? 1.5f : 0.3f)) return null;
        if (!TryGroundY(p.x, p.z, raycastSet, out float y)) return null; // Over a stream.

        var go = Spawn(Prefab("Scenery_" + names[rng.Next(names.Length)], "Scenery"), parent);
        go.transform.position = new Vector3(p.x, y - 0.03f, p.z);
        go.transform.rotation = Quaternion.Euler(0f, Next(rng) * 360f, 0f);
        go.transform.localScale = Vector3.one * Mathf.Lerp(scaleMin, scaleMax, Next(rng));
        MakeStatic(go);
        placed.Add(point);
        return go;
    }

    // Trees the player can reach must be solid. The trunk collider sits on a child of the instance; the scenery
    // prefabs themselves stay collider-free (EnvironmentPrefabPolicyTests).
    private static void AddTrunkCollider(GameObject tree)
    {
        var trunk = new GameObject("Trunk");
        trunk.transform.SetParent(tree.transform, false);
        var capsule = trunk.AddComponent<CapsuleCollider>();
        capsule.radius = 0.35f;
        capsule.height = 3f;
        capsule.center = new Vector3(0f, 1.5f, 0f);
        MakeStatic(trunk);
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
        return found && y > -0.5f; // Ignore the slopes that fall away at the outer edge.
    }

    private static void BuildFarAway(Transform root, ForestTrail trail)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (float s = 0f; s <= trail.Length; s += 5f)
        {
            var p = trail.PointAt(s);
            min = Vector2.Min(min, new Vector2(p.x, p.z));
            max = Vector2.Max(max, new Vector2(p.x, p.z));
        }
        Vector2 center = (min + max) / 2f, size = max - min;

        // A flat base far below the trail ground, where the outer slopes end, so the view never shows the sky below.
        var baseGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseGround.name = "BaseGround";
        baseGround.transform.SetParent(root, false);
        Object.DestroyImmediate(baseGround.GetComponent<BoxCollider>());
        baseGround.transform.position = new Vector3(center.x, FalloffDepth - 0.52f, center.y);
        baseGround.transform.localScale = new Vector3(size.x + 260f, 1f, size.y + 260f);
        baseGround.GetComponent<MeshRenderer>().sharedMaterial = GrassMaterial();
        MakeStatic(baseGround);

        // Mountains: the tall hill tile stretched wide and high, in a ring around the whole level.
        var mountain = Prefab("Terrain_Ground_02", "Terrain");
        var rng = new Random(11);
        const int count = 22;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            var go = Spawn(mountain, root);
            go.transform.rotation = Quaternion.Euler(0f, 90f * rng.Next(4), 0f);
            go.transform.localScale = new Vector3(1.8f, 2.2f, 1.8f);
            go.transform.position = Vector3.zero;
            var b = RendererBounds(go);
            float x = center.x + Mathf.Cos(angle) * (size.x / 2f + 75f);
            float z = center.y + Mathf.Sin(angle) * (size.y / 2f + 75f);
            go.transform.position = new Vector3(x - b.center.x, FalloffDepth - b.min.y, z - b.center.z);
            MakeStatic(go);
        }
    }

    // ================================================================== helpers

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

    private static Material LaneMaterial() => FlatMaterial(LaneMaterialPath, "Lane_Path", new Color(0.50f, 0.38f, 0.25f), 0f);
    private static Material GrassMaterial() => FlatMaterial(GrassMaterialPath, "Terrain_Base", new Color(0.37f, 0.55f, 0.32f), 0f);

    private static Material FlatMaterial(string path, string name, Color color, float smoothness)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Writes the mesh into the asset at path, reusing the existing asset (and its GUID) when there is one.
    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder("Assets", "Generated");

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        existing.Clear();
        existing.indexFormat = mesh.indexFormat;
        existing.vertices = mesh.vertices;
        existing.normals = mesh.normals;
        existing.subMeshCount = mesh.subMeshCount;
        for (int i = 0; i < mesh.subMeshCount; i++) existing.SetTriangles(mesh.GetTriangles(i), i);
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssetIfDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    /// <summary>Flat-shaded quads with one submesh per material.</summary>
    private class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<int>[] triangles;

        public MeshBuilder(int submeshes)
        {
            triangles = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) triangles[i] = new List<int>();
        }

        /// <summary>Adds the quad a-b-c-d. Its front faces the side from which a, b, c read clockwise.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int submesh, bool doubleSided)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-10f) normal = Vector3.Cross(c - a, d - a);
            if (normal.sqrMagnitude < 1e-10f) return; // Degenerate (zero-height wall).
            normal.Normalize();

            Add(a, b, c, d, normal, submesh);
            if (doubleSided) Add(a, d, c, b, -normal, submesh);
        }

        private void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int submesh)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int k = 0; k < 4; k++) normals.Add(normal);
            triangles[submesh].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
