using System;
using UnityEngine;

/// <summary>
/// A waste item the player picks up by touching it (GDD §6.4): counts one waste in the <see cref="GameSession"/>
/// and disappears. The trigger is deliberately larger than the model (GDD §11, enlarged pickup radius).
/// The model sits under <see cref="visual"/>, which bobs and spins so waste never reads as scenery
/// (the "bright and moving means collect" rule in Docs/NaturePackIntegrationPlan.md); the trigger stays still.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Collectible : MonoBehaviour
{
    [SerializeField, Tooltip("Child that holds the model; it bobs and spins, the root and its trigger do not.")]
    private Transform visual;

    [SerializeField, Min(0f), Tooltip("Degrees per second around the vertical axis.")]
    private float spinSpeed = 90f;

    [SerializeField, Min(0f), Tooltip("Vertical travel of the bob, in metres (peak to peak).")]
    private float bobHeight = 0.15f;

    [SerializeField, Min(0.1f), Tooltip("Seconds for one full bob up and down.")]
    private float bobPeriod = 2f;

    /// <summary>Raised with the collected item, after it was counted. Hook VFX and SFX here.</summary>
    public static event Action<Collectible> Collected;

    public bool IsCollected { get; private set; }

    private Vector3 visualRestPosition;
    private float phase;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (visual != null) visualRestPosition = visual.localPosition;
        // Desynchronise neighbouring items so a row of waste does not move in lockstep.
        phase = Mathf.Repeat(transform.position.x * 0.37f, 1f) * bobPeriod;
    }

    private void Update()
    {
        if (visual == null) return;

        float t = Time.time + phase;
        visual.localPosition = visualRestPosition + Vector3.up * (Mathf.Sin(t * 2f * Mathf.PI / bobPeriod) * bobHeight * 0.5f);
        visual.localRotation = Quaternion.Euler(0f, t * spinSpeed, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsCollected || !other.TryGetComponent(out PlayerController _)) return;
        if (GameManager.Instance == null || !GameManager.Instance.Session.AddWaste()) return;

        IsCollected = true;
        Collected?.Invoke(this);
        gameObject.SetActive(false);
    }
}
