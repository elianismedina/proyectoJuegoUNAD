using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the HUD clock ("Tiempo: 2:00", <see cref="LevelTimer"/>) to <c>Assets/Prefabs/UI/GameScreens.prefab</c>, in the
/// top-right corner: below the "Vas en sentido contrario" warning (y ≥ −88), right of the "¡Zona recuperada!" notice
/// (x ≤ 1310) and above the objective banner and the other notices (y ≤ −255), so it never covers any of them. Its warning
/// for the last seconds plays <c>Assets/Sound/SFX/sonido_tiempo_aviso.wav</c> (a generated placeholder anyone can swap for
/// another clip of the same name) from the canvas' 2D AudioSource. It also wires the lose screen's detail text into
/// <see cref="GameScreens"/>, so the loss can say "¡Se acabó el tiempo!". Prefab only: Level01.unity is untouched.
/// Menu: Forest Guardian > UI > Build Level Timer. Safe to run again: it replaces the clock.
/// </summary>
public static class LevelTimerBuilder
{
    private const string ScreensPath = "Assets/Prefabs/UI/GameScreens.prefab";
    private const string ClipPath = "Assets/Sound/SFX/sonido_tiempo_aviso.wav";
    private const string TimerName = "LevelTimer";

    public static readonly Vector2 TimerPosition = new Vector2(-40f, -100f);
    public static readonly Vector2 TimerSize = new Vector2(300f, 90f);
    private static readonly Color PanelColor = new Color(0.04f, 0.08f, 0.05f, 0.75f);
    private const int FontSize = 44;

    [MenuItem("Forest Guardian/UI/Build Level Timer")]
    public static void Build()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (clip == null) Debug.LogWarning("LevelTimerBuilder: missing " + ClipPath + "; the time warning will be silent.");

        var root = PrefabUtility.LoadPrefabContents(ScreensPath);
        try
        {
            var old = root.transform.Find(TimerName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var font = root.transform.Find("MissingNotice/Text").GetComponent<Text>().font;

            var timer = new GameObject(TimerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            timer.layer = root.layer;
            var rect = (RectTransform)timer.transform;
            rect.SetParent(root.transform, false);
            var counter = root.transform.Find("WasteCounter");
            rect.SetSiblingIndex(counter != null ? counter.GetSiblingIndex() + 1 : 0); // HUD layer, under the panels
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = TimerPosition;
            rect.sizeDelta = TimerSize;
            var image = timer.GetComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;

            var label = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            label.layer = root.layer;
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.font = font;
            text.text = LevelTimer.Format(120f);
            text.fontSize = FontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            var outline = label.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);

            // The warning plays from the canvas, which is always active (the clock hides at the end of the game).
            var audio = root.GetComponent<AudioSource>();
            if (audio == null) audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var component = timer.AddComponent<LevelTimer>();
            var so = new SerializedObject(component);
            so.FindProperty("label").objectReferenceValue = text;
            so.FindProperty("warningAudio").objectReferenceValue = audio;
            so.FindProperty("warningClip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();

            var loseDetail = root.transform.Find("LosePanel/Box/Detail");
            if (loseDetail == null) Debug.LogWarning("LevelTimerBuilder: no LosePanel/Box/Detail; the loss will not say why.");
            var screens = new SerializedObject(root.GetComponent<GameScreens>());
            screens.FindProperty("loseDetail").objectReferenceValue = loseDetail != null ? loseDetail.GetComponent<Text>() : null;
            screens.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ScreensPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("LevelTimerBuilder: level timer added to " + ScreensPath + ".");
    }
}
