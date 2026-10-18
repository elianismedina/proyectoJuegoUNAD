using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Discreet HUD arrow at the top of the screen that shows the way to the goal (GDD §10.2): it points, relative to the
/// camera, at a spot <see cref="lookAhead"/> metres further down the <see cref="ForestTrail"/>, so on a bend it follows
/// the path instead of pointing through the trees; near the end that spot is the <see cref="LevelGoal"/> itself.
/// When the player keeps running back along the trail for <see cref="wrongWaySeconds"/>, the arrow turns orange and
/// "Vas en sentido contrario" appears beside it. Nothing flashes (accessibility). Hidden whenever the game is not Playing.
/// Built into <c>GameScreens.prefab</c> by <i>Forest Guardian &gt; UI &gt; Build Goal Compass</i>.
/// </summary>
public class GoalCompass : MonoBehaviour
{
    public const string WrongWayMessage = "Vas en sentido contrario";

    [SerializeField] private RectTransform arrow;
    [SerializeField] private Image arrowImage;
    [SerializeField] private GameObject wrongWayLabel;

    [SerializeField, Min(1f), Tooltip("How far ahead along the trail the arrow aims, in metres.")]
    private float lookAhead = 10f;

    [SerializeField, Min(0.1f), Tooltip("Seconds of running back along the trail before the warning shows.")]
    private float wrongWaySeconds = 1f;

    [SerializeField, Min(0f), Tooltip("Degrees per second the arrow turns at most, so it glides instead of jittering.")]
    private float turnSpeed = 360f;

    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color wrongWayColor = new Color(1f, 0.6f, 0.15f, 1f);

    /// <summary>Speed (m/s) along the trail below which the player is not considered to be going anywhere.</summary>
    public const float MinTravelSpeed = 1f;

    private PlayerController player;
    private ForestTrail trail;
    private LevelGoal goal;
    private float angle;
    private float wrongWayTimer;

    /// <summary>The arrow's current angle in degrees: 0 straight ahead (up on screen), positive to the right.</summary>
    public float ArrowAngle => angle;

    public bool WrongWayShown => wrongWayLabel.activeSelf;

    /// <summary>
    /// Signed angle in degrees from <paramref name="viewForward"/> to <paramref name="toTarget"/> on the ground plane:
    /// 0 when the target is straight ahead, positive to the right, ±180 behind.
    /// </summary>
    public static float AngleOnScreen(Vector3 viewForward, Vector3 toTarget)
    {
        viewForward.y = 0f;
        toTarget.y = 0f;
        if (viewForward.sqrMagnitude < 1e-6f || toTarget.sqrMagnitude < 1e-6f) return 0f;
        return Vector3.SignedAngle(viewForward, toTarget, Vector3.up);
    }

    /// <summary>
    /// True when <paramref name="velocity"/> runs back along the trail: at least <see cref="MinTravelSpeed"/> against
    /// <paramref name="trailForward"/>, and more back than sideways (so walking across the path or round an obstacle is fine).
    /// </summary>
    public static bool IsGoingBack(Vector3 velocity, Vector3 trailForward)
    {
        velocity.y = 0f;
        trailForward.y = 0f;
        if (trailForward.sqrMagnitude < 1e-6f) return false;
        float along = Vector3.Dot(velocity, trailForward.normalized);
        return along <= -MinTravelSpeed && -along >= velocity.magnitude * 0.7f;
    }

    private void Start()
    {
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.GetComponent<PlayerController>();
        trail = FindAnyObjectByType<ForestTrail>();
        goal = FindAnyObjectByType<LevelGoal>();
        wrongWayLabel.SetActive(false);
        arrowImage.color = normalColor;
    }

    private void Update()
    {
        bool playing = GameManager.Instance == null || GameManager.Instance.Session.State == GameState.Playing;
        var view = Camera.main;
        bool show = playing && player != null && view != null && (trail != null || goal != null);
        arrow.gameObject.SetActive(show);
        if (!show)
        {
            wrongWayTimer = 0f;
            wrongWayLabel.SetActive(false);
            return;
        }

        Vector3 position = player.transform.position;
        Vector3 target, trailForward;
        if (trail != null)
        {
            float s = trail.Project(position, out _);
            target = trail.PointAt(Mathf.Min(s + lookAhead, trail.Length));
            if (goal != null && trail.Length - s < lookAhead) target = goal.transform.position;
            trailForward = trail.TangentAt(s);
        }
        else
        {
            target = goal.transform.position;
            trailForward = target - position;
        }

        float wanted = AngleOnScreen(view.transform.forward, target - position);
        angle = Mathf.MoveTowardsAngle(angle, wanted, turnSpeed * Time.unscaledDeltaTime);
        arrow.localRotation = Quaternion.Euler(0f, 0f, -angle);

        wrongWayTimer = IsGoingBack(player.HorizontalVelocity, trailForward) ? wrongWayTimer + Time.deltaTime : 0f;
        bool wrongWay = wrongWayTimer >= wrongWaySeconds;
        wrongWayLabel.SetActive(wrongWay);
        arrowImage.color = wrongWay ? wrongWayColor : normalColor;
    }
}
