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
        return Face(player, Vector3.right);
    }

    /// <summary>Makes "forward" input move the player along <paramref name="direction"/> (only its ground-plane part counts).</summary>
    public static Transform Face(GameObject player, Vector3 direction)
    {
        var controller = player.GetComponent<PlayerController>();
        var reference = controller.MovementReference;
        if (reference == null || reference.name != "TestMovementReference")
            reference = new GameObject("TestMovementReference").transform;

        direction.y = 0f;
        reference.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        controller.MovementReference = reference;
        return reference;
    }
}
