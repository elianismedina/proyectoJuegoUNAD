using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Guards the look of Level01 (see Docs/NaturePackIntegrationPlan.md, phase F): depth fog that never hazes the lane,
/// a post-processing volume that belongs to the level, and a sun that casts shadows.
/// </summary>
public class LevelAtmospherePlayModeTests
{
    private const string LevelScene = "Level01";

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
        yield return null;
    }

    [Test]
    public void Fog_IsOnAndStartsBeyondTheLane()
    {
        Assert.IsTrue(RenderSettings.fog, "Level01 needs depth fog.");
        // The camera sits about 8 m from the lane; fog must not touch the player, obstacles or hazards.
        Assert.GreaterOrEqual(RenderSettings.fogStartDistance, 15f, "Fog must start well past the lane so gameplay objects stay crisp.");
    }

    [Test]
    public void PostProcessing_UsesTheLevelsOwnVolume()
    {
        var camera = Camera.main;
        Assert.IsNotNull(camera);
        Assert.IsTrue(camera.GetUniversalAdditionalCameraData().renderPostProcessing, "Post-processing must be on for the main camera.");

        Volume levelVolume = null;
        foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (volume.isGlobal && volume.sharedProfile != null && volume.sharedProfile.name == "Level01_VolumeProfile") levelVolume = volume;
        Assert.IsNotNull(levelVolume, "Level01 needs a global Volume using Level01_VolumeProfile (not the shared DefaultVolumeProfile).");
    }

    [Test]
    public void Post_HasNoDisorientingEffects()
    {
        var volume = Object.FindFirstObjectByType<Volume>();
        Assert.IsNotNull(volume);
        var profile = volume.sharedProfile;

        // Accessibility (GDD section 11): none of the motion-heavy effects may be active.
        Assert.IsFalse(profile.TryGet(out MotionBlur motionBlur) && motionBlur.active, "No motion blur.");
        Assert.IsFalse(profile.TryGet(out FilmGrain grain) && grain.active, "No film grain.");
        Assert.IsFalse(profile.TryGet(out ChromaticAberration aberration) && aberration.active, "No chromatic aberration.");
        Assert.IsFalse(profile.TryGet(out LensDistortion distortion) && distortion.active, "No lens distortion.");
    }

    [Test]
    public void Sun_CastsShadows()
    {
        Assert.IsNotNull(RenderSettings.sun, "RenderSettings.sun must point at the directional light.");
        Assert.AreNotEqual(LightShadows.None, RenderSettings.sun.shadows);
    }
}
