using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The banner that states the level goal when Level01 starts (GDD §5.1): its text and its place on the GameScreens canvas.
public class ObjectiveBannerTests
{
    const string ScreensPath = "Assets/Prefabs/UI/GameScreens.prefab";

    [TestCase(10, "Recoge 10 residuos y llega a la meta")]
    [TestCase(15, "Recoge 15 residuos y llega a la meta")]
    [TestCase(1, "Recoge 1 residuo y llega a la meta")]
    public void FormatObjective_NamesTheTargetAndTheGoal(int target, string expected)
    {
        Assert.AreEqual(expected, GameScreens.FormatObjective(target));
    }

    [Test]
    public void GameScreens_HasAnObjectiveBannerUnderTheEndScreensThatNeverBlocksClicks()
    {
        var screens = AssetDatabase.LoadAssetAtPath<GameObject>(ScreensPath);
        Assert.IsNotNull(screens, "Missing " + ScreensPath);

        var so = new SerializedObject(screens.GetComponent<GameScreens>());
        var banner = so.FindProperty("objectiveBanner").objectReferenceValue as CanvasGroup;
        Assert.IsNotNull(banner, "GameScreens needs its objective banner.");
        Assert.IsNotNull(so.FindProperty("objectiveText").objectReferenceValue, "The banner needs its text.");
        Assert.IsFalse(banner.blocksRaycasts, "The banner must not catch clicks meant for the menus.");

        var win = screens.transform.Find("WinPanel");
        var pause = screens.transform.Find("PausePanel");
        Assert.Less(banner.transform.GetSiblingIndex(), win.GetSiblingIndex(), "The win panel must draw over the banner.");
        Assert.Less(banner.transform.GetSiblingIndex(), pause.GetSiblingIndex(), "The pause menu must draw over the banner.");
    }
}
