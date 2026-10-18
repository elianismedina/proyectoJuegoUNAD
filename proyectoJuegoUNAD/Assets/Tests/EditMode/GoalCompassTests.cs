using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The HUD arrow that shows the way to the goal (GDD §10.2): its maths and its place on the GameScreens canvas.
public class GoalCompassTests
{
    const string ScreensPath = "Assets/Prefabs/UI/GameScreens.prefab";

    [TestCase(0f, 1f, 0f)]      // straight ahead
    [TestCase(1f, 0f, 90f)]     // to the right
    [TestCase(-1f, 0f, -90f)]   // to the left
    [TestCase(0f, -1f, 180f)]   // behind
    public void AngleOnScreen_IsZeroAheadAndPositiveToTheRight(float targetX, float targetZ, float expected)
    {
        float angle = GoalCompass.AngleOnScreen(Vector3.forward, new Vector3(targetX, 5f, targetZ)); // height is ignored
        Assert.AreEqual(Mathf.Abs(expected), Mathf.Abs(angle), 0.01f);
        if (Mathf.Abs(expected) < 179f) Assert.AreEqual(expected, angle, 0.01f);
    }

    [Test]
    public void IsGoingBack_OnlyWhenRunningBackAlongTheTrail()
    {
        Vector3 trail = Vector3.forward;
        Assert.IsTrue(GoalCompass.IsGoingBack(new Vector3(0f, 0f, -6f), trail), "Running straight back.");
        Assert.IsTrue(GoalCompass.IsGoingBack(new Vector3(1.5f, 0f, -6f), trail), "Mostly back is still back.");
        Assert.IsFalse(GoalCompass.IsGoingBack(new Vector3(0f, 0f, 6f), trail), "Running toward the goal.");
        Assert.IsFalse(GoalCompass.IsGoingBack(new Vector3(6f, 0f, -1.5f), trail), "Crossing the path or going round an obstacle.");
        Assert.IsFalse(GoalCompass.IsGoingBack(new Vector3(0f, 0f, -0.5f), trail), "A small step back, or the stumble's end.");
        Assert.IsFalse(GoalCompass.IsGoingBack(Vector3.zero, trail), "Standing still.");
    }

    [Test]
    public void GameScreens_HasTheCompassInTheHudUnderThePanels()
    {
        var screens = AssetDatabase.LoadAssetAtPath<GameObject>(ScreensPath);
        Assert.IsNotNull(screens, "Missing " + ScreensPath);
        var compass = screens.GetComponentInChildren<GoalCompass>(true);
        Assert.IsNotNull(compass, "GameScreens needs the GoalCompass (Forest Guardian > UI > Build Goal Compass).");

        var so = new SerializedObject(compass);
        var arrow = so.FindProperty("arrow").objectReferenceValue as RectTransform;
        var image = so.FindProperty("arrowImage").objectReferenceValue;
        var label = so.FindProperty("wrongWayLabel").objectReferenceValue as GameObject;
        Assert.IsNotNull(arrow, "The compass needs its arrow.");
        Assert.IsNotNull(image, "The arrow needs its image.");
        Assert.IsNotNull(new SerializedObject(image).FindProperty("m_Sprite").objectReferenceValue, "The arrow needs its sprite.");
        Assert.IsNotNull(label, "The compass needs the wrong-way label.");
        Assert.IsFalse(label.activeSelf, "The warning starts hidden.");

        var win = screens.transform.Find("WinPanel");
        var pause = screens.transform.Find("PausePanel");
        Assert.Less(compass.transform.GetSiblingIndex(), win.GetSiblingIndex(), "The win panel must draw over the compass.");
        Assert.Less(compass.transform.GetSiblingIndex(), pause.GetSiblingIndex(), "The pause menu must draw over the compass.");
    }
}
