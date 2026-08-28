using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DroneHUD : MonoBehaviour
{
    [SerializeField] private DroneController droneController;
    [SerializeField] private TextMeshProUGUI beamEnergyText;
    [SerializeField] private Image beamEnergyBar;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private Color energyFullColor = Color.green;
    [SerializeField] private Color energyLowColor = Color.red;

    private void Update()
    {
        if (droneController == null) return;

        UpdateBeamEnergyDisplay();
        UpdateStateDisplay();
    }

    private void UpdateBeamEnergyDisplay()
    {
        float energyNormalized = droneController.GetBeamEnergyNormalized();

        if (beamEnergyBar != null)
        {
            beamEnergyBar.fillAmount = energyNormalized;
            beamEnergyBar.color = Color.Lerp(energyLowColor, energyFullColor, energyNormalized);
        }

        if (beamEnergyText != null)
        {
            beamEnergyText.text = $"Energy: {(energyNormalized * 100):F0}%";
        }
    }

    private void UpdateStateDisplay()
    {
        if (stateText == null) return;

        string stateLabel = droneController.GetState() switch
        {
            DroneController.DroneState.Flying => "Flying",
            DroneController.DroneState.Hovering => "Hovering",
            DroneController.DroneState.Planting => "Planting...",
            _ => "Unknown"
        };

        stateText.text = $"State: {stateLabel}";
    }
}
