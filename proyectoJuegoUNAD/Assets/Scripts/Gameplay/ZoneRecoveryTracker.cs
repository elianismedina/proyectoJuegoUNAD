using System;

/// <summary>
/// Pure rule for the forest's recovery (GDD §6.1.4, §8.6): each zone holds some waste; when the last of it is collected
/// the zone is recovered. A zone with no waste counts as recovered from the start. No Unity types, so it is unit-tested
/// on its own; <see cref="ForestRecovery"/> drives it in the scene.
/// </summary>
public class ZoneRecoveryTracker
{
    private readonly int[] total;
    private readonly int[] collected;
    private readonly bool[] recovered;

    /// <summary>Raised with the zone index when a zone becomes recovered (once per zone).</summary>
    public event Action<int> ZoneRecovered;

    public ZoneRecoveryTracker(int[] wastePerZone)
    {
        if (wastePerZone == null || wastePerZone.Length == 0) throw new ArgumentException("Need at least one zone.", nameof(wastePerZone));
        total = (int[])wastePerZone.Clone();
        collected = new int[total.Length];
        recovered = new bool[total.Length];
        for (int z = 0; z < total.Length; z++)
        {
            if (total[z] < 0) throw new ArgumentException("Waste counts cannot be negative.", nameof(wastePerZone));
            recovered[z] = total[z] == 0;
        }
    }

    public int ZoneCount => total.Length;

    public int WasteInZone(int zone) => total[zone];
    public int CollectedInZone(int zone) => collected[zone];
    public bool IsRecovered(int zone) => recovered[zone];

    /// <summary>Share of the zone's waste collected, 0 to 1 (1 for a zone with no waste).</summary>
    public float Progress(int zone) => total[zone] == 0 ? 1f : (float)collected[zone] / total[zone];

    /// <summary>The whole forest's recovery, 0 to 1: the average of the zones' progress.</summary>
    public float OverallProgress
    {
        get
        {
            float sum = 0f;
            for (int z = 0; z < total.Length; z++) sum += Progress(z);
            return sum / total.Length;
        }
    }

    public bool AllRecovered
    {
        get
        {
            foreach (bool r in recovered) if (!r) return false;
            return true;
        }
    }

    /// <summary>Counts one collected item in <paramref name="zone"/>. Returns true if it was the zone's last one.</summary>
    public bool Collect(int zone)
    {
        if (zone < 0 || zone >= total.Length || collected[zone] >= total[zone]) return false;

        collected[zone]++;
        if (collected[zone] < total[zone]) return false;

        Recover(zone);
        return true;
    }

    /// <summary>Marks every zone recovered (the level was won: "¡Bosque limpio!"), raising the event for each one still polluted.</summary>
    public void RecoverAll()
    {
        for (int z = 0; z < total.Length; z++)
        {
            collected[z] = total[z];
            if (!recovered[z]) Recover(z);
        }
    }

    private void Recover(int zone)
    {
        recovered[zone] = true;
        ZoneRecovered?.Invoke(zone);
    }
}
