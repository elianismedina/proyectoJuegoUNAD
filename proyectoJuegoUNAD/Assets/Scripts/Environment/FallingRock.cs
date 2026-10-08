using UnityEngine;

/// <summary>
/// A rock that drops onto the course after a visible warning. A <see cref="FallingRockTrigger"/> arms it;
/// a shadow marker appears on the ground for <see cref="warningDuration"/> seconds, then the rock falls from
/// <see cref="dropHeight"/> and disappears shortly after impact. Add a trigger collider with
/// <see cref="PlayerHazard"/> to the rock to make it stumble the player.
/// </summary>
public class FallingRock : MonoBehaviour
{
    [Tooltip("Ground shadow shown during the warning; scaled up while the countdown runs.")]
    [SerializeField] private Transform warningMarker;

    [Tooltip("Seconds between the warning appearing and the rock starting to fall.")]
    [SerializeField] private float warningDuration = 1f;

    [Tooltip("Height above the ground (the rock's starting Y) it falls from.")]
    [SerializeField] private float dropHeight = 8f;

    [Tooltip("Seconds the rock stays on the ground after impact before it disappears.")]
    [SerializeField] private float lingerTime = 0.5f;

    private enum Phase { Idle, Warning, Falling, Landed }

    private Phase phase = Phase.Idle;
    private float timer;
    private float groundY;
    private float velocityY;
    private Vector3 markerFullScale;

    private void Awake()
    {
        groundY = transform.position.y;
        if (warningMarker != null)
        {
            markerFullScale = warningMarker.localScale;
            warningMarker.gameObject.SetActive(false);
        }

        // The rock starts hidden and harmless until armed.
        SetRockActive(false);
    }

    /// <summary>Starts the warning countdown. Ignored if the rock was already armed.</summary>
    public void Arm()
    {
        if (phase != Phase.Idle) return;

        phase = Phase.Warning;
        timer = 0f;
        if (warningMarker != null)
        {
            warningMarker.gameObject.SetActive(true);
            warningMarker.localScale = markerFullScale * 0.3f;
        }
    }

    private void Update()
    {
        switch (phase)
        {
            case Phase.Warning:
                timer += Time.deltaTime;
                if (warningMarker != null)
                    warningMarker.localScale = Vector3.Lerp(markerFullScale * 0.3f, markerFullScale, timer / warningDuration);

                if (timer >= warningDuration)
                {
                    phase = Phase.Falling;
                    velocityY = 0f;
                    transform.position = new Vector3(transform.position.x, groundY + dropHeight, transform.position.z);
                    SetRockActive(true);
                }
                break;

            case Phase.Falling:
                velocityY += Physics.gravity.y * Time.deltaTime;
                Vector3 position = transform.position;
                position.y += velocityY * Time.deltaTime;

                if (position.y <= groundY)
                {
                    position.y = groundY;
                    phase = Phase.Landed;
                    timer = 0f;
                    if (warningMarker != null) warningMarker.gameObject.SetActive(false);
                }
                transform.position = position;
                break;

            case Phase.Landed:
                timer += Time.deltaTime;
                if (timer >= lingerTime) SetRockActive(false);
                break;
        }
    }

    /// <summary>Shows or hides the rock's renderers and colliders; the script and the warning marker stay alive.</summary>
    private void SetRockActive(bool active)
    {
        foreach (var rendererComponent in GetComponentsInChildren<Renderer>(true))
        {
            if (warningMarker != null && rendererComponent.transform.IsChildOf(warningMarker)) continue;
            rendererComponent.enabled = active;
        }

        foreach (var colliderComponent in GetComponentsInChildren<Collider>(true))
            colliderComponent.enabled = active;
    }
}
