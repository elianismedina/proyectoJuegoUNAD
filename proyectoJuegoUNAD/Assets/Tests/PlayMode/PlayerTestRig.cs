using UnityEngine;

/// <summary>
/// Shared setup for PlayMode tests that drive the player with <c>StarterAssetsInputs.move</c>.
/// Movement is relative to the camera, which can face anywhere; tests pin it to a fixed reference instead.
/// </summary>
public static class PlayerTestRig
{
    /// <summary>
    /// Makes "forward" input (<see cref="Vector2.up"/>) move the player toward +X, down the course,
    /// and "right" input (<see cref="Vector2.right"/>) toward -Z. Returns the reference so a test can turn it.
    /// </summary>
    public static Transform FaceCourse(GameObject player)
    {
        var reference = new GameObject("TestMovementReference").transform;
        reference.rotation = Quaternion.Euler(0f, 90f, 0f);
        player.GetComponent<PlayerController>().MovementReference = reference;
        return reference;
    }
}
