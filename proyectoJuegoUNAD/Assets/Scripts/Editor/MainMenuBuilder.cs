using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the title screen in Assets/Scenes/MainMenu.unity: the game's name, its goal in one sentence (so the player
/// knows the objective before playing), the controls, and the "Jugar" / "Salir" buttons wired to <see cref="MainMenu"/>.
/// Same look as the in-game screens (GameScreens.prefab): dark green box, yellow buttons, legacy Text.
/// Opens the scene, rebuilds the menu canvas (hand edits under it are discarded) and saves the scene.
/// </summary>
public static class MainMenuBuilder
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string CanvasName = "MainMenuCanvas";
    private const string EventSystemName = "EventSystem";

    private static readonly Color Background = new Color(0.16f, 0.27f, 0.17f);
    private static readonly Color BoxColor = new Color(0.07f, 0.11f, 0.07f, 0.94f);
    private static readonly Color TitleColor = new Color(1f, 0.85f, 0.1f);
    private static readonly Color TextColor = new Color(0.93f, 0.96f, 0.9f);
    private static readonly Color HintColor = new Color(0.7f, 0.8f, 0.68f);
    private static readonly Color ButtonText = new Color(0.1f, 0.12f, 0.08f);

    private const string Title = "Guardianes del Bosque";
    private const string Goal = "Recorre el sendero, recoge 10 residuos y llega a la meta para limpiar el bosque.";
    private const string Controls = "WASD o flechas: moverte    Ratón: mirar    Espacio: saltar    Esc: pausa";

    [MenuItem("Forest Guardian/UI/Build Main Menu")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (var name in new[] { CanvasName, EventSystemName })
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        var camera = Camera.main;
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
        }

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Canvas, scaled like GameScreens (1920x1080 reference).
        var canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var box = MakePanel("Box", canvasObject.transform, new Vector2(1200f, 760f), BoxColor);

        MakeLabel("Title", box, Title, font, 96, TitleColor, new Vector2(0f, 250f), new Vector2(1100f, 140f), FontStyle.Bold);
        MakeLabel("Goal", box, Goal, font, 40, TextColor, new Vector2(0f, 90f), new Vector2(1000f, 130f), FontStyle.Normal);
        var play = MakeButton("PlayButton", box, "Jugar", font, new Vector2(0f, -70f));
        var quit = MakeButton("QuitButton", box, "Salir", font, new Vector2(0f, -200f));
        MakeLabel("Controls", box, Controls, font, 28, HintColor, new Vector2(0f, -320f), new Vector2(1150f, 60f), FontStyle.Normal);

        var menu = canvasObject.AddComponent<MainMenu>();
        var serialized = new SerializedObject(menu);
        serialized.FindProperty("playButton").objectReferenceValue = play;
        serialized.FindProperty("quitButton").objectReferenceValue = quit;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Up and down move between the two buttons with arrows, W/S or the gamepad.
        SetVerticalNavigation(play, null, quit);
        SetVerticalNavigation(quit, play, null);

        var eventSystem = new GameObject(EventSystemName, typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<EventSystem>().firstSelectedGameObject = play.gameObject;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvasObject;
        Debug.Log("MainMenuBuilder: main menu built and " + ScenePath + " saved.");
    }

    private static RectTransform MakePanel(string name, Transform parent, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private static Text MakeLabel(string name, Transform parent, string text, Font font, int size, Color color,
        Vector2 position, Vector2 box, FontStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.layer = parent.gameObject.layer;
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = box;
        rect.anchoredPosition = position;

        var label = go.GetComponent<Text>();
        label.text = text;
        label.font = font;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static Button MakeButton(string name, Transform parent, string text, Font font, Vector2 position)
    {
        var rect = MakePanel(name, parent, new Vector2(460f, 110f), Color.white);
        rect.anchoredPosition = position;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors; // Same palette as the GameScreens buttons.
        colors.normalColor = new Color(1f, 0.85f, 0.1f);
        colors.highlightedColor = new Color(1f, 0.95f, 0.55f);
        colors.selectedColor = new Color(1f, 0.95f, 0.55f);
        colors.pressedColor = new Color(0.85f, 0.7f, 0.05f);
        button.colors = colors;

        var label = MakeLabel("Label", rect, text, font, 48, ButtonText, Vector2.zero, rect.sizeDelta, FontStyle.Bold);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.sizeDelta = Vector2.zero;
        return button;
    }

    private static void SetVerticalNavigation(Button button, Button up, Button down)
    {
        var navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
        button.navigation = navigation;
    }
}
