using System;
using System.Collections.Generic;
using NUnit.Framework;

// The forest's recovery rule (GDD §6.1.4): a zone is recovered when its last waste item is collected.
public class ZoneRecoveryTrackerTests
{
    [Test]
    public void ZonesStartPolluted_ExceptOnesWithNoWaste()
    {
        var tracker = new ZoneRecoveryTracker(new[] { 3, 0, 5 });
        Assert.IsFalse(tracker.IsRecovered(0));
        Assert.IsTrue(tracker.IsRecovered(1), "A zone with nothing to collect counts as recovered.");
        Assert.IsFalse(tracker.IsRecovered(2));
        Assert.AreEqual(0f, tracker.Progress(0));
        Assert.AreEqual(1f, tracker.Progress(1));
        Assert.IsFalse(tracker.AllRecovered);
    }

    [Test]
    public void CollectingTheLastItemOfAZone_RecoversItOnce()
    {
        var tracker = new ZoneRecoveryTracker(new[] { 3, 4, 5 });
        var recovered = new List<int>();
        tracker.ZoneRecovered += recovered.Add;

        Assert.IsFalse(tracker.Collect(0));
        Assert.IsFalse(tracker.Collect(0));
        Assert.IsFalse(tracker.IsRecovered(0), "Two of three is not recovered yet.");
        Assert.AreEqual(2f / 3f, tracker.Progress(0), 1e-5f);

        Assert.IsTrue(tracker.Collect(0), "The third item recovers Zone 1.");
        Assert.IsTrue(tracker.IsRecovered(0));
        Assert.IsFalse(tracker.IsRecovered(1), "Other zones are untouched.");
        CollectionAssert.AreEqual(new[] { 0 }, recovered);

        Assert.IsFalse(tracker.Collect(0), "Nothing left to collect in a recovered zone.");
        Assert.IsFalse(tracker.Collect(7), "Unknown zones are ignored.");
        CollectionAssert.AreEqual(new[] { 0 }, recovered, "The event fires once per zone.");
    }

    [Test]
    public void OverallProgress_IsTheAverageOfTheZones()
    {
        var tracker = new ZoneRecoveryTracker(new[] { 3, 4, 5 });
        Assert.AreEqual(0f, tracker.OverallProgress);

        for (int i = 0; i < 3; i++) tracker.Collect(0);
        Assert.AreEqual(1f / 3f, tracker.OverallProgress, 1e-5f, "One zone of three recovered.");

        tracker.Collect(1);
        tracker.Collect(1);
        Assert.AreEqual((1f + 0.5f + 0f) / 3f, tracker.OverallProgress, 1e-5f);
    }

    [Test]
    public void RecoverAll_RecoversEveryZoneStillPolluted()
    {
        var tracker = new ZoneRecoveryTracker(new[] { 1, 2, 3 });
        tracker.Collect(0);
        var recovered = new List<int>();
        tracker.ZoneRecovered += recovered.Add;

        tracker.RecoverAll();

        Assert.IsTrue(tracker.AllRecovered);
        Assert.AreEqual(1f, tracker.OverallProgress);
        CollectionAssert.AreEqual(new[] { 1, 2 }, recovered, "Zone 1 was already recovered.");
    }

    [Test]
    public void BadZoneCounts_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new ZoneRecoveryTracker(new int[0]));
        Assert.Throws<ArgumentException>(() => new ZoneRecoveryTracker(null));
        Assert.Throws<ArgumentException>(() => new ZoneRecoveryTracker(new[] { 2, -1 }));
    }
}
