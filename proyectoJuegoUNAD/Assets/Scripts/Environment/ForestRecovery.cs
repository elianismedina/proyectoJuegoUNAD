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
/// </summary>
public class ForestRecovery : MonoBehaviour
{
    [SerializeField, Tooltip("Parent of the scenery whose look changes (trees, bushes, grass, flowers).")]
    private Transform sceneryRoot;

    [SerializeField, Tooltip("The scenery's normal material; renderers using it are the ones that change.")]
    private Material healthyMaterial;

    [SerializeField, Tooltip("What that scenery looks like while its zone is polluted.")]
    private Material pollutedMaterial;

    /// <summary>Raised with the zone index when a zone is recovered.</summary>
    public static event Action<int> ZoneRecovered;

    /// <summary>Raised with the forest's overall recovery (0 to 1) whenever it changes, and once when the level starts.</summary>
    public static event Action<float> ProgressChanged;

    /// <summary>The forest of the loaded level, or null.</summary>
    public static ForestRecovery Current { get; private set; }

    private readonly Dictionary<Collectible, int> zoneOfWaste = new Dictionary<Collectible, int>();
    private List<Renderer>[] zoneRenderers;
    private GameSession session;

    public ZoneRecoveryTracker Tracker { get; private set; }

    /// <summary>The forest's overall recovery, 0 to 1 (0 until the level has started).</summary>
    public float Progress => Tracker != null ? Tracker.OverallProgress : 0f;

    public Material HealthyMaterial => healthyMaterial;
    public Material PollutedMaterial => pollutedMaterial;

    public IReadOnlyList<Renderer> RenderersInZone(int zone) => zoneRenderers[zone];

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

        for (int z = 0; z < zoneRenderers.Length; z++)
            if (!Tracker.IsRecovered(z)) SetLook(z, healthyMaterial, pollutedMaterial);

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
        ZoneRecovered?.Invoke(zone);
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
