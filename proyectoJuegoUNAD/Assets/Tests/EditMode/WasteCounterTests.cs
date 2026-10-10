using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The HUD counter "Residuos: X/10" (GDD §10.2): its text and its place on the GameScreens canvas.
public class WasteCounterTests
{
    const string ScreensPath = "Assets/Prefabs/UI/GameScreens.prefab";

    [TestCase(0, 10, "Residuos: 0/10")]
    [TestCase(7, 10, "Residuos: 7/10")]
    [TestCase(10, 10, "Residuos: 10/10")]
    [TestCase(12, 10, "Residuos: 12/10")]
    [TestCase(3, 15, "Residuos: 3/15")]
    public void Format_ShowsCollectedOverTarget(int collected, int target, string expected)
    {
        Assert.AreEqual(expected, WasteCounter.Format(collected, target));
    }

    [Test]
    public void GameScreens_HasACounterBehindTheEndScreens()
    {
        var screens = AssetDatabase.LoadAssetAtPath<GameObject>(ScreensPath);
        Assert.IsNotNull(screens, "Missing " + ScreensPath);

        var counter = screens.GetComponentInChildren<WasteCounter>(true);
        Assert.IsNotNull(counter, "GameScreens needs a WasteCounter.");
        Assert.IsTrue(counter.gameObject.activeSelf, "The counter must be visible from the start.");
        Assert.IsNotNull(new SerializedObject(counter).FindProperty("label").objectReferenceValue, "The counter needs its label.");

        var win = screens.transform.Find("WinPanel");
        var lose = screens.transform.Find("LosePanel");
        Assert.Less(counter.transform.GetSiblingIndex(), win.GetSiblingIndex(), "The win panel must draw over the counter.");
        Assert.Less(counter.transform.GetSiblingIndex(), lose.GetSiblingIndex(), "The lose panel must draw over the counter.");
    }
}
