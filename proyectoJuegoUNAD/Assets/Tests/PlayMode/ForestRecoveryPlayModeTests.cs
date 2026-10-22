using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The forest's recovery in Level01: every zone starts polluted; collecting all of a zone's waste recovers that zone
/// (its scenery goes back to the healthy material), fills Jhon's "Recuperación del bosque" bar and shows "¡Zona recuperada!"
/// for about 2 s with a chime; winning recovers all without the notice. A recovered zone's flowers bloom and its animals
/// come out, animated, off the path.
/// </summary>
public class ForestRecoveryPlayModeTests
{
    private const string LevelScene = "Level01";

    private ForestRecovery forest;
    private ForestTrail trail;
    private WasteCounter counter;
    private GameObject player;
    private CharacterController characterController;
    private GameScreens screens;

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;

        forest = Object.FindFirstObjectByType<ForestRecovery>();
        trail = Object.FindFirstObjectByType<ForestTrail>();
        counter = Object.FindFirstObjectByType<WasteCounter>();
        player = GameObject.FindGameObjectWithTag("Player");
        characterController = player.GetComponent<CharacterController>();
        screens = Object.FindFirstObjectByType<GameScreens>();
        Assert.IsNotNull(forest, "Level01 needs a ForestRecovery (Forest Guardian > Level > Build Level01 Environment).");
        Assert.IsNotNull(forest.Tracker, "The forest must be set up once the level has started.");
    }

    private List<Collectible> WasteInZone(int zone)
    {
        var items = new List<Collectible>();
        foreach (var c in Object.FindObjectsByType<Collectible>())
            if (trail.ZoneAt(trail.Project(c.transform.position, out _)) == zone) items.Add(c);
        return items;
    }

    [Test]
    public void EveryZone_HasWasteAndSceneryAndStartsPolluted()
    {
        Assert.IsNotNull(forest.HealthyMaterial);
        Assert.IsNotNull(forest.PollutedMaterial);
        Assert.AreNotEqual(forest.HealthyMaterial, forest.PollutedMaterial);
        for (int zone = 0; zone < ForestTrail.ZoneCount; zone++)
        {
            Assert.Greater(forest.Tracker.WasteInZone(zone), 0, "Zone " + (zone + 1) + " needs waste to recover.");
            Assert.Greater(forest.RenderersInZone(zone).Count, 0, "Zone " + (zone + 1) + " needs scenery that changes.");
            Assert.IsTrue(forest.LooksPolluted(zone), "Zone " + (zone + 1) + " must start polluted.");
            Assert.IsFalse(forest.Tracker.IsRecovered(zone));
            Assert.GreaterOrEqual(forest.FlowersInZone(zone).Count, 30, "Zone " + (zone + 1) + " needs flowers to bloom when it recovers.");
            Assert.IsTrue(forest.FlowersHidden(zone), "Zone " + (zone + 1) + "'s flowers must not show while it is polluted.");
            Assert.GreaterOrEqual(forest.AnimalsInZone(zone).Count, 3, "Zone " + (zone + 1) + " needs a few animals to come out when it recovers.");
            Assert.IsTrue(forest.AnimalsHidden(zone), "Zone " + (zone + 1) + "'s animals must not show while it is polluted.");
        }
        Assert.AreEqual(0f, counter.BarFill, 0.001f, "The recovery bar starts empty.");
    }

    [UnityTest]
    public IEnumerator CollectingAllOfAZonesWaste_RecoversThatZoneOnlyAndFillsTheBar()
    {
        var recovered = new List<int>();
        System.Action<int> record = recovered.Add;
        ForestRecovery.ZoneRecovered += record;
        try
        {
            var zone1 = WasteInZone(0);
            Assert.AreEqual(forest.Tracker.WasteInZone(0), zone1.Count);
            for (int i = 0; i < zone1.Count; i++)
            {
                characterController.enabled = false;
                player.transform.position = zone1[i].transform.position;
                characterController.enabled = true;
                yield return new WaitForSeconds(0.15f);
                Assert.IsTrue(zone1[i].IsCollected);
                if (i < zone1.Count - 1) Assert.IsTrue(forest.LooksPolluted(0), "Zone 1 recovers only with its last item.");
            }

            Assert.IsTrue(forest.Tracker.IsRecovered(0));
            Assert.IsFalse(forest.LooksPolluted(0), "Zone 1's scenery must be healthy again.");
            Assert.IsTrue(forest.LooksPolluted(1) && forest.LooksPolluted(2), "The other zones stay polluted.");
            CollectionAssert.AreEqual(new[] { 0 }, recovered);
            Assert.AreEqual(forest.Progress, counter.BarFill, 0.001f, "Jhon's bar shows the forest's recovery.");
            Assert.AreEqual(1f / 3f, counter.BarFill, 0.001f, "One zone of three recovered.");

            Assert.IsTrue(screens.ZoneNoticeShown, "Recovering a zone must show the notice.");
            Assert.AreEqual(GameScreens.ZoneRecoveredMessage, screens.transform.Find("ZoneNotice/Text").GetComponent<UnityEngine.UI.Text>().text);
            Assert.AreEqual(1, screens.ZoneNoticeSoundsPlayed, "...with one short chime.");
            yield return new WaitForSecondsRealtime(2.3f);
            Assert.IsFalse(screens.ZoneNoticeShown, "The notice goes away after about 2 s.");

            Assert.IsTrue(forest.FlowersBloomed(0), "Zone 1's flowers must have grown in by now.");
            Assert.IsTrue(forest.FlowersHidden(1) && forest.FlowersHidden(2), "The other zones' flowers stay hidden.");
            Assert.IsTrue(forest.AnimalsShown(0), "Zone 1's animals must be out by now.");
            Assert.IsTrue(forest.AnimalsHidden(1) && forest.AnimalsHidden(2), "The other zones' animals stay hidden.");
        }
        finally
        {
            ForestRecovery.ZoneRecovered -= record;
        }
    }

    [UnityTest]
    public IEnumerator Winning_RecoversTheWholeForest()
    {
        var session = GameManager.Instance.Session;
        for (int i = 0; i < session.TargetWaste; i++) session.AddWaste();
        GameManager.Instance.ReachGoal();
        yield return null;

        Assert.AreEqual(GameState.Won, session.State);
        for (int zone = 0; zone < ForestTrail.ZoneCount; zone++)
            Assert.IsFalse(forest.LooksPolluted(zone), "¡Bosque limpio! Zone " + (zone + 1) + " must be healthy.");
        Assert.AreEqual(1f, counter.BarFill, 0.001f, "The bar is full when the forest is clean.");
        Assert.IsFalse(screens.ZoneNoticeShown, "The win screen says it already: no zone notice over it.");
        Assert.AreEqual(0, screens.ZoneNoticeSoundsPlayed);

        yield return new WaitForSeconds(2f);
        for (int zone = 0; zone < ForestTrail.ZoneCount; zone++)
        {
            Assert.IsTrue(forest.FlowersBloomed(zone), "¡Bosque limpio! Zone " + (zone + 1) + "'s flowers must bloom.");
            Assert.IsTrue(forest.AnimalsShown(zone), "¡Bosque limpio! Zone " + (zone + 1) + "'s animals must come out.");
        }
    }

    // Once out, each animal plays its animation: walkers pace their stretch (walk loop) without leaving it, the rest idle.
    [UnityTest]
    public IEnumerator Animals_AreAnimatedAndWalkersPaceTheirStretch()
    {
        forest.Tracker.RecoverAll();
        var walkers = new List<RecoveryAnimal>();
        var moved = new HashSet<RecoveryAnimal>();
        for (int zone = 0; zone < ForestTrail.ZoneCount; zone++)
            foreach (var animal in forest.AnimalsInZone(zone))
                if (animal.Walks) walkers.Add(animal);
        Assert.IsNotEmpty(walkers, "Some animals should walk about.");

        for (float t = 0f; t < 8f; t += Time.deltaTime)
        {
            foreach (var animal in walkers)
            {
                if (animal.IsMoving) moved.Add(animal);
                Vector3 p = Flat(animal.transform.position), a = Flat(animal.PointA), b = Flat(animal.PointB);
                float along = Vector3.Dot(p - a, (b - a).normalized);
                Assert.That(along, Is.InRange(-0.05f, Vector3.Distance(a, b) + 0.05f), animal.name + " walked off its stretch.");
                Assert.Less(Vector3.Distance(p, a + (b - a).normalized * along), 0.05f, animal.name + " walked off its stretch.");
            }
            yield return null;
        }
        CollectionAssert.AreEquivalent(walkers, moved, "Every walker should set off within a few seconds.");

        for (int zone = 0; zone < ForestTrail.ZoneCount; zone++)
        {
            foreach (var animal in forest.AnimalsInZone(zone))
            {
                var animator = animal.GetComponentInChildren<Animator>();
                Assert.IsTrue(animator.isActiveAndEnabled && animator.runtimeAnimatorController != null, animal.name + " must be animated.");
                Assert.Greater(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 0f, animal.name + "'s animation must be playing.");
            }
        }
    }

    // The animals are decoration too: no colliders, and neither where they stand nor anywhere along a walker's stretch is
    // on the path or crowding waste or checkpoints.
    [Test]
    public void Animals_StayOffThePathAndBlockNothing()
    {
        for (int zone = 0; zone < ForestTrail.ZoneCount; zone++)
        {
            foreach (var animal in forest.AnimalsInZone(zone))
            {
                Assert.IsEmpty(animal.GetComponentsInChildren<Collider>(true), animal.name + " must not block the player.");
                for (int k = 0; k <= 4; k++)
                {
                    Vector3 p = Vector3.Lerp(animal.PointA, animal.PointB, k / 4f);
                    trail.Project(p, out float lateral);
                    Assert.Greater(Mathf.Abs(lateral), 3f, animal.name + " gets onto the path.");
                    foreach (var c in Object.FindObjectsByType<Checkpoint>())
                        Assert.Greater(Vector3.Distance(Flat(c.transform.position), Flat(p)), 1.5f, animal.name + " crowds " + c.name);
                    foreach (var w in Object.FindObjectsByType<Collectible>())
                        Assert.Greater(Vector3.Distance(Flat(w.transform.position), Flat(p)), 1.5f, animal.name + " covers " + w.name);
                }
            }
        }
    }

    // The flowers are decoration: off the dirt path, never solid, and not in front of waste or checkpoints.
    [Test]
    public void Flowers_StayOffThePathAndBlockNothing()
    {
        for (int zone = 0; zone < ForestTrail.ZoneCount; zone++)
        {
            foreach (var flower in forest.FlowersInZone(zone))
            {
                trail.Project(flower.position, out float lateral);
                Assert.Greater(Mathf.Abs(lateral), 2f, flower.name + " stands on the path.");
                Assert.IsEmpty(flower.GetComponentsInChildren<Collider>(true), flower.name + " must not block the player.");
                foreach (var c in Object.FindObjectsByType<Checkpoint>())
                    Assert.Greater(Vector3.Distance(Flat(c.transform.position), Flat(flower.position)), 0.9f, flower.name + " crowds " + c.name);
                foreach (var w in Object.FindObjectsByType<Collectible>())
                    Assert.Greater(Vector3.Distance(Flat(w.transform.position), Flat(flower.position)), 0.9f, flower.name + " crowds " + w.name);
            }
        }
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // As laid out in Level01 (with Jhon's taller counter), in screen space.
    [Test]
    public void ZoneNotice_DoesNotCoverTheBannerTheArrowOrTheCounter()
    {
        Rect notice = ScreenRect(screens.transform.Find("ZoneNotice"));
        foreach (var path in new[] { "ObjectiveBanner", "GoalCompass/Arrow", "GoalCompass/WrongWay", "WasteCounter", "MissingNotice" })
            Assert.IsFalse(notice.Overlaps(ScreenRect(screens.transform.Find(path))), "'¡Zona recuperada!' would cover " + path + ".");
    }

    private static Rect ScreenRect(Transform t)
    {
        var corners = new Vector3[4];
        ((RectTransform)t).GetWorldCorners(corners); // screen-space overlay canvas: world = screen pixels
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
}
