using UnityEngine;

/// <summary>
/// The centre line of the forest trail of a level, as a polyline sampled about every metre, plus the spans
/// of trail that streams cut through. Written by the level builder (Editor) and read at runtime by anything
/// that needs to know "how far along the trail" a point is: tests, zone logic and the navigation hint.
/// Distances along the trail are in metres from the start (s = 0) to the goal (s = <see cref="Length"/>).
/// </summary>
public class ForestTrail : MonoBehaviour
{
    public const int ZoneCount = 3;

    [SerializeField, HideInInspector] private Vector3[] points = new Vector3[0];
    [SerializeField, HideInInspector] private float[] distances = new float[0];

    [Tooltip("Trail distance ranges (x = start, y = end) where a stream crosses the trail and there is no ground.")]
    [SerializeField] private Vector2[] streamSpans = new Vector2[0];

    public float Length => distances.Length > 0 ? distances[distances.Length - 1] : 0f;
    public Vector2[] StreamSpans => streamSpans;

    /// <summary>Stores a new centre line; the points must be in order from the start to the goal.</summary>
    public void SetPath(Vector3[] newPoints, Vector2[] newStreamSpans)
    {
        points = newPoints;
        distances = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
            distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        streamSpans = newStreamSpans;
    }

    /// <summary>Point on the centre line at trail distance <paramref name="s"/> (clamped to the trail).</summary>
    public Vector3 PointAt(float s)
    {
        int i = SegmentAt(s, out float t);
        return Vector3.Lerp(points[i], points[i + 1], t);
    }

    /// <summary>Unit direction of travel on the ground plane at trail distance <paramref name="s"/>.</summary>
    public Vector3 TangentAt(float s)
    {
        int i = SegmentAt(s, out _);
        Vector3 d = points[i + 1] - points[i];
        d.y = 0f;
        return d.normalized;
    }

    /// <summary>Unit vector pointing to the left of the direction of travel.</summary>
    public Vector3 LeftAt(float s)
    {
        Vector3 t = TangentAt(s);
        return new Vector3(-t.z, 0f, t.x);
    }

    /// <summary>
    /// Projects <paramref name="position"/> onto the centre line (ignoring height). Returns the trail distance of
    /// the closest point; <paramref name="lateral"/> is the signed sideways distance (positive = left of travel).
    /// </summary>
    public float Project(Vector3 position, out float lateral)
    {
        Vector2 p = new Vector2(position.x, position.z);
        float bestSqr = float.MaxValue, bestS = 0f;
        lateral = 0f;

        for (int i = 0; i + 1 < points.Length; i++)
        {
            Vector2 a = new Vector2(points[i].x, points[i].z);
            Vector2 b = new Vector2(points[i + 1].x, points[i + 1].z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            Vector2 closest = a + ab * t;
            float sqr = (p - closest).sqrMagnitude;
            if (sqr >= bestSqr) continue;

            bestSqr = sqr;
            bestS = distances[i] + (distances[i + 1] - distances[i]) * t;
            float cross = ab.x * (p.y - a.y) - ab.y * (p.x - a.x); // > 0 when p is to the left of a→b
            lateral = Mathf.Sign(cross) * Mathf.Sqrt(sqr);
        }
        return bestS;
    }

    /// <summary>True when trail distance <paramref name="s"/> lies over a stream.</summary>
    public bool IsOverStream(float s)
    {
        foreach (var span in streamSpans)
            if (s >= span.x && s <= span.y) return true;
        return false;
    }

    /// <summary>Zone index (0 Learning, 1 Development, 2 Challenge) of trail distance <paramref name="s"/>.</summary>
    public int ZoneAt(float s)
    {
        if (Length <= 0f) return 0;
        return Mathf.Clamp(Mathf.FloorToInt(s / Length * ZoneCount), 0, ZoneCount - 1);
    }

    private int SegmentAt(float s, out float t)
    {
        s = Mathf.Clamp(s, 0f, Length);
        int i = 0;
        while (i < distances.Length - 2 && distances[i + 1] < s) i++;
        float span = distances[i + 1] - distances[i];
        t = span > 0f ? (s - distances[i]) / span : 0f;
        return i;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        for (int i = 0; i + 1 < points.Length; i++)
            Gizmos.DrawLine(points[i] + Vector3.up * 0.1f, points[i + 1] + Vector3.up * 0.1f);
    }
}
