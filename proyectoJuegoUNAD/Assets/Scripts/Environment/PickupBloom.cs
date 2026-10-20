using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every waste item picked up leaves a few flowers growing where it was (GDD §6.1.4, "aparición de vegetación"): on
/// <see cref="Collectible.Collected"/> it spawns <see cref="flowersPerPickup"/> flowers from <see cref="flowerPrefabs"/>
/// (the Low Poly Flower Pack variants, no colliders) on the forest floor below the item and grows them in with a short
/// pop. They never stand on the dirt path: for an item above the path they sprout on both edges of it, and for one over
/// a stream on the nearest bank. Each sprouting plays <see cref="bloomClip"/> (<c>flowers_sound</c>) once, in 2D.
/// <c>LevelEnvironmentBuilder</c> adds this to the <c>Environment</c> root.
/// </summary>
public class PickupBloom : MonoBehaviour
{
    [SerializeField, Tooltip("Flowers to pick from (no colliders).")]
    private GameObject[] flowerPrefabs;

    [SerializeField, Range(1, 12)] private int flowersPerPickup = 5;

    [SerializeField, Min(0.1f), Tooltip("How far the flowers spread around each sprouting spot, in metres.")]
    private float spread = 0.6f;

    [SerializeField, Min(0.05f), Tooltip("Seconds one flower takes to grow.")]
    private float growSeconds = 0.5f;

    [SerializeField, Min(0f), Tooltip("Delay between one flower and the next, so they pop up one after another.")]
    private float stagger = 0.08f;

    [SerializeField, Tooltip("Played once each time flowers sprout (Assets/Sound/SFX/flowers_sound.wav).")]
    private AudioClip bloomClip;

    [SerializeField, Range(0f, 1f)] private float bloomVolume = 0.8f;

    /// <summary>Half-width of the dirt path the flowers keep off (ForestTrail's path is 4 m wide), plus a margin.</summary>
    public const float PathClearance = 2.4f;

    private const float BankOffset = 0.7f; // how far from a stream's edge flowers sprout on the bank

    private ForestTrail trail;
    private Transform container;
    private AudioSource audioSource;
    private readonly List<(Transform flower, Vector3 scale, float start)> growing = new List<(Transform, Vector3, float)>();
    private readonly List<Transform> spawned = new List<Transform>();

    /// <summary>Every flower sprouted so far (for tests).</summary>
    public IReadOnlyList<Transform> Spawned => spawned;

    /// <summary>True while some flower is still growing.</summary>
    public bool IsGrowing => growing.Count > 0;

    public AudioClip BloomClip => bloomClip;

    /// <summary>How many times the bloom sound played since the level loaded.</summary>
    public int BloomSoundsPlayed { get; private set; }

    private void OnEnable() => Collectible.Collected += OnCollected;

    private void OnDisable() => Collectible.Collected -= OnCollected;

    private void Start()
    {
        trail = FindAnyObjectByType<ForestTrail>();
        container = new GameObject("PickupFlowers").transform;
        container.SetParent(transform, false);

        audioSource = container.gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D, like the rest of the level's one-shots (LevelAudio)
    }

    private void OnCollected(Collectible item)
    {
        if (flowerPrefabs == null || flowerPrefabs.Length == 0 || container == null) return;

        var spots = SproutSpots(item.transform.position);
        int sprouted = 0;
        for (int i = 0; i < flowersPerPickup; i++)
        {
            Vector3 centre = spots[i % spots.Count];
            Vector2 jitter = Random.insideUnitCircle * spread;
            Vector3 p = KeepOffPath(centre + new Vector3(jitter.x, 0f, jitter.y));
            if (!GroundBelow(p, out float y)) continue;

            var prefab = flowerPrefabs[Random.Range(0, flowerPrefabs.Length)];
            var flower = Instantiate(prefab, new Vector3(p.x, y - 0.02f, p.z), Quaternion.Euler(0f, Random.value * 360f, 0f), container).transform;
            Vector3 scale = flower.localScale * Random.Range(1.1f, 1.5f);
            flower.localScale = Vector3.zero;
            growing.Add((flower, scale, Time.time + i * stagger));
            spawned.Add(flower);
            sprouted++;
        }

        if (sprouted > 0 && bloomClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(bloomClip, bloomVolume);
            BloomSoundsPlayed++;
        }
    }

    // Where to sprout: below the item, or, if that is on the path or over a stream, beside the path / on the bank.
    private List<Vector3> SproutSpots(Vector3 itemPosition)
    {
        var spots = new List<Vector3>();
        if (trail == null)
        {
            spots.Add(itemPosition);
            return spots;
        }

        float s = trail.Project(itemPosition, out float lateral);
        foreach (var span in trail.StreamSpans)
        {
            if (s < span.x - BankOffset || s > span.y + BankOffset) continue;
            s = s - span.x < span.y - s ? span.x - BankOffset : span.y + BankOffset; // nearer bank
            break;
        }

        if (Mathf.Abs(lateral) >= PathClearance)
        {
            spots.Add(trail.PointAt(s) + trail.LeftAt(s) * lateral);
        }
        else
        {
            // Above the path: a patch on each edge, so the trail stays clear.
            float edge = PathClearance + spread;
            spots.Add(trail.PointAt(s) + trail.LeftAt(s) * edge);
            spots.Add(trail.PointAt(s) - trail.LeftAt(s) * edge);
        }
        return spots;
    }

    private Vector3 KeepOffPath(Vector3 p)
    {
        if (trail == null) return p;
        float s = trail.Project(p, out float lateral);
        if (Mathf.Abs(lateral) >= PathClearance) return p;
        float side = lateral >= 0f ? 1f : -1f;
        Vector3 q = trail.PointAt(s) + trail.LeftAt(s) * (side * (PathClearance + 0.1f));
        return new Vector3(q.x, p.y, q.z);
    }

    // The forest floor only (not a rock, log or the player): flowers grow from the ground.
    private static bool GroundBelow(Vector3 p, out float y)
    {
        y = 0f;
        float best = float.MinValue;
        foreach (var hit in Physics.RaycastAll(new Vector3(p.x, 20f, p.z), Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.name != "ForestFloor" || hit.point.y <= best) continue;
            best = hit.point.y;
        }
        if (best == float.MinValue || best < -0.5f) return false; // nothing, or a stream bed
        y = best;
        return true;
    }

    // Game time, so pausing holds the bloom. Ease out with a small overshoot, then settle.
    private void Update()
    {
        for (int i = growing.Count - 1; i >= 0; i--)
        {
            var (flower, scale, start) = growing[i];
            float t = (Time.time - start) / growSeconds;
            if (t < 0f) continue;
            if (flower == null || t >= 1f)
            {
                if (flower != null) flower.localScale = scale;
                growing.RemoveAt(i);
                continue;
            }
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            flower.localScale = scale * (eased * (1f + 0.2f * Mathf.Sin(t * Mathf.PI)));
        }
    }
}
