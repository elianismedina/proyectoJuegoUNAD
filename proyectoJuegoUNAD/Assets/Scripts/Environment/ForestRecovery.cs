using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows the forest going from polluted to healthy, zone by zone (GDD §6.1.4, §8.6, §9.1). At the start of the level it
/// counts the waste of each zone (by <see cref="ForestTrail.ZoneAt"/>) and dresses that zone's scenery in
/// <see cref="pollutedMaterial"/>; when the last waste item of a zone is collected the zone is recovered and its scenery
/// goes back to <see cref="healthyMaterial"/>. Winning the level ("¡Bosque limpio!") recovers every zone.
/// The rule itself is the pure <see cref="ZoneRecoveryTracker"/>. The overall progress fills the "Recuperación del bosque"
/// bar of the <see cref="WasteCounter"/>.
/// The look is a simple shared-material swap (placeholder art): any renderer under <see cref="sceneryRoot"/> that uses
/// <see cref="healthyMaterial"/> is swapped. To change the art, assign other materials here, or give the polluted state its
/// own material per asset later. <c>LevelEnvironmentBuilder</c> adds this to the <c>Environment</c> root.
/// The flowers under <see cref="recoveryPropsRoot"/> (Low Poly Flower Pack, placed by the builder) are hidden while their
/// zone is polluted and grow in, one after another, when it is recovered (GDD §6.1.4, "aparición de vegetación").
/// The animals under <see cref="animalsRoot"/> (<see cref="RecoveryAnimal"/>, Animals FREE pack) stay inactive while
/// their zone is polluted and are switched on, growing in the same way, when it is recovered.
/// </summary>
public class ForestRecovery : MonoBehaviour
{
    [SerializeField, Tooltip("Parent of the scenery whose look changes (trees, bushes, grass, flowers).")]
    private Transform sceneryRoot;

    [SerializeField, Tooltip("The scenery's normal material; renderers using it are the ones that change.")]
    private Material healthyMaterial;

    [SerializeField, Tooltip("What that scenery looks like while its zone is polluted.")]
    private Material pollutedMaterial;

    [SerializeField, Tooltip("Parent of the flowers that appear when a zone is recovered (each child is one flower).")]
    private Transform recoveryPropsRoot;

    [SerializeField, Tooltip("Parent of the animals that appear when a zone is recovered (one child per zone, each holding its animals).")]
    private Transform animalsRoot;

    [SerializeField, Min(0.05f), Tooltip("Seconds one flower takes to grow in.")]
    private float growSeconds = 0.6f;

    [SerializeField, Min(0f), Tooltip("Flowers start growing at random within this many seconds, so the zone blooms in a wave.")]
    private float growSpread = 1f;

    /// <summary>Raised with the zone index when a zone is recovered.</summary>
    public static event Action<int> ZoneRecovered;

    /// <summary>Raised with the forest's overall recovery (0 to 1) whenever it changes, and once when the level starts.</summary>
    public static event Action<float> ProgressChanged;

    /// <summary>The forest of the loaded level, or null.</summary>
    public static ForestRecovery Current { get; private set; }

    private readonly Dictionary<Collectible, int> zoneOfWaste = new Dictionary<Collectible, int>();
    private List<Renderer>[] zoneRenderers;
    private List<Transform>[] zoneProps;
    private List<RecoveryAnimal>[] zoneAnimals;
    private readonly Dictionary<Transform, Vector3> fullScale = new Dictionary<Transform, Vector3>();
    private readonly List<(Transform prop, float start)> growing = new List<(Transform, float)>();
    private GameSession session;

    public ZoneRecoveryTracker Tracker { get; private set; }

    /// <summary>The forest's overall recovery, 0 to 1 (0 until the level has started).</summary>
    public float Progress => Tracker != null ? Tracker.OverallProgress : 0f;

    public Material HealthyMaterial => healthyMaterial;
    public Material PollutedMaterial => pollutedMaterial;

    public IReadOnlyList<Renderer> RenderersInZone(int zone) => zoneRenderers[zone];

    /// <summary>The flowers that bloom when the zone is recovered.</summary>
    public IReadOnlyList<Transform> FlowersInZone(int zone) => zoneProps[zone];

    /// <summary>True when every flower of the zone has finished growing (also for a zone with no flowers).</summary>
    public bool FlowersBloomed(int zone)
    {
        foreach (var prop in zoneProps[zone])
            if (prop.localScale != fullScale[prop]) return false;
        return true;
    }

    /// <summary>True while none of the zone's flowers shows yet.</summary>
    public bool FlowersHidden(int zone)
    {
        foreach (var prop in zoneProps[zone])
            if (prop.localScale != Vector3.zero) return false;
        return true;
    }

    /// <summary>The animals that appear when the zone is recovered.</summary>
    public IReadOnlyList<RecoveryAnimal> AnimalsInZone(int zone) => zoneAnimals[zone];

    /// <summary>True when every animal of the zone is out and fully grown in.</summary>
    public bool AnimalsShown(int zone)
    {
        foreach (var animal in zoneAnimals[zone])
            if (!animal.gameObject.activeSelf || animal.transform.localScale != fullScale[animal.transform]) return false;
        return true;
    }

    /// <summary>True while none of the zone's animals is out.</summary>
    public bool AnimalsHidden(int zone)
    {
        foreach (var animal in zoneAnimals[zone])
            if (animal.gameObject.activeSelf) return false;
        return true;
    }

    private void Awake() => Current = this;

    private void OnEnable() => Collectible.Collected += OnCollected;

    private void OnDisable() => Collectible.Collected -= OnCollected;

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (session != null) session.StateChanged -= OnStateChanged;
    }

    private void Start()
    {
        var trail = FindAnyObjectByType<ForestTrail>();
        if (trail == null)
        {
            enabled = false;
            return;
        }

        var wastePerZone = new int[ForestTrail.ZoneCount];
        foreach (var item in FindObjectsByType<Collectible>())
        {
            if (item.IsCollected) continue;
            int zone = trail.ZoneAt(trail.Project(item.transform.position, out _));
            zoneOfWaste[item] = zone;
            wastePerZone[zone]++;
        }
        Tracker = new ZoneRecoveryTracker(wastePerZone);
        Tracker.ZoneRecovered += OnZoneRecovered;

        zoneRenderers = new List<Renderer>[ForestTrail.ZoneCount];
        for (int z = 0; z < zoneRenderers.Length; z++) zoneRenderers[z] = new List<Renderer>();
        if (sceneryRoot != null && healthyMaterial != null)
        {
            foreach (var r in sceneryRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (Array.IndexOf(r.sharedMaterials, healthyMaterial) < 0) continue;
                zoneRenderers[trail.ZoneAt(trail.Project(r.bounds.center, out _))].Add(r);
            }
        }

        zoneProps = new List<Transform>[ForestTrail.ZoneCount];
        for (int z = 0; z < zoneProps.Length; z++) zoneProps[z] = new List<Transform>();
        if (recoveryPropsRoot != null)
        {
            foreach (Transform group in recoveryPropsRoot)
            foreach (Transform prop in group)
            {
                zoneProps[trail.ZoneAt(trail.Project(prop.position, out _))].Add(prop);
                fullScale[prop] = prop.localScale;
            }
        }

        zoneAnimals = new List<RecoveryAnimal>[ForestTrail.ZoneCount];
        for (int z = 0; z < zoneAnimals.Length; z++) zoneAnimals[z] = new List<RecoveryAnimal>();
        if (animalsRoot != null)
        {
            foreach (var animal in animalsRoot.GetComponentsInChildren<RecoveryAnimal>(true))
            {
                zoneAnimals[trail.ZoneAt(trail.Project(animal.PointA, out _))].Add(animal);
                fullScale[animal.transform] = animal.transform.localScale;
            }
        }

        for (int z = 0; z < zoneRenderers.Length; z++)
        {
            if (Tracker.IsRecovered(z)) continue;
            SetLook(z, healthyMaterial, pollutedMaterial);
            foreach (var prop in zoneProps[z]) prop.localScale = Vector3.zero;
            foreach (var animal in zoneAnimals[z]) animal.gameObject.SetActive(false);
        }

        if (GameManager.Instance != null)
        {
            session = GameManager.Instance.Session;
            session.StateChanged += OnStateChanged;
        }

        ProgressChanged?.Invoke(Progress);
    }

    /// <summary>True while the zone's scenery wears the polluted material.</summary>
    public bool LooksPolluted(int zone)
    {
        foreach (var r in zoneRenderers[zone])
            if (Array.IndexOf(r.sharedMaterials, pollutedMaterial) >= 0) return true;
        return false;
    }

    private void OnCollected(Collectible item)
    {
        if (Tracker == null || !zoneOfWaste.TryGetValue(item, out int zone)) return;
        Tracker.Collect(zone);
        ProgressChanged?.Invoke(Progress);
    }

    private void OnStateChanged(GameState state)
    {
        if (state != GameState.Won || Tracker.AllRecovered) return;
        Tracker.RecoverAll();
        ProgressChanged?.Invoke(Progress);
    }

    private void OnZoneRecovered(int zone)
    {
        SetLook(zone, pollutedMaterial, healthyMaterial);
        foreach (var prop in zoneProps[zone]) growing.Add((prop, Time.time + UnityEngine.Random.value * growSpread));
        foreach (var animal in zoneAnimals[zone])
        {
            if (animal.gameObject.activeSelf) continue;
            animal.transform.localScale = Vector3.zero;
            animal.gameObject.SetActive(true);
            growing.Add((animal.transform, Time.time + UnityEngine.Random.value * growSpread));
        }
        ZoneRecovered?.Invoke(zone);
    }

    // Game time, so a pause holds the bloom. Each flower pops up with a small overshoot, then settles.
    private void Update()
    {
        for (int i = growing.Count - 1; i >= 0; i--)
        {
            var (prop, start) = growing[i];
            float t = (Time.time - start) / growSeconds;
            if (t < 0f) continue;
            if (prop == null || t >= 1f)
            {
                if (prop != null) prop.localScale = fullScale[prop];
                growing.RemoveAt(i);
                continue;
            }
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float overshoot = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
            prop.localScale = fullScale[prop] * (eased * overshoot);
        }
    }

    // Swaps one shared material for another on the zone's scenery (shared materials keep the SRP Batcher happy).
    private void SetLook(int zone, Material from, Material to)
    {
        if (from == null || to == null || from == to) return;
        foreach (var r in zoneRenderers[zone])
        {
            var materials = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != from) continue;
                materials[i] = to;
                changed = true;
            }
            if (changed) r.sharedMaterials = materials;
        }
    }
}
