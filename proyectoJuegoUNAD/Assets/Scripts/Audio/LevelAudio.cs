using UnityEngine;

/// <summary>
/// Level sound: a forest ambience bed, the pickup sound for waste, and one-shot sounds for the game events:
/// stumbling on a hazard (<see cref="PlayerController.Stumbled"/>), reaching the goal with waste still missing
/// (<see cref="GameSession.GoalReachedWithMissingWaste"/>), winning and losing (<see cref="GameSession.StateChanged"/>).
/// The ambience plays the clips in <see cref="ambientClips"/> one after another, looping the list and
/// crossfading between them so the short clips do not repeat audibly; with a single clip it simply loops.
/// The pickup sound plays on <see cref="Collectible.Collected"/> with a slight pitch variation.
/// All are 2D (non-spatial), so they sound the same wherever the camera is. Pausing the game lowers the
/// ambience instead of cutting it, and so does the end of the game, so the win / lose sound stands out;
/// the fades use unscaled time because pausing freezes <see cref="Time.timeScale"/>.
/// </summary>
public class LevelAudio : MonoBehaviour
{
    [Header("Ambience")]
    [SerializeField, Tooltip("Ambient loops, played in order and crossfaded.")]
    private AudioClip[] ambientClips;

    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.5f;

    [SerializeField, Min(0.1f), Tooltip("Seconds of overlap between two ambient clips.")]
    private float crossfadeDuration = 2f;

    [SerializeField, Range(0f, 1f), Tooltip("Ambience volume multiplier while the game is paused.")]
    private float pausedVolumeScale = 0.4f;

    [Header("Pickup")]
    [SerializeField] private AudioClip pickupClip;

    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.8f;

    [SerializeField, Range(0f, 0.3f), Tooltip("Random pitch offset, so repeated pickups do not sound identical.")]
    private float pickupPitchVariation = 0.06f;

    [Header("Game events")]
    [SerializeField] private AudioClip stumbleClip;
    [SerializeField, Range(0f, 1f)] private float stumbleVolume = 0.8f;

    [SerializeField, Tooltip("Reaching the goal with waste still missing.")]
    private AudioClip goalMissingClip;
    [SerializeField, Range(0f, 1f)] private float goalMissingVolume = 0.9f;

    [SerializeField] private AudioClip winClip;
    [SerializeField, Range(0f, 1f)] private float winVolume = 0.9f;

    [SerializeField] private AudioClip loseClip;
    [SerializeField, Range(0f, 1f)] private float loseVolume = 0.9f;

    [SerializeField, Range(0f, 1f), Tooltip("Ambience volume multiplier once the level is won or lost.")]
    private float endVolumeScale = 0.35f;

    private AudioSource[] ambientSources;
    private int activeSource;
    private int clipIndex;
    private float fade = 1f;
    private float volumeScale = 1f;
    private AudioSource sfxSource;
    private AudioSource eventSource;
    private GameSession session;

    /// <summary>The ambient clip currently fading in or playing, or null when there is no ambience.</summary>
    public AudioClip CurrentAmbientClip => ambientSources != null ? ambientSources[activeSource].clip : null;

    public AudioClip PickupClip => pickupClip;

    /// <summary>How many pickup sounds were played since the level loaded.</summary>
    public int PickupSoundsPlayed { get; private set; }

    public AudioClip StumbleClip => stumbleClip;
    public AudioClip GoalMissingClip => goalMissingClip;
    public AudioClip WinClip => winClip;
    public AudioClip LoseClip => loseClip;

    /// <summary>The last game-event sound played (stumble, goal with waste missing, win or lose), or null.</summary>
    public AudioClip LastEventClip { get; private set; }

    /// <summary>How many game-event sounds were played since the level loaded.</summary>
    public int EventSoundsPlayed { get; private set; }

    private void Awake()
    {
        ambientSources = new[] { CreateSource("Ambience A"), CreateSource("Ambience B") };
        sfxSource = CreateSource("Pickup SFX");
        // Its own source: the pickup source's random pitch must not detune these.
        eventSource = CreateSource("Event SFX");
    }

    private void OnEnable()
    {
        Collectible.Collected += OnCollected;
        PlayerController.Stumbled += OnStumbled;
    }

    private void OnDisable()
    {
        Collectible.Collected -= OnCollected;
        PlayerController.Stumbled -= OnStumbled;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            session = GameManager.Instance.Session;
            session.StateChanged += OnStateChanged;
            session.GoalReachedWithMissingWaste += OnGoalReachedWithMissingWaste;
        }

        if (ambientClips == null || ambientClips.Length == 0) return;

        AudioSource source = ambientSources[activeSource];
        source.clip = ambientClips[0];
        source.loop = ambientClips.Length == 1;
        source.volume = ambientVolume;
        source.Play();
    }

    private void OnDestroy()
    {
        if (session == null) return;

        session.StateChanged -= OnStateChanged;
        session.GoalReachedWithMissingWaste -= OnGoalReachedWithMissingWaste;
    }

    private void Update()
    {
        if (ambientClips == null || ambientClips.Length < 2) return;

        AudioSource current = ambientSources[activeSource];
        if (current.isPlaying && current.clip.length - current.time <= crossfadeDuration && fade >= 1f)
            PlayNextAmbientClip();

        if (fade < 1f)
            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / crossfadeDuration);

        AudioSource previous = ambientSources[1 - activeSource];
        ambientSources[activeSource].volume = ambientVolume * volumeScale * fade;
        previous.volume = ambientVolume * volumeScale * (1f - fade);
        if (fade >= 1f && previous.isPlaying) previous.Stop();
    }

    private void PlayNextAmbientClip()
    {
        clipIndex = (clipIndex + 1) % ambientClips.Length;
        activeSource = 1 - activeSource;
        fade = 0f;

        AudioSource next = ambientSources[activeSource];
        next.clip = ambientClips[clipIndex];
        next.volume = 0f;
        next.Play();
    }

    private void OnCollected(Collectible item)
    {
        if (pickupClip == null) return;

        sfxSource.pitch = 1f + Random.Range(-pickupPitchVariation, pickupPitchVariation);
        sfxSource.PlayOneShot(pickupClip, pickupVolume);
        PickupSoundsPlayed++;
    }

    private void OnStumbled(PlayerController player) => PlayEvent(stumbleClip, stumbleVolume);

    private void OnGoalReachedWithMissingWaste(int missing) => PlayEvent(goalMissingClip, goalMissingVolume);

    private void PlayEvent(AudioClip clip, float volume)
    {
        if (clip == null) return;

        eventSource.PlayOneShot(clip, volume);
        LastEventClip = clip;
        EventSoundsPlayed++;
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Won) PlayEvent(winClip, winVolume);
        else if (state == GameState.Lost) PlayEvent(loseClip, loseVolume);

        volumeScale = state == GameState.Paused ? pausedVolumeScale
            : state == GameState.Won || state == GameState.Lost ? endVolumeScale
            : 1f;
        if (ambientClips != null && ambientClips.Length == 1)
            ambientSources[activeSource].volume = ambientVolume * volumeScale;
    }

    private AudioSource CreateSource(string sourceName)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);

        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }
}
