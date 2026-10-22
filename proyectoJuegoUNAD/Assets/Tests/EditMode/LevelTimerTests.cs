using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The HUD clock (GDD §10.2): its text, the level's 2-minute limit, its wiring on the GameScreens canvas, and its place
// in the top-right corner, where it covers none of the other HUD.
public class LevelTimerTests
{
    const string ScreensPath = "Assets/Prefabs/UI/GameScreens.prefab";
    const string ConfigPath = "Assets/Config/LevelConfig_Level01.asset";
    static readonly Vector2 Reference = new Vector2(1920f, 1080f); // the CanvasScaler's reference resolution

    [TestCase(120f, "2:00")]
    [TestCase(119.2f, "2:00")]
    [TestCase(65f, "1:05")]
    [TestCase(59.2f, "1:00")]
    [TestCase(0.4f, "0:01")]
    [TestCase(0f, "0:00")]
    [TestCase(-3f, "0:00")]
    public void FormatTime_RoundsUpToWholeSeconds(float seconds, string expected)
    {
        Assert.AreEqual(expected, LevelTimer.FormatTime(seconds));
    }

    [Test]
    public void Format_ReadsLikeTheCounter()
    {
        Assert.AreEqual("Tiempo: 2:00", LevelTimer.Format(120f));
    }

    [Test]
    public void ObjectiveAndWinDetail_NameTheTime()
    {
        Assert.AreEqual("Recoge 10 residuos y llega a la meta en 2:00", GameScreens.FormatObjective(10, 120f));
        Assert.AreEqual("Recoge 10 residuos y llega a la meta", GameScreens.FormatObjective(10, 0f));
        Assert.AreEqual("Recogiste 10 residuos en 1:42.", GameScreens.FormatWinDetail(10, 101.3f, true));
        Assert.AreEqual("Recogiste 1 residuo.", GameScreens.FormatWinDetail(1, 50f, false));
    }

    [Test]
    public void Level01_HasTwoMinutes()
    {
        var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(ConfigPath);
        Assert.IsNotNull(config, "Missing " + ConfigPath);
        Assert.AreEqual(120f, config.timeLimitSeconds, "GDD §5.2: the level lasts 2 minutes.");
        Assert.Greater(config.timeWarningSeconds, 0f);
        Assert.Less(config.timeWarningSeconds, config.timeLimitSeconds);
    }

    static GameObject Screens()
    {
        var screens = AssetDatabase.LoadAssetAtPath<GameObject>(ScreensPath);
        Assert.IsNotNull(screens, "Missing " + ScreensPath);
        return screens;
    }

    [Test]
    public void GameScreens_HasTheTimerWithItsWarning()
    {
        var screens = Screens();
        var timer = screens.GetComponentInChildren<LevelTimer>(true);
        Assert.IsNotNull(timer, "GameScreens needs the clock (Forest Guardian > UI > Build Level Timer).");

        var so = new SerializedObject(timer);
        Assert.IsNotNull(so.FindProperty("label").objectReferenceValue, "The clock needs its text.");
        var audio = so.FindProperty("warningAudio").objectReferenceValue as AudioSource;
        Assert.IsNotNull(audio, "The clock needs an AudioSource for its warning.");
        Assert.AreEqual(0f, audio.spatialBlend, "The warning is 2D, like the rest of the UI sound.");
        var clip = so.FindProperty("warningClip").objectReferenceValue as AudioClip;
        Assert.IsNotNull(clip, "The clock needs its warning clip.");
        Assert.Less(clip.length, 1.5f, "The warning must be short.");

        foreach (var graphic in timer.GetComponentsInChildren<Graphic>(true))
            Assert.IsFalse(graphic.raycastTarget, graphic.name + " must not catch clicks meant for the menus.");
        Assert.Less(timer.transform.GetSiblingIndex(), screens.transform.Find("WinPanel").GetSiblingIndex(), "The win panel must draw over the clock.");
        Assert.Less(timer.transform.GetSiblingIndex(), screens.transform.Find("PausePanel").GetSiblingIndex(), "The pause menu must draw over the clock.");

        var loseDetail = new SerializedObject(screens.GetComponent<GameScreens>()).FindProperty("loseDetail").objectReferenceValue as Text;
        Assert.IsNotNull(loseDetail, "The lose screen's detail must be wired, so a loss can say the time ran out.");
        Assert.IsTrue(loseDetail.transform.IsChildOf(screens.transform.Find("LosePanel")));
    }

    // Rect on the reference canvas (origin bottom-left) of a child of the canvas whose anchors are a single point.
    static Rect CanvasRect(RectTransform r)
    {
        Assert.AreEqual(r.anchorMin, r.anchorMax, r.name + ": the layout check expects point anchors.");
        Vector2 anchor = Vector2.Scale(r.anchorMin, Reference);
        if (r.parent is RectTransform parent && parent.GetComponent<Canvas>() == null)
        {
            Rect p = CanvasRect(parent);
            anchor = p.min + Vector2.Scale(r.anchorMin, p.size);
        }
        Vector2 min = anchor + r.anchoredPosition - Vector2.Scale(r.pivot, r.sizeDelta);
        return new Rect(min, r.sizeDelta);
    }

    [Test]
    public void Timer_SitsOnScreenAndCoversNoOtherHud()
    {
        var screens = Screens().transform;
        Rect timer = CanvasRect((RectTransform)screens.Find("LevelTimer"));
        Assert.IsTrue(new Rect(Vector2.zero, Reference).Contains(timer.min) && new Rect(Vector2.zero, Reference).Contains(timer.max),
            "The clock must be fully on screen.");

        foreach (var path in new[] { "ObjectiveBanner", "GoalCompass", "GoalCompass/Arrow", "GoalCompass/WrongWay", "MissingNotice", "ZoneNotice", "WasteCounter" })
        {
            var other = (RectTransform)screens.Find(path);
            Assert.IsNotNull(other, "GameScreens needs " + path);
            Assert.IsFalse(timer.Overlaps(CanvasRect(other)), "The clock would cover " + path + ".");
        }
    }
}
