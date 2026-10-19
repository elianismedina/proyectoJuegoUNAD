using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the "¡Zona recuperada!" notice to the HUD in <c>Assets/Prefabs/UI/GameScreens.prefab</c>: a compact panel in the
/// forest-green of recovery, shown by <see cref="GameScreens"/> for about 2 s when a zone is recovered, plus a 2D
/// AudioSource on the canvas for its chime (<c>Assets/Sound/SFX/sonido_zona_recuperada.wav</c>, a placeholder anyone can
/// swap for another clip of the same name). It sits between the goal arrow (y −24…−88) and the objective banner
/// (y −255…−445) and right of the counter (x ≤ 480), so it never covers any of them. Prefab only: Level01.unity is untouched.
/// Menu: Forest Guardian > UI > Build Zone Notice. Safe to run again: it replaces the notice.
/// </summary>
public static class ZoneNoticeBuilder
{
    private const string ScreensPath = "Assets/Prefabs/UI/GameScreens.prefab";
    private const string ClipPath = "Assets/Sound/SFX/sonido_zona_recuperada.wav";
    private const string NoticeName = "ZoneNotice";

    public static readonly Vector2 NoticePosition = new Vector2(0f, -150f);
    public static readonly Vector2 NoticeSize = new Vector2(700f, 100f);
    private static readonly Color PanelColor = new Color(0.05f, 0.18f, 0.06f, 0.85f);
    private static readonly Color TextColor = new Color(0.65f, 1f, 0.45f, 1f);
    private const int FontSize = 56;

    [MenuItem("Forest Guardian/UI/Build Zone Notice")]
    public static void Build()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (clip == null) Debug.LogWarning("ZoneNoticeBuilder: missing " + ClipPath + "; the notice will be silent.");

        var root = PrefabUtility.LoadPrefabContents(ScreensPath);
        try
        {
            var old = root.transform.Find(NoticeName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var font = root.transform.Find("MissingNotice/Text").GetComponent<Text>().font;

            var notice = new GameObject(NoticeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            notice.layer = root.layer;
            var rect = (RectTransform)notice.transform;
            rect.SetParent(root.transform, false);
            var counter = root.transform.Find("WasteCounter");
            rect.SetSiblingIndex(counter != null ? counter.GetSiblingIndex() + 1 : 0); // HUD layer, under the panels
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = NoticePosition;
            rect.sizeDelta = NoticeSize;
            var image = notice.GetComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;
            var group = notice.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var label = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            label.layer = root.layer;
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.font = font;
            text.text = GameScreens.ZoneRecoveredMessage;
            text.fontSize = FontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = TextColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            var outline = label.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);
            notice.SetActive(false);

            // The chime plays from the canvas, which is always active (the notice itself starts hidden).
            var audio = root.GetComponent<AudioSource>();
            if (audio == null) audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var so = new SerializedObject(root.GetComponent<GameScreens>());
            so.FindProperty("zoneNotice").objectReferenceValue = group;
            so.FindProperty("zoneNoticeAudio").objectReferenceValue = audio;
            so.FindProperty("zoneNoticeClip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ScreensPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("ZoneNoticeBuilder: zone notice added to " + ScreensPath + ".");
    }
}
