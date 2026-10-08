using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Receives the "Pause" action from <see cref="PlayerInput"/> (Send Messages behaviour) and toggles pause.
/// </summary>
public class PlayerPauseInput : MonoBehaviour
{
    // Called by PlayerInput via SendMessage for the "Pause" action.
    private void OnPause(InputValue value)
    {
        if (value.isPressed && GameManager.Instance != null)
            GameManager.Instance.TogglePause();
    }
}
