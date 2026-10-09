using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Turns the side-view Cinemachine camera of Level01 into a third person camera behind the player
/// (Docs/ThirdPersonConversionPlan.md, phase 1): Orbital Follow on a sphere, Rotation Composer aimed at the
/// player's chest, and an Input Axis Controller so the mouse or right stick can orbit. The camera eases back
/// behind the player when nobody touches the mouse, so the game is playable with the keyboard alone.
/// Safe to run more than once; it also turns the player to face the level goal.
/// </summary>
public static class ThirdPersonCameraSetup
{
    private const string OldCameraName = "CM Side Camera";
    private const string CameraName = "CM Player Camera";

    // Camera framing, tuned for a 1.6 m character in a forest: close enough to read the trail, high enough to see over bushes.
    private const float OrbitRadius = 6f;
    private const float DefaultPitch = 18f;
    private static readonly Vector2 PitchRange = new Vector2(-5f, 60f);
    private static readonly Vector3 AimOffset = new Vector3(0f, 1.2f, 0f);

    // Recentering: wait this long without mouse input, then swing behind the player over this many seconds.
    private const float RecenterWait = 1.5f;
    private const float RecenterTime = 1f;

    [MenuItem("Forest Guardian/Player/Set Up Third Person Camera")]
    public static void SetUp()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Level01")
        {
            Debug.LogError("ThirdPersonCameraSetup: open the Level01 scene first (active scene is '" + scene.name + "').");
            return;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("ThirdPersonCameraSetup: no GameObject tagged Player in Level01.");
            return;
        }

        var cameraObject = GameObject.Find(CameraName) ?? GameObject.Find(OldCameraName);
        if (cameraObject == null)
        {
            Debug.LogError("ThirdPersonCameraSetup: no '" + OldCameraName + "' or '" + CameraName + "' in Level01.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(cameraObject, "Set Up Third Person Camera");
        cameraObject.name = CameraName;

        // The side view's components fight the orbit: the position composer places the camera itself and the
        // confiner keeps it inside the side-view bounds box.
        RemoveIfPresent<CinemachinePositionComposer>(cameraObject);
        RemoveIfPresent<CinemachineConfiner3D>(cameraObject);

        // The camera recenters behind the player's facing, so start the player looking down the course.
        var goal = Object.FindFirstObjectByType<LevelGoal>();
        if (goal != null)
        {
            Vector3 toGoal = goal.transform.position - player.transform.position;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude > 0.01f)
            {
                Undo.RecordObject(player.transform, "Face the level goal");
                player.transform.rotation = Quaternion.LookRotation(toGoal.normalized, Vector3.up);
            }
        }

        var root = player.transform.Find("PlayerCameraRoot");
        var target = root != null ? root : player.transform;

        var vcam = cameraObject.GetComponent<CinemachineCamera>();
        vcam.Target.TrackingTarget = target;
        vcam.Target.LookAtTarget = null;
        vcam.Target.CustomLookAtTarget = false;

        var orbit = GetOrAdd<CinemachineOrbitalFollow>(cameraObject);
        orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orbit.Radius = OrbitRadius;
        orbit.TargetOffset = root != null ? Vector3.zero : AimOffset;
        orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
        orbit.TrackerSettings.PositionDamping = new Vector3(0.3f, 0.3f, 0.3f);
        orbit.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.TrackingTarget;

        // Start behind the player (yaw 0 puts the camera on -Z looking toward +Z).
        orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, player.transform.eulerAngles.y);
        orbit.HorizontalAxis.Recentering.Enabled = true;
        orbit.HorizontalAxis.Recentering.Wait = RecenterWait;
        orbit.HorizontalAxis.Recentering.Time = RecenterTime;

        orbit.VerticalAxis.Range = PitchRange;
        orbit.VerticalAxis.Center = DefaultPitch;
        orbit.VerticalAxis.Value = DefaultPitch;
        orbit.VerticalAxis.Recentering.Enabled = true;
        orbit.VerticalAxis.Recentering.Wait = RecenterWait;
        orbit.VerticalAxis.Recentering.Time = RecenterTime;

        var composer = GetOrAdd<CinemachineRotationComposer>(cameraObject);
        composer.TargetOffset = root != null ? Vector3.zero : AimOffset;
        composer.Damping = new Vector2(0.4f, 0.4f);

        // Added last so it discovers the orbit's axes and fills in its default mouse / right stick bindings.
        var input = GetOrAdd<CinemachineInputAxisController>(cameraObject);
        input.SynchronizeControllers();

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = cameraObject;
        Debug.Log("ThirdPersonCameraSetup: '" + CameraName + "' now orbits " + target.name + ". Save the scene to keep it.");
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent(out T existing) ? existing : Undo.AddComponent<T>(go);
    }

    private static void RemoveIfPresent<T>(GameObject go) where T : Component
    {
        if (go.TryGetComponent(out T component)) Undo.DestroyObjectImmediate(component);
    }
}
