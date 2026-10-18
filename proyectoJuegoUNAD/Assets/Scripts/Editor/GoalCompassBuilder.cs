using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the <see cref="GoalCompass"/> to the HUD in <c>Assets/Prefabs/UI/GameScreens.prefab</c>: a small arrowhead at the
/// top centre of the screen that points the way to the goal, and the "Vas en sentido contrario" label beside it.
/// It also draws the arrow sprite into <c>Assets/Generated/HUD_GoalArrow.png</c> (white with a dark rim, so it reads on
/// sky and on grass). Lives in the prefab, not the scene, so it does not touch <c>Level01.unity</c>.
/// Menu: Forest Guardian > UI > Build Goal Compass. Safe to run again: it replaces the compass.
/// </summary>
public static class GoalCompassBuilder
{
    private const string ScreensPath = "Assets/Prefabs/UI/GameScreens.prefab";
    private const string ArrowPath = "Assets/Generated/HUD_GoalArrow.png";
    private const string CompassName = "GoalCompass";

    // Top centre, above the notices (MissingNotice spans y −270…−390) and well clear of the counter at the top left.
    private static readonly Vector2 CompassPosition = new Vector2(0f, -56f);
    private const float ArrowSize = 64f;
    private const int LabelFontSize = 36;
    private static readonly Color LabelColor = new Color(1f, 0.6f, 0.15f, 1f);

    // Arrowhead with a notch, pointing up, in 0–1 texture space.
    private static readonly Vector2[] ArrowShape =
    {
        new Vector2(0.5f, 0.94f), new Vector2(0.86f, 0.16f), new Vector2(0.5f, 0.36f), new Vector2(0.14f, 0.16f),
    };
    private const int ArrowTextureSize = 128;
    private const float RimWidth = 0.045f;

    [MenuItem("Forest Guardian/UI/Build Goal Compass")]
    public static void Build()
    {
        var sprite = WriteArrowSprite();

        var root = PrefabUtility.LoadPrefabContents(ScreensPath);
        try
        {
            var old = root.transform.Find(CompassName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var font = root.transform.Find("MissingNotice/Text").GetComponent<Text>().font;

            var compass = new GameObject(CompassName, typeof(RectTransform));
            compass.layer = root.layer;
            var rect = (RectTransform)compass.transform;
            rect.SetParent(root.transform, false);
            var counter = root.transform.Find("WasteCounter");
            rect.SetSiblingIndex(counter != null ? counter.GetSiblingIndex() + 1 : 0); // HUD layer, under the panels
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = CompassPosition;
            rect.sizeDelta = new Vector2(ArrowSize, ArrowSize);

            var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            arrow.layer = root.layer;
            var arrowRect = (RectTransform)arrow.transform;
            arrowRect.SetParent(rect, false);
            arrowRect.anchorMin = arrowRect.anchorMax = arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            var image = arrow.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var label = new GameObject("WrongWay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            label.layer = root.layer;
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(ArrowSize * 0.5f + 12f, 0f); // beside the arrow, so it never covers a notice
            labelRect.sizeDelta = new Vector2(620f, ArrowSize);
            var text = label.GetComponent<Text>();
            text.font = font;
            text.text = GoalCompass.WrongWayMessage;
            text.fontSize = LabelFontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = LabelColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            var outline = label.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);
            label.SetActive(false);

            var component = compass.AddComponent<GoalCompass>();
            var so = new SerializedObject(component);
            so.FindProperty("arrow").objectReferenceValue = arrowRect;
            so.FindProperty("arrowImage").objectReferenceValue = image;
            so.FindProperty("wrongWayLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ScreensPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("GoalCompassBuilder: goal compass added to " + ScreensPath + ".");
    }

    private static Sprite WriteArrowSprite()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ArrowPath));
        int n = ArrowTextureSize;
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            var p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
            float edge = DistanceToOutline(p);
            bool inside = Inside(p);
            // White inside, a dark rim around it, soft edges (about one pixel) on both.
            float pixel = 1f / n;
            Color c;
            if (inside) c = Color.Lerp(new Color(0.08f, 0.08f, 0.08f, 1f), Color.white, Mathf.Clamp01((edge - RimWidth * 0.5f) / pixel));
            else c = new Color(0.08f, 0.08f, 0.08f, Mathf.Clamp01((RimWidth * 0.5f - edge) / pixel + 0.5f));
            texture.SetPixel(x, y, c);
        }
        File.WriteAllBytes(ArrowPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(ArrowPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(ArrowPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single; // a fresh texture can come in as Multiple, with no sprite to assign
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(ArrowPath);
    }

    private static bool Inside(Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = ArrowShape.Length - 1; i < ArrowShape.Length; j = i++)
        {
            Vector2 a = ArrowShape[i], b = ArrowShape[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    private static float DistanceToOutline(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0, j = ArrowShape.Length - 1; i < ArrowShape.Length; j = i++)
        {
            Vector2 a = ArrowShape[j], ab = ArrowShape[i] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }
}
