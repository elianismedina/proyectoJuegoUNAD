using System;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Catches the tagged player when they fall below <see cref="killY"/> (into a stream or off the course) and puts them
/// back at the <see cref="Checkpoint"/> at the start of the zone they fell in (GDD §5.2): the game keeps going and the
/// waste already collected stays counted. The camera is snapped behind the player so the view does not swing across
/// the level. In a scene without checkpoints it falls back to ending the game as Lost.
/// The threshold comes from the <see cref="LevelConfig"/> held by the <see cref="GameManager"/> when available.
/// </summary>
public class KillPlane : MonoBehaviour
{
    [Tooltip("Used when no GameManager/LevelConfig is present.")]
    [SerializeField] private float killY = -10f;

    /// <summary>Raised after the player has been put back at a checkpoint.</summary>
    public static event Action<Checkpoint> PlayerRespawned;

    /// <summary>How many times the player was put back at a checkpoint since the level loaded.</summary>
    public int Respawns { get; private set; }

    private Transform player;
    private PlayerController controller;
    private ForestTrail trail;

    private void Start()
    {
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            controller = playerObject.GetComponent<PlayerController>();
        }
        trail = FindAnyObjectByType<ForestTrail>();
    }

    private void Update()
    {
        if (player == null) return;

        var manager = GameManager.Instance;
        float threshold = manager != null && manager.Config != null ? manager.Config.killPlaneY : killY;
        if (player.position.y >= threshold || manager == null || manager.Session.State != GameState.Playing) return;

        int zone = trail != null ? trail.ZoneAt(trail.Project(player.position, out _)) : 0;
        var checkpoint = Checkpoint.ForZone(zone);
        if (checkpoint == null || controller == null)
        {
            manager.Lose();
            return;
        }

        controller.Respawn(checkpoint.transform.position, checkpoint.transform.rotation);
        SnapCameraBehind(checkpoint.transform.eulerAngles.y);
        Respawns++;
        PlayerRespawned?.Invoke(checkpoint);
    }

    // Turn the orbit behind the new facing and drop the camera's damping history, so it cuts to the checkpoint
    // instead of flying there.
    private static void SnapCameraBehind(float yaw)
    {
        var orbit = FindAnyObjectByType<CinemachineOrbitalFollow>();
        if (orbit == null) return;

        orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, yaw);
        orbit.VerticalAxis.Value = orbit.VerticalAxis.Center;
        if (orbit.TryGetComponent(out CinemachineCamera vcam)) vcam.PreviousStateIsValid = false;
    }
}
