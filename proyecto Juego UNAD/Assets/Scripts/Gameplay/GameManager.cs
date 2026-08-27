using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI hudText;
    [SerializeField] private GameObject winPanel;

    private int totalSmogZones;
    private int totalPlantingZones;
    private int smogZonesCleared;
    private int treesPlanted;
    private bool hasWon = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Count total zones in the scene
        SmogZone[] smogZones = FindObjectsByType<SmogZone>();
        totalSmogZones = smogZones.Length;

        PlantingZone[] plantingZones = FindObjectsByType<PlantingZone>();
        totalPlantingZones = plantingZones.Length;

        smogZonesCleared = 0;
        treesPlanted = 0;

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        UpdateHUD();
    }

    public void NotifySmogCleared(SmogZone zone)
    {
        smogZonesCleared++;
        UpdateHUD();
        CheckWinCondition();
    }

    public void NotifyTreePlanted(PlantingZone zone)
    {
        treesPlanted++;
        UpdateHUD();
        CheckWinCondition();
    }

    private void UpdateHUD()
    {
        string text = $"Smog Cleared: {smogZonesCleared}/{totalSmogZones}   Trees Planted: {treesPlanted}/{totalPlantingZones}";

        if (hudText != null)
        {
            hudText.text = text;
        }

        Debug.Log(text);
    }

    private void CheckWinCondition()
    {
        if (hasWon) return;

        if (smogZonesCleared >= totalSmogZones && treesPlanted >= totalPlantingZones)
        {
            Win();
        }
    }

    private void Win()
    {
        hasWon = true;
        Debug.Log("=== LEVEL COMPLETE! ===");

        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }

        Time.timeScale = 0f; // Pause the game
    }
}
