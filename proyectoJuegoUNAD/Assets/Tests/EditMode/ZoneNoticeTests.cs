using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The "¡Zona recuperada!" notice on the GameScreens canvas: wired, starts hidden, never catches clicks, and never covers
// the objective banner, the goal arrow (and its warning), the other notices or the counter.
public class ZoneNoticeTests
{
    const string ScreensPath = "Assets/Prefabs/UI/GameScreens.prefab";
    static readonly Vector2 Reference = new Vector2(1920f, 1080f); // the CanvasScaler's reference resolution

    static GameObject Screens()
    {
        var screens = AssetDatabase.LoadAssetAtPath<GameObject>(ScreensPath);
        Assert.IsNotNull(screens, "Missing " + ScreensPath);
        return screens;
    }

    // Rect on the reference canvas (origin bottom-left) of a child of the canvas whose anchors are a single point.
    static Rect CanvasRect(RectTransform r)
    {
        Assert.AreEqual(r.anchorMin, r.anchorMax, r.name + ": the layout check expects point anchors.");
        Vector2 anchor = Vector2.Scale(r.anchorMin, Reference);
        if (r.parent is RectTransform parent && parent.GetComponent<Canvas>() == null)
        {
            // One level down (e.g. the warning beside the arrow): anchor inside the parent's rect.
            Rect p = CanvasRect(parent);
            anchor = p.min + Vector2.Scale(r.anchorMin, p.size);
        }
        Vector2 min = anchor + r.anchoredPosition - Vector2.Scale(r.pivot, r.sizeDelta);
        return new Rect(min, r.sizeDelta);
    }

    [Test]
    public void GameScreens_HasTheZoneNoticeWithItsChime()
    {
        var screens = Screens();
        var so = new SerializedObject(screens.GetComponent<GameScreens>());
        var notice = so.FindProperty("zoneNotice").objectReferenceValue as CanvasGroup;
        Assert.IsNotNull(notice, "GameScreens needs the zone notice (Forest Guardian > UI > Build Zone Notice).");
        Assert.IsFalse(notice.gameObject.activeSelf, "The notice starts hidden.");
        Assert.IsFalse(notice.blocksRaycasts, "The notice must not catch clicks meant for the menus.");

        var audio = so.FindProperty("zoneNoticeAudio").objectReferenceValue as AudioSource;
        Assert.IsNotNull(audio, "The notice needs an AudioSource for its chime.");
        Assert.AreEqual(0f, audio.spatialBlend, "The chime is 2D, like the rest of the UI sound.");
        Assert.IsFalse(audio.playOnAwake);
        var clip = so.FindProperty("zoneNoticeClip").objectReferenceValue as AudioClip;
        Assert.IsNotNull(clip, "The notice needs its chime clip.");
        Assert.Less(clip.length, 1.5f, "The chime must be short.");

        Assert.Less(notice.transform.GetSiblingIndex(), screens.transform.Find("WinPanel").GetSiblingIndex(), "The win panel must draw over the notice.");
        Assert.Less(notice.transform.GetSiblingIndex(), screens.transform.Find("PausePanel").GetSiblingIndex(), "The pause menu must draw over the notice.");
    }

    [Test]
    public void ZoneNotice_CoversNeitherTheBannerNorTheArrowNorTheOtherHud()
    {
        var screens = Screens().transform;
        Rect notice = CanvasRect((RectTransform)screens.Find("ZoneNotice"));

        foreach (var path in new[] { "ObjectiveBanner", "GoalCompass", "GoalCompass/Arrow", "GoalCompass/WrongWay", "MissingNotice", "WasteCounter" })
        {
            var other = (RectTransform)screens.Find(path);
            Assert.IsNotNull(other, "GameScreens needs " + path);
            Assert.IsFalse(notice.Overlaps(CanvasRect(other)), "'¡Zona recuperada!' would cover " + path + ".");
        }
    }
}
