using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SceneSetup
{
    [MenuItem("Smog Buster/Setup Scene")]
    public static void SetupScene()
    {
        // Create layers if they don't exist
        CreateLayer("Smog");
        CreateLayer("PlantingZone");
        CreateLayer("Ground");

        // Create Ground
        CreateGround();

        // Create Canvas with UI
        CreateUICanvas();

        // Create GameManager
        CreateGameManager();

        // Create a placeholder smog zone for testing
        CreatePlaceholderSmogZone();

        // Create a placeholder planting zone for testing
        CreatePlaceholderPlantingZone();

        Debug.Log("✓ Scene setup complete! You can now add smog zones and planting zones as needed.");
    }

    private static void CreateLayer(string layerName)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        bool layerExists = false;
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (layer.stringValue == layerName)
            {
                layerExists = true;
                break;
            }
        }

        if (!layerExists)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"Created layer: {layerName}");
                    break;
                }
            }
        }
    }

    private static void CreateGround()
    {
        GameObject primitivePlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        primitivePlane.name = "Ground";
        primitivePlane.transform.localScale = new Vector3(10, 1, 10);
        primitivePlane.layer = LayerMask.NameToLayer("Ground");

        // Remove the default collider and add a Box collider
        Object.DestroyImmediate(primitivePlane.GetComponent<Collider>());
        primitivePlane.AddComponent<BoxCollider>();

        Material groundMat = new Material(Shader.Find("Standard"));
        groundMat.color = new Color(0.2f, 0.8f, 0.2f);
        primitivePlane.GetComponent<MeshRenderer>().material = groundMat;

        Debug.Log("Created Ground");
    }

    private static void CreateUICanvas()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        // Create HUD Text
        GameObject hudObj = new GameObject("HUDText");
        hudObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI hudText = hudObj.AddComponent<TextMeshProUGUI>();
        hudText.text = "Smog Cleared: 0/0   Trees Planted: 0/0";
        hudText.fontSize = 36;
        hudText.color = Color.white;

        RectTransform hudRect = hudObj.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0, 1);
        hudRect.anchorMax = new Vector2(0, 1);
        hudRect.pivot = new Vector2(0, 1);
        hudRect.anchoredPosition = new Vector2(20, -20);
        hudRect.sizeDelta = new Vector2(800, 100);

        // Create Win Panel
        GameObject winPanelObj = new GameObject("WinPanel");
        winPanelObj.transform.SetParent(canvasObj.transform, false);
        Image panelImage = winPanelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);

        RectTransform panelRect = winPanelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Create Win Text
        GameObject winTextObj = new GameObject("Text");
        winTextObj.transform.SetParent(winPanelObj.transform, false);
        TextMeshProUGUI winText = winTextObj.AddComponent<TextMeshProUGUI>();
        winText.text = "YOU WIN!";
        winText.fontSize = 80;
        winText.color = Color.white;
        winText.alignment = TextAlignmentOptions.Center;

        RectTransform winTextRect = winTextObj.GetComponent<RectTransform>();
        winTextRect.anchorMin = Vector2.zero;
        winTextRect.anchorMax = Vector2.one;
        winTextRect.offsetMin = Vector2.zero;
        winTextRect.offsetMax = Vector2.zero;

        winPanelObj.SetActive(false);

        Debug.Log("Created Canvas with UI");

        // Store references in GameManager (if it exists)
        GameManager gm = Object.FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            gm.GetComponent<GameManager>().GetType().GetField("hudText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(gm, hudText);

            gm.GetComponent<GameManager>().GetType().GetField("winPanel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(gm, winPanelObj);
        }
    }

    private static void CreateGameManager()
    {
        GameObject gmObj = new GameObject("GameManager");
        GameManager gm = gmObj.AddComponent<GameManager>();

        Debug.Log("Created GameManager");
    }

    private static void CreatePlaceholderSmogZone()
    {
        GameObject smogObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        smogObj.name = "SmogZone_1";
        smogObj.transform.position = new Vector3(0, 3, 5);
        smogObj.transform.localScale = new Vector3(2, 2, 2);

        Collider collider = smogObj.GetComponent<Collider>();
        collider.isTrigger = true;

        smogObj.layer = LayerMask.NameToLayer("Smog");

        // Remove the MeshCollider that comes with the primitive
        Object.DestroyImmediate(smogObj.GetComponent<MeshCollider>());
        smogObj.AddComponent<SphereCollider>().isTrigger = true;

        SmogZone smogZone = smogObj.AddComponent<SmogZone>();

        // Create a simple particle system as child
        GameObject psObj = new GameObject("ParticleSystem");
        psObj.transform.SetParent(smogObj.transform);
        psObj.transform.localPosition = Vector3.zero;
        ParticleSystem ps = psObj.AddComponent<ParticleSystem>();

        // Add renderer
        smogObj.GetComponent<MeshRenderer>().material.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        Debug.Log("Created placeholder SmogZone");
    }

    private static void CreatePlaceholderPlantingZone()
    {
        GameObject plantObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plantObj.name = "PlantingZone_1";
        plantObj.transform.position = new Vector3(-3, 0.1f, 0);
        plantObj.transform.localScale = new Vector3(2, 0.1f, 2);

        Collider collider = plantObj.GetComponent<Collider>();
        collider.isTrigger = true;

        plantObj.layer = LayerMask.NameToLayer("PlantingZone");

        // Remove the old collider
        Object.DestroyImmediate(plantObj.GetComponent<CapsuleCollider>());
        plantObj.AddComponent<SphereCollider>().isTrigger = true;

        PlantingZone zone = plantObj.AddComponent<PlantingZone>();

        // Create prompt UI (just a placeholder, actual prompt handled by PlantingZone)
        GameObject promptObj = new GameObject("Prompt");
        promptObj.transform.SetParent(plantObj.transform);
        promptObj.transform.localPosition = new Vector3(0, 2, 0);
        TextMesh prompt = promptObj.AddComponent<TextMesh>();
        prompt.text = "Press F to plant";
        promptObj.SetActive(false);

        // Add tree prefab reference (will be created as placeholder)
        GameObject treeObj = new GameObject("TreePlaceholder");
        treeObj.transform.position = new Vector3(-3, 0.5f, 0);

        // Make tree invisible for now
        treeObj.SetActive(false);

        plantObj.GetComponent<MeshRenderer>().material.color = new Color(0.6f, 0.4f, 0.2f);

        Debug.Log("Created placeholder PlantingZone");
    }
}
