using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies the look of Level01: sun, sky, ambient light, depth fog and a light post-processing volume.
/// Safe to run repeatedly (it updates what exists and never deletes scene objects).
/// The volume profile is Level01's own asset: the shared DefaultVolumeProfile in Assets/Settings is left alone,
/// because Unity rewrites those shared defaults during builds. See Docs/NaturePackIntegrationPlan.md, phase F.
/// </summary>
public static class LevelAtmosphereSetup
{
    private const string SkyMaterialPath = "Assets/Materials/Level01_Sky.mat";
    private const string VolumeProfilePath = "Assets/Config/Level01_VolumeProfile.asset";
    private const string VolumeObjectName = "Level01 Volume";

    // Light. The sun comes from the camera side (yaw 330) so the lane faces are lit and shadows fall behind the player.
    private static readonly Color SunColor = new Color(1f, 0.95f, 0.84f);
    private const float SunIntensity = 1.1f;
    private const float SunShadowStrength = 0.85f; // Softer than 1 so shaded faces stay readable.
    private static readonly Vector3 SunRotation = new Vector3(48f, 330f, 0f);

    // Ambient, as a gradient so it does not depend on the procedural sky.
    private static readonly Color AmbientSky = new Color(0.62f, 0.74f, 0.92f);
    private static readonly Color AmbientEquator = new Color(0.56f, 0.64f, 0.56f);
    private static readonly Color AmbientGround = new Color(0.34f, 0.32f, 0.26f);

    // Depth fog. Linear and starting past the lane (camera is about 8 m away), so the player, obstacles and hazards
    // are never hazed; the far trees fade a little and the mountains (about 100 m away) mostly dissolve into the sky.
    private static readonly Color FogColor = new Color(0.74f, 0.84f, 0.88f);
    private const float FogStart = 24f;
    private const float FogEnd = 150f;

    [MenuItem("Forest Guardian/Level/Apply Level01 Atmosphere")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Level01")
        {
            Debug.LogError("LevelAtmosphereSetup: open the Level01 scene first (active scene is '" + scene.name + "').");
            return;
        }

        ApplySun();
        ApplySkyAndAmbient();
        ApplyFog();
        ApplyCamera();
        ApplyVolume(scene);

        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("LevelAtmosphereSetup: Level01 atmosphere applied.");
    }

    private static void ApplySun()
    {
        var sun = RenderSettings.sun != null ? RenderSettings.sun : Object.FindFirstObjectByType<Light>();
        if (sun == null || sun.type != LightType.Directional)
        {
            Debug.LogWarning("LevelAtmosphereSetup: no directional light found.");
            return;
        }

        sun.transform.rotation = Quaternion.Euler(SunRotation);
        sun.color = SunColor;
        sun.intensity = SunIntensity;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = SunShadowStrength;
        RenderSettings.sun = sun;
    }

    private static void ApplySkyAndAmbient()
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Level01_Sky" };
            AssetDatabase.CreateAsset(sky, SkyMaterialPath);
        }

        sky.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f)); // Neutral: the shader treats the tint as the scattering complement, so a blue tint gave a yellow horizon band.
        sky.SetColor("_GroundColor", FogColor); // Same as the fog, so distant hills dissolve into the horizon instead of a coloured band.
        sky.SetFloat("_AtmosphereThickness", 1f);
        sky.SetFloat("_Exposure", 1.1f);
        sky.SetFloat("_SunSize", 0.04f);
        EditorUtility.SetDirty(sky);
        AssetDatabase.SaveAssets();

        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor = AmbientGround;
        RenderSettings.ambientIntensity = 1f;
    }

    private static void ApplyFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance = FogEnd;
    }

    private static void ApplyCamera()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("LevelAtmosphereSetup: no main camera found.");
            return;
        }

        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; // Cheap edge smoothing; the URP asset has MSAA off.
        data.antialiasingQuality = AntialiasingQuality.Medium;
        EditorUtility.SetDirty(camera);
    }

    private static void ApplyVolume(UnityEngine.SceneManagement.Scene scene)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Level01_VolumeProfile";
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

        // Deliberately mild: no motion blur, film grain, chromatic aberration or lens distortion (accessibility, GDD section 11).
        var tonemapping = Ensure<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.Neutral);

        var colorAdjustments = Ensure<ColorAdjustments>(profile);
        colorAdjustments.contrast.Override(8f);
        colorAdjustments.saturation.Override(10f);

        var bloom = Ensure<Bloom>(profile);
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.15f);
        bloom.scatter.Override(0.6f);

        var vignette = Ensure<Vignette>(profile);
        vignette.intensity.Override(0.15f);
        vignette.smoothness.Override(0.4f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        Volume volume = null;
        foreach (var existing in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (existing.name == VolumeObjectName) { volume = existing; break; }
        if (volume == null)
        {
            var go = new GameObject(VolumeObjectName);
            volume = go.AddComponent<Volume>();
        }

        volume.isGlobal = true;
        volume.priority = 1f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
    }

    private static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T existing)) return existing;

        T component = profile.Add<T>(true);
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }
}
