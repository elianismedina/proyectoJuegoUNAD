using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

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

    // Recentering: wait this long without mouse input, then ease behind the player over this many seconds.
    // Movement is camera-relative, so holding A/D keeps the player 90° off the camera and recentering chases it
    // in a circle at roughly 90° / Time per second. A long Time keeps that turn slow (about 35° per second when
    // play-tested, starting only after the wait) instead of the dizzy 160° per second spin of the old 1 s swing
    // (playtest critique). Pitch only settles back, so it can be quicker.
    private const float RecenterWait = 2.5f;
    private const float RecenterTime = 4f;
    private const float PitchRecenterTime = 2.5f;

    // Mouse / right stick orbit, read from Cinemachine's own Look action (mouse in pixels per second × 0.1,
    // stick × 100 degrees per second), which suits its speed-based axis driver. The gains bring the mouse down to
    // 0.06° per pixel and the stick to 60° per second (pitch a bit slower); the accel / decel times ease the
    // start and stop of every orbit so a flick of the mouse never jerks the view.
    private const string LookActionAsset = CinemachineCore.kPackageRoot + "/Runtime/Input/CinemachineDefaultInputActions.inputactions";
    private const string LookActionName = "Look";
    private const float OrbitGainX = 0.6f;
    private const float OrbitGainY = -0.4f; // negative: mouse up looks up
    private const float OrbitAccelTime = 0.1f;
    private const float OrbitDecelTime = 0.2f;

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
        orbit.VerticalAxis.Recentering.Time = PitchRecenterTime;

        var composer = GetOrAdd<CinemachineRotationComposer>(cameraObject);
        composer.TargetOffset = root != null ? Vector3.zero : AimOffset;
        composer.Damping = new Vector2(0.4f, 0.4f);

        // Added last so it discovers the orbit's axes. Its automatic default bindings are not reliable when the
        // component is added from a script (they came out empty), so the look controllers are bound explicitly.
        var input = GetOrAdd<CinemachineInputAxisController>(cameraObject);
        input.SynchronizeControllers();
        var look = LoadLookAction();
        foreach (var controller in input.Controllers)
        {
            bool isX = controller.Name == "Look Orbit X";
            if (!isX && controller.Name != "Look Orbit Y") continue;
            controller.Enabled = true;
            controller.Input.InputAction = look;
            controller.Input.Gain = isX ? OrbitGainX : OrbitGainY;
            controller.Input.CancelDeltaTime = false;
            controller.Driver.AccelTime = OrbitAccelTime;
            controller.Driver.DecelTime = OrbitDecelTime;
        }
        if (look == null)
            Debug.LogWarning("ThirdPersonCameraSetup: no '" + LookActionName + "' action in " + LookActionAsset + "; the mouse will not orbit.");

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = cameraObject;
        Debug.Log("ThirdPersonCameraSetup: '" + CameraName + "' now orbits " + target.name + ". Save the scene to keep it.");
    }

    private static InputActionReference LoadLookAction()
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(LookActionAsset))
        {
            if (asset is InputActionReference reference && reference.action != null && reference.action.name == LookActionName)
                return reference;
        }
        return null;
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
